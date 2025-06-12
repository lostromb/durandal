
namespace Durandal.Plugins
{
    using System;

    using Durandal.API;
    using Durandal.Common.Utils;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;

    using System.IO;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Durandal.Common.IO;
    using Durandal.Common.Tasks;
    using Durandal.Common.File;

    public class FacebookPlugin : DurandalPlugin
    {
        public FacebookPlugin()
            : base("facebook")
        {
        }

        public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            // just open the facebook homepage
            return new PluginResult(Result.Success)
            {
                ResponseSsml = "Checking your face book",
                ResponseText = "Checking your Facebook",
                ResponseUrl = "http://www.facebook.com"
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
                    InternalName = "facebook",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Facebook",
                    ShortDescription = "Check yo self before you wreck yo self",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Do I have any facebook messages?");

                return returnVal;
            }
        }
    }
}
