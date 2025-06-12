
namespace Durandal.Plugins.Bing
{
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.IO;
    using Durandal.Common.Tasks;
    using Durandal.Common.UnitConversion;
    using Durandal.CommonViews;
    using Durandal.ExternalServices.Bing.Search;
    using Durandal.ExternalServices.Bing.Search.Schemas;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web;
    using Durandal.Common.NLP.Language;
    using System.Threading;
    using Durandal.Common.Time;

    public class BingAnswer : DurandalPlugin
    {
        private BingSearch _bingSearch;
        private IHttpClientFactory _overrideHttpClientFactory;

        public BingAnswer()
            : this(null)
        {
        }

        public BingAnswer(IHttpClientFactory overrideHttpClientFactory)
            : base("bing")
        {
            _overrideHttpClientFactory = overrideHttpClientFactory;
        }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            //returnVal.AddStartState("web_search", this.WebSearch);
            //returnVal.AddStartState("image_search", this.ImageSearch);
            //returnVal.AddStartState("navigate_directly", this.DirectNavigate);

            returnVal.AddStartState("calculate", this.Calculate);
            returnVal.AddStartState("convert", this.Convert);

            return returnVal;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            string apiKey = services.PluginConfiguration.GetString("apiKey");

            // detect if we're in a testing environment
            if (_overrideHttpClientFactory != null)
            {
                apiKey = "unit_test_api_key";
            }
            else
            {
                apiKey = services.PluginConfiguration.GetString("apiKey");
            }

            if (!string.IsNullOrEmpty(apiKey))
            {
                _bingSearch = new BingSearch(apiKey, _overrideHttpClientFactory ?? services.HttpClientFactory, services.Logger, BingApiVersion.V7Internal);
            }
            else
            {
                services.Logger.Log("Cannot connect to Bing Search API without an API key", LogLevel.Err);
                _bingSearch = null;
                await DurandalTaskExtensions.NoOpTask;
            }
        }

