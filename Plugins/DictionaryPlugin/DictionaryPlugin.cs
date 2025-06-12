
namespace Durandal.Plugins
{
    using Common.File;
    using Common.Net;
    using Common.Net.Http;
    using CommonViews;
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class DictionaryPlugin : DurandalPlugin
    {
        public DictionaryPlugin()
            : base("dictionary")
        {
        }

        private IHttpClient _httpClient;

        // TODO: Add "tell me more" multiturn capability

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode defineNode = returnVal.CreateNode(GetDefinition);
            IConversationNode spellNode = returnVal.CreateNode(GetSpelling);
            IConversationNode repeatDefinitionNode = returnVal.CreateNode(RepeatDefinition);
            
            returnVal.AddStartState("define", defineNode);
            defineNode.CreateCommonEdge("repeat", repeatDefinitionNode);

            returnVal.AddStartState("spell", spellNode);

            return returnVal;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            _httpClient = services.HttpClientFactory.CreateHttpClient(new Uri("http://www.onelook.com"));
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
        }

        public async Task<PluginResult> RepeatDefinition(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            string lastDefinition;

            if (!services.SessionStore.TryGetString("LastDefinition", out lastDefinition))
            {
                return new PluginResult(Result.Skip);
            }

            return new PluginResult(Result.Success)
                {
                    ResponseText = lastDefinition,
                    ResponseSsml = lastDefinition,
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                    ResponseHtml = new MessageView()
                    {
                        Content = lastDefinition,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                };
        }

        public async Task<PluginResult> GetSpelling(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            string word = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "word");

            if (string.IsNullOrWhiteSpace(word))
            {
                services.Logger.Log("no word to spell", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }

            string spelledOut = string.Join(" ", word.ToUpper().ToCharArray());

            return new PluginResult(Result.Success)
                {
                    ResponseText = spelledOut.ToString(),
                    ResponseSsml = spelledOut.ToString(),
                    ResponseHtml = new MessageView()
                    {
                        Content = spelledOut.ToString(),
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                };
        }

        public async Task<PluginResult> GetDefinition(QueryWithContext queryWithContext, IPluginServices services)
        {
            string query = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "word");

            // Trim all spaces
            query = query.Replace(" ", "");

            if (string.IsNullOrWhiteSpace(query))
            {
                services.Logger.Log("no query extracted", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }

            string lookupUrl = "/?w=" + WebUtility.UrlEncode(query) + "&xml=1";
            using (HttpRequest req = HttpRequest.CreateOutgoing(lookupUrl))
            using (NetworkResponseInstrumented<HttpResponse> result = await _httpClient.SendInstrumentedRequestAsync(
                req,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton,
                services.Logger).ConfigureAwait(false))
            {
                try
                {
                    if (result == null || !result.Success || result.Response == null || result.Response.ResponseCode != 200)
                    {
                        return null;
                    }

                    string xmlResult = await result.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(xmlResult))
                    {
                        return new PluginResult(Result.Failure);
                    }

                    string definition = this.ReadXMLResult(xmlResult);

                    if (string.IsNullOrWhiteSpace(definition))
                    {
                        return new PluginResult(Result.Failure);
                    }

                    definition = WebUtility.HtmlDecode(WebUtility.HtmlDecode(definition));
                    // Strip all HTML entities from the result
                    definition = StringUtils.RegexRemove(new Regex("<(.+?)[ >][\\w\\W]+?</ ?\\1.*?>"), definition);
                    definition = query + ": " + definition.Trim();


                    services.SessionStore.Put("LastDefinition", definition);

                    return new PluginResult(Result.Success)
                    {
                        ResponseText = definition,
                        ResponseSsml = definition,
                        MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                        ResponseHtml = new MessageView()
                        {
                            Content = definition,
                            ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                        }.Render()
                    };
                }
                finally
                {
                    if (result != null)
                    {
                        await result.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
            }
        }

        private string ReadXMLResult(string xml)
        {
            Regex parser = new Regex("<OLQuickDef>([\\w\\W]+?)<");
            Match result = parser.Match(xml);
            if (!result.Success)
                return null;

            return result.Groups[1].Value;
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            using (MemoryStream pngStream = new MemoryStream())
            {
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
                    InternalName = "Dictionary",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Dictionary & Thesaurus",
                    ShortDescription = "Sausage",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("What does coruscate mean?");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Define egregious");

                return returnVal;
            }
        }
    }
}
