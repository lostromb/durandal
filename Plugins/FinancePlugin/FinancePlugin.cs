
namespace Durandal.Plugins
{
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class FinancePlugin : DurandalPlugin
    {
        public FinancePlugin() : base("finance") { }

        public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            switch (queryWithContext.Understanding.Intent)
            {
                case "show_overview":
                    return new PluginResult(Result.Success)
                    {
                        ResponseSsml = "Here is the current market",
                        ResponseUrl = "http://finance.yahoo.com/market-overview/"
                    };
                case "show_stocks":
                    return new PluginResult(Result.Success)
                    {
                        ResponseSsml = "Here is the stock market",
                        ResponseUrl = "http://finance.yahoo.com/stock-center/"
                    };
                case "show_bonds":
                    return new PluginResult(Result.Success)
                    {
                        ResponseSsml = "Here is the bond market",
                        ResponseUrl = "http://finance.yahoo.com/bonds"
                    };
                default:
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Unknown intent"
                    };
            }
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
                    InternalName = "Finance",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Yahoo! Finance",
                    ShortDescription = "Checks your stocks and things",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("How is the stock market?");

                return returnVal;
            }
        }
    }
}