        public async Task<PluginResult> Convert(QueryWithContext queryWithContext, IPluginServices services)
        {
            string sourceUnit = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "source_unit");
            string targetUnit = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "target_unit");
            SlotValue amountSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "amount");

            decimal sourceAmount = 1;
            if (amountSlot != null)
            {
                decimal? numberAnnotationValue = amountSlot.GetNumber();
                if (!numberAnnotationValue.HasValue)
                {
                    services.Logger.Log("Could not parse number " + amountSlot.Value + "; number annotation failed I guess", LogLevel.Wrn);
                }
                else
                {
                    sourceAmount = numberAnnotationValue.Value;
                }
            }

            // Does this conversion involve currency?
            if (IsCurrencyCode(sourceUnit) ||
                IsCurrencyCode(targetUnit))
            {
                // If target type is not specified, assume one based on the current market
                if (string.IsNullOrEmpty(sourceUnit))
                {
                    sourceUnit = GetDefaultCurrencyForLocale(queryWithContext.ClientContext.Locale);
                }

                if (string.IsNullOrEmpty(targetUnit))
                {
                    targetUnit = GetDefaultCurrencyForLocale(queryWithContext.ClientContext.Locale);
                }

                BingResponse currencyResponse = await _bingSearch.Query(
                    "Convert " + sourceAmount + " " + sourceUnit + " to " + targetUnit,
                    services.Logger,
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton,
                    LanguageCode.Parse("en-US"));
                if (currencyResponse != null && currencyResponse.Currency != null && currencyResponse.Currency.Value != null)
                {
                    decimal toValue = currencyResponse.Currency.Value.ToValue;

                    string lgResponse = sourceAmount + " " + sourceUnit + " = " + toValue + " " + targetUnit;

                    MessageView responseView = new MessageView()
                    {
                        Content = lgResponse,
                        Subscript = currencyResponse.Currency.Attributions[0].CopyrightMessage
                    };

                    return new PluginResult(Result.Success)
                    {
                        ResponseText = lgResponse,
                        ResponseSsml = lgResponse,
                        ResponseHtml = responseView.Render()
                    };
                }
            }

            if (!string.IsNullOrEmpty(sourceUnit) && !string.IsNullOrEmpty(targetUnit))
            {
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
                    string x = (await lg.Render()).Text;

                    return await lg.ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        ResponseHtml = new MessageView()
                        {
                            Content = (await lg.Render()).Text,
                            ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                        }.Render()
                    });
                }
                else
                {
                    // If conversion failed, fallback to bing
                    return await Calculate(queryWithContext, services);
                }
            }

            return new PluginResult(Result.Skip);
        }

        public async Task<PluginResult> Calculate(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (_bingSearch == null)
            {
                services.Logger.Log("No Bing API key in configuration!", LogLevel.Err);
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "No Bing API key in configuration!"
                };
            }

            BingResponse result = await _bingSearch.Query(
                queryWithContext.Understanding.Utterance.OriginalText,
                services.Logger,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton,
                queryWithContext.ClientContext.Locale);
            
            if (result == null || result.Computation == null || string.IsNullOrEmpty(result.Computation.Expression) || string.IsNullOrEmpty(result.Computation.Value))
            {
                services.Logger.Log("No computation result", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }
            
            decimal val;
            if (!decimal.TryParse(result.Computation.Value, out val))
            {
                // It's a non-numeric value. This happens for things like "how many cups in a gallon"
                ILGPattern lg = services.LanguageGenerator.GetPattern("CalculationResultNonNumeric", queryWithContext.ClientContext, services.Logger);
                lg.Sub("expression", result.Computation.Expression);
                lg.Sub("result", result.Computation.Value);
                return await lg.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await lg.Render()).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                });
            }
            else
            {
                decimal roundedVal = NumberHelpers.RoundToSigFigs(val, 3);

                string exact_result_string = val.ToString();
                string rounded_result_string = roundedVal.ToString();
                string expression = result.Computation.Expression.Replace(" ", ",");

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

                return await lg.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await lg.Render()).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
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
        public async Task<PluginResult> WebSearch(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Make sure the client can handle HTML
            if (!queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayBasicHtml) &&
                !queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayHtml5))
            {
                ILGPattern lg = services.LanguageGenerator.GetPattern("CannotDisplayResults", queryWithContext.ClientContext, services.Logger);
                return await lg.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await lg.Render()).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                });
            }
            
            // Extract the query from the text
            string query = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "query");

            if (string.IsNullOrWhiteSpace(query))
            {
                services.Logger.Log("no query extracted", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }

            string bingURL = "http://www.bing.com/search?q=" + WebUtility.UrlEncode(query);
            services.Logger.Log("Searching Bing for " + query);

            ILGPattern pattern = services.LanguageGenerator.GetPattern("WebSearch", queryWithContext.ClientContext, services.Logger)
                .Sub("query", query);

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseUrl = bingURL,
                    ResponseHtml = new MessageView()
                    {
                        Content = (await pattern.Render()).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
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
        public async Task<PluginResult> ImageSearch(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Make sure the client can handle HTML
            if (!queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayBasicHtml) &&
                !queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayHtml5))
            {
                ILGPattern pattern = services.LanguageGenerator.GetPattern("CannotDisplayResults", queryWithContext.ClientContext, services.Logger);
                return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await pattern.Render()).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                });
            }
            
            // Extract the query from the text
            string query = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "query");

            if (string.IsNullOrWhiteSpace(query))
            {
                services.Logger.Log("no query extracted", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }

            string bingURL = "http://www.bing.com/images/search?q=" + WebUtility.UrlEncode(query);
            services.Logger.Log("Searching Bing Images for " + query);

            ILGPattern lg = services.LanguageGenerator.GetPattern("ImageSearch", queryWithContext.ClientContext, services.Logger)
                .Sub("query", query);

            return await lg.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseUrl = bingURL,
                    ResponseHtml = new MessageView()
                    {
                        Content = (await lg.Render()).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
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
        public async Task<PluginResult> DirectNavigate(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Make sure the client can handle HTML
            if (!queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayBasicHtml) &&
                !queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayHtml5))
            {
                return await services.LanguageGenerator.GetPattern("NoWebBrowser", queryWithContext.ClientContext, services.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }
            
            // Extract the query from the text
            string query = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "query");

            if (string.IsNullOrWhiteSpace(query))
            {
                services.Logger.Log("no query extracted", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }

            string normalizedQuery = query;
            // Remove .com from the query
            normalizedQuery = StringUtils.RegexRemove(new Regex("\\.[a-zA-Z]{3}"), normalizedQuery);
            // remove "web site" or "website" from the query
            normalizedQuery = StringUtils.RegexRemove(new Regex(" web ?site", RegexOptions.IgnoreCase), normalizedQuery);
            normalizedQuery = StringUtils.RegexRemove(new Regex(" dot ?com", RegexOptions.IgnoreCase), normalizedQuery);
            string feelingLuckyUrl = "http://www.google.com/search?btnI=1&q=" + WebUtility.UrlEncode(normalizedQuery);
            services.Logger.Log("Navigating directly to website by query " + normalizedQuery);
            return new PluginResult(Result.Success)
            {
                ResponseUrl = feelingLuckyUrl
            };
        }

        private string GetDefaultCurrencyForLocale(LanguageCode locale)
        {
            CountryCode localeCountryCode = locale.Region;
            if (localeCountryCode != null)
            {
                if (CountryCode.UNITED_STATES_OF_AMERICA.Equals(localeCountryCode))
                {
                    return "USD";
                }
                else if (CountryCode.JAPAN.Equals(localeCountryCode))
                {
                    return "JPY";
                }
                else if (CountryCode.CHINA.Equals(localeCountryCode))
                {
                    return "CHY";
                }
            }

            return "USD";
        }

        private bool IsCurrencyCode(string code)
        {
            return string.Equals("USD", code) ||
                string.Equals("EUR", code) ||
                string.Equals("CHY", code) ||
                string.Equals("JPY", code);
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            MemoryStream pngStream = new MemoryStream();
            if (pluginDataDirectory != null && pluginDataManager != null)
            {
                VirtualPath iconFile = pluginDataDirectory + "\\icon.png";
                if (pluginDataManager.Exists(iconFile))
                {
                    using (Stream iconStream = pluginDataManager.OpenStream(iconFile, FileOpenMode.Open, FileAccessMode.Read))
                    {
                        iconStream.CopyTo(pngStream);
                    }
                }
            }

            PluginInformation returnVal = new PluginInformation()
            {
                InternalName = "Bing",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
                IconPngData = new ArraySegment<byte>(pngStream.ToArray())
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Bing",
                ShortDescription = "Search for facts and entites on Bing",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("who was gandhi");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("kubo and the two strings");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("George Washington");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("how many cups are in a liter");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("what movies are playing now");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("find italian restaurants");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("restaurants in Issaquah");

            return returnVal;
        }
    }
}
