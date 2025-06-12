
namespace Durandal.Plugins.Fortune
{
    using Durandal.API;
        using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.MathExt;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Tasks;
    using Durandal.CommonViews;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class FortunePlugin : DurandalPlugin
    {
        private static readonly Regex FORTUNE_FILENAME_MATCHER = new Regex("fortunes\\.(\\w\\w-\\w\\w)\\.txt", RegexOptions.IgnoreCase);

        private IRandom _random;

        // localized fortune lists
        private IDictionary<LanguageCode, IList<string>> _fortunes = new Dictionary<LanguageCode, IList<string>>();

        public FortunePlugin() : this(new FastRandom()) { }

        public FortunePlugin(IRandom random) : base("fortune")
        {
            _random = random;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            foreach (VirtualPath fortuneFile in await services.FileSystem.ListFilesAsync(services.PluginDataDirectory).ConfigureAwait(false))
            {
                Match filenameMatch = FORTUNE_FILENAME_MATCHER.Match(fortuneFile.Name);
                if (filenameMatch != null && filenameMatch.Success)
                {
                    LanguageCode locale = LanguageCode.Parse(filenameMatch.Groups[1].Value);
                    List<string> fileContents = new List<string>(await services.FileSystem.ReadLinesAsync(fortuneFile).ConfigureAwait(false));
                    _fortunes[locale] = fileContents;
                    services.Logger.Log("Loaded " + fileContents + " fortunes for locale " + locale);
                }
            }
        }

        public override Task OnUnload(IPluginServices services)
        {
            _fortunes.Clear();
            return DurandalTaskExtensions.NoOpTask;
        }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode tellFortuneNode = tree.CreateNode(TellFortune);
            tellFortuneNode.CreateNormalEdge("tell_another", tellFortuneNode);
            tree.AddStartState("tell_fortune", tellFortuneNode);
            return tree;
        }

        public async Task<PluginResult> TellFortune(QueryWithContext queryWithContext, IPluginServices services)
        {
            // See if we have any fortunes for this locale
            if (!_fortunes.ContainsKey(queryWithContext.ClientContext.Locale))
            {
                // None found
                ILGPattern lgResponse = services.LanguageGenerator.GetPattern("NoFortune", queryWithContext.ClientContext, services.Logger, false, _random.NextInt());
                return await lgResponse.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await lgResponse.Render().ConfigureAwait(false)).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
            }
            
            IList<string> allFortunes = _fortunes[queryWithContext.ClientContext.Locale];
            string fortune = allFortunes[_random.NextInt(0, allFortunes.Count)];

            return new PluginResult(Result.Success)
            {
                ResponseText = fortune,
                ResponseSsml = fortune,
                ResponseHtml = new MessageView()
                {
                    Content = fortune,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render(),
                MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                TriggerKeywords = new List<TriggerKeyword>()
                {
                    new TriggerKeyword()
                    {
                        ExpireTimeSeconds = 30,
                        TriggerPhrase = "another one",
                        AllowBargeIn = false
                    }
                }
            };
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
                    InternalName = "Fortune",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Fortune",
                    ShortDescription = "Simple and fun fortunetelling",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Tell me my fortune");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Read my fortune");

                return returnVal;
            }
        }
    }
}
