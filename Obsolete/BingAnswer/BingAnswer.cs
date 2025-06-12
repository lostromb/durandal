using Durandal.API.Data;

namespace Durandal.Answers.StandardAnswers.Bing.Old
{
    using System;
    using System.Text.RegularExpressions;

    using Durandal.API;

    using Durandal.Common.Logger;

    using Durandal.API.Utils;
    using System.IO;
    using System.Collections.Generic;
    using System.Drawing.Imaging;
    using System.Net;
    using Newtonsoft.Json;
    using CommonViews;
    using Common.Utils.UnitConversion;
    using System.Threading.Tasks;
    using Common.Utils.IO;
    using Common.Net.Http;
    using Common.Net;

    public class BingAnswer : DurandalPlugin
    {
        private readonly string API_KEY = "7857b708c438452a93b0ec5980fddad9";

        public BingAnswer()
            : base("bing")
        {
        }

        protected override ConversationTree BuildConversationTree(ConversationTree returnVal)
        {
            //returnVal.AddStartState("web_search", this.WebSearch);
            //returnVal.AddStartState("image_search", this.ImageSearch);
            //returnVal.AddStartState("navigate_directly", this.DirectNavigate);

            returnVal.AddStartState("calculate", this.Calculate);
            returnVal.AddStartState("unit_convert", this.UnitConvert);

            return returnVal;
        }


#pragma warning disable 649
        private class BingResult
        {
            public Computation computation;
        }

        private class Computation
        {
            public string expression;
            public string value;
        }
#pragma warning restore 649

