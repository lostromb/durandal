
namespace Durandal.Plugins.Dolphin
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
    using System.IO;
    using System.Threading.Tasks;

    public class DolphinAnswer : DurandalPlugin
    {
        public DolphinAnswer()
            : base("dolphin")
        {
        }

        public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            // Extract the game name from the text
            string gameName = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "game");

            if (string.IsNullOrWhiteSpace(gameName))
            {
                services.Logger.Log("No game title found", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }

            return new PluginResult(Result.Success)
                {
                    ResponseText = "Launching " + gameName,
                    ResponseSsml = "Launching " + gameName
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
                    InternalName = "Dolphin",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Dolphin Launcher",
                    ShortDescription = "It's Mario time"
                });

                return returnVal;
            }
        }
    }
}