        public async Task<DialogResult> UnitConvert(QueryWithContext queryWithContext, PluginServices services)
        {
            string sourceUnit = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "source_unit");
            string targetUnit = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "target_unit");
            string amount = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "amount");

            if (!string.IsNullOrEmpty(sourceUnit) && !string.IsNullOrEmpty(targetUnit))
            {
                decimal sourceAmount;
                // FIXME capture the number annotation field instead of the raw slot value
                if (string.IsNullOrEmpty(amount) || !decimal.TryParse(amount, out sourceAmount))
                {
                    services.Logger.Log("Could not parse number " + amount + "; number annotation failed I guess", LogLevel.Wrn);
                    sourceAmount = 1;
                }

                List<UnitConversionResult> results = UnitConverter.Convert(sourceUnit, targetUnit, sourceAmount, services.Logger);

                if (results.Count > 0)
                {
                    UnitConversionResult result = results[0];
                    string patternName;
                    if (result.IsApproximate)
                    {
                        patternName = "CalculationResultApprox";
                    }
                    else
                    {
                        patternName = "CalculationResultExact";
                    }
                    ILGPattern lg = services.LanguageGenerator.GetPattern(patternName, queryWithContext.ClientContext, services.Logger);
                    lg.Sub("source_amount", result.SourceAmountString);
                    if (string.Equals(result.SourceAmountString, "1"))
                    {
                        lg.Sub("source_unit_si", result.SourceUnitName);
                    }
                    else
                    {
                        lg.Sub("source_unit_pl", result.SourceUnitName);
                    }

                    lg.Sub("target_amount", result.TargetAmountString);
                    if (string.Equals(result.TargetAmountString, "1"))
                    {
                        lg.Sub("target_unit_si", result.TargetUnitName);
                    }
                    else
                    {
                        lg.Sub("target_unit_pl", result.TargetUnitName);
                    }
                    string x = lg.Render().Text;

                    return lg.ApplyToDialogResult(new DialogResult(Result.Success)
                    {
                        ResponseHtml = new MessageView()
                        {
                            Content = lg.Render().Text,
                            RequestData = queryWithContext.ClientContext.Data
                        }.Render()
                    });
                }
                else
                {
                    // If conversion failed, fallback to bing
                    return await Calculate(queryWithContext, services);
                }
            }

            return new DialogResult(Result.Skip);
        }

        public async Task<DialogResult> Calculate(QueryWithContext queryWithContext, PluginServices services)
        {
            IHttpClient httpClient = services.HttpClientFactory.CreateHttpClient(new Uri("https://api.cognitive.microsoft.com/"), services.Logger);
            HttpRequest req = HttpRequest.BuildFromUrlString("/bing/v5.0/search", "GET");
            req.RequestHeaders.Add("Ocp-Apim-Subscription-Key", API_KEY);
            req.GetParameters.Add("q", queryWithContext.Understanding.Utterance.OriginalText);
            req.GetParameters.Add("count", "1");
            req.GetParameters.Add("offset", "0");
            req.GetParameters.Add("mkt", queryWithContext.ClientContext.Locale);
            req.GetParameters.Add("safesearch", "Strict");
            NetworkResponseInstrumented<HttpResponse> netResponse = await httpClient.SendRequestAsync(req);
            if (netResponse == null || netResponse.Response == null)
            {
                services.Logger.Log("No response from bing api", LogLevel.Err);
                return new DialogResult(Result.Skip);
            }

            string json = netResponse.Response.GetPayloadAsString();
            BingResult result = JsonConvert.DeserializeObject<BingResult>(json);
            if (result == null || result.computation == null || string.IsNullOrEmpty(result.computation.expression) || string.IsNullOrEmpty(result.computation.value))
            {
                services.Logger.Log("No computation result", LogLevel.Err);
                return new DialogResult(Result.Skip);
            }
            
            decimal val;
            if (!decimal.TryParse(result.computation.value, out val))
            {
                // It's a non-numeric value. This happens for things like "how many cups in a gallon"
                ILGPattern lg = services.LanguageGenerator.GetPattern("CalculationResultNonNumeric", queryWithContext.ClientContext, services.Logger);
                lg.Sub("expression", result.computation.expression);
                lg.Sub("result", result.computation.value);
                return lg.ApplyToDialogResult(new DialogResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = lg.RenderText(),
                        RequestData = queryWithContext.ClientContext.Data
                    }.Render()
                });
            }
            else
            {
                decimal roundedVal = NumberHelpers.RoundToSigFigs(val, 3);

                string exact_result_string = val.ToString();
                string rounded_result_string = roundedVal.ToString();
                string expression = result.computation.expression.Replace(" ", ",");

                ILGPattern lg;
                if (val == roundedVal)
                {
                    lg = services.LanguageGenerator.GetPattern("CalculationResultExact", queryWithContext.ClientContext, services.Logger);
                    lg.Sub("expression", expression);
                    lg.Sub("result", exact_result_string);
                }
                else
                {
                    lg = services.LanguageGenerator.GetPattern("CalculationResultApprox", queryWithContext.ClientContext, services.Logger);
                    lg.Sub("expression", expression);
                    lg.Sub("exact_result", exact_result_string);
                    lg.Sub("result", rounded_result_string);
                }

                return lg.ApplyToDialogResult(new DialogResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = lg.RenderText(),
                        RequestData = queryWithContext.ClientContext.Data
                    }.Render()
                });
            }
        }

        /// <summary>
        /// Scenario: The user wants to do a regular search
        /// "Search the web for ____"
        /// </summary>
        /// <param name="luInput"></param>
        /// <param name="store"></param>
        /// <returns></returns>
        public DialogResult WebSearch(QueryWithContext queryWithContext, PluginServices services)
        {
            // Make sure the client can handle HTML
            if (!queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayBasicHtml) &&
                !queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayHtml5))
            {
                ILGPattern lg = services.LanguageGenerator.GetPattern("CannotDisplayResults", queryWithContext.ClientContext, services.Logger);
                return lg.ApplyToDialogResult(new DialogResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = lg.RenderText(),
                        RequestData = queryWithContext.ClientContext.Data
                    }.Render()
                });
            }
            
            // Extract the query from the text
            string query = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "query");

            if (string.IsNullOrWhiteSpace(query))
            {
                services.Logger.Log("no query extracted", LogLevel.Err);
                return new DialogResult(Result.Skip);
            }

            string bingURL = "http://www.bing.com/search?q=" + System.Web.HttpUtility.UrlEncode(query);
            services.Logger.Log("Searching Bing for " + query);

            ILGPattern pattern = services.LanguageGenerator.GetPattern("WebSearch", queryWithContext.ClientContext, services.Logger)
                .Sub("query", query);

            return pattern.ApplyToDialogResult(new DialogResult(Result.Success)
                {
                    ResponseUrl = bingURL,
                    ResponseHtml = new MessageView()
                    {
                        Content = pattern.RenderText(),
                        RequestData = queryWithContext.ClientContext.Data
                    }.Render()
                });
        }

        /// <summary>
        /// Scenario: The user wants to find pictures of something
        /// "Show me pictures of ____"
        /// </summary>
        /// <param name="luInput"></param>
        /// <param name="store"></param>
        /// <returns></returns>
        public DialogResult ImageSearch(QueryWithContext queryWithContext, PluginServices services)
        {
            // Make sure the client can handle HTML
            if (!queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayBasicHtml) &&
                !queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayHtml5))
            {
                ILGPattern pattern = services.LanguageGenerator.GetPattern("CannotDisplayResults", queryWithContext.ClientContext, services.Logger);
                return pattern.ApplyToDialogResult(new DialogResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = pattern.RenderText(),
                        RequestData = queryWithContext.ClientContext.Data
                    }.Render()
                });
            }
            
            // Extract the query from the text
            string query = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "query");

            if (string.IsNullOrWhiteSpace(query))
            {
                services.Logger.Log("no query extracted", LogLevel.Err);
                return new DialogResult(Result.Skip);
            }

            string bingURL = "http://www.bing.com/images/search?q=" + System.Web.HttpUtility.UrlEncode(query);
            services.Logger.Log("Searching Bing Images for " + query);

            ILGPattern lg = services.LanguageGenerator.GetPattern("ImageSearch", queryWithContext.ClientContext, services.Logger)
                .Sub("query", query);

            return lg.ApplyToDialogResult(new DialogResult(Result.Success)
                {
                    ResponseUrl = bingURL,
                    ResponseHtml = new MessageView()
                    {
                        Content = lg.RenderText(),
                        RequestData = queryWithContext.ClientContext.Data
                    }.Render()
                });
        }

        /// <summary>
        /// Scenario: The user is looking for a specific web site and wants to get there by keyword
        /// "Go to the MSN home page"
        /// </summary>
        /// <param name="luInput"></param>
        /// <param name="store"></param>
        /// <returns></returns>
        public DialogResult DirectNavigate(QueryWithContext queryWithContext, PluginServices services)
        {
            // Make sure the client can handle HTML
            if (!queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayBasicHtml) &&
                !queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayHtml5))
            {
                return services.LanguageGenerator.GetPattern("NoWebBrowser", queryWithContext.ClientContext, services.Logger)
                    .ApplyToDialogResult(new DialogResult(Result.Success));
            }
            
            // Extract the query from the text
            string query = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "query");

            if (string.IsNullOrWhiteSpace(query))
            {
                services.Logger.Log("no query extracted", LogLevel.Err);
                return new DialogResult(Result.Skip);
            }

            string normalizedQuery = query;
            // Remove .com from the query
            normalizedQuery = DurandalUtils.RegexRemove(new Regex("\\.[a-zA-Z]{3}"), normalizedQuery);
            // remove "web site" or "website" from the query
            normalizedQuery = DurandalUtils.RegexRemove(new Regex(" web ?site", RegexOptions.IgnoreCase), normalizedQuery);
            normalizedQuery = DurandalUtils.RegexRemove(new Regex(" dot ?com", RegexOptions.IgnoreCase), normalizedQuery);
            string feelingLuckyUrl = "http://www.google.com/search?btnI=1&q=" + System.Web.HttpUtility.UrlEncode(normalizedQuery);
            services.Logger.Log("Navigating directly to website by query " + normalizedQuery);
            return new DialogResult(Result.Success)
            {
                ResponseUrl = feelingLuckyUrl
            };
        }

        protected override AnswerPluginInformation GetInformation(IResourceManager pluginDataManager, ResourceName pluginDataDirectory)
        {
            MemoryStream pngStream = new MemoryStream();
            AssemblyResources.Icon_bing.Save(pngStream, ImageFormat.Png);

            AnswerPluginInformation returnVal = new AnswerPluginInformation()
            {
                InternalName = "Bing",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
                IconPngData = new ArraySegment<byte>(pngStream.ToArray())
            };

            returnVal.LocalizedInfo.Add("en-us", new LocalizedInformation()
            {
                DisplayName = "Bing Search",
                ShortDescription = "Bing it on!",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-us"].SampleQueries.Add("What is 8 * 7");
            returnVal.LocalizedInfo["en-us"].SampleQueries.Add("How many cups are in a liter?");

            return returnVal;
        }
    }
}
