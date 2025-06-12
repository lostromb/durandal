
namespace Durandal.Answers.VLCAnswer
{
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class VLCAnswer : DurandalPlugin
    {
        public VLCAnswer() : base("vlcmedia")
        {
        }

        public override async Task OnLoad(IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            string mediaDirectory = services.PluginConfiguration.GetString("movieLibraryPath", string.Empty);
            if (!string.IsNullOrEmpty(mediaDirectory))
            {
                DirectoryInfo mediaLibrary = new DirectoryInfo(mediaDirectory);
                services.Logger.Log("VLC plugin is loading media library from " + mediaLibrary.FullName);

                // Get the local NL tools
                NLPTools languageTools = null;
                if (languageTools == null || languageTools.Pronouncer == null)
                {
                    services.Logger.Log("NLP tools are not available for the given locale en-US", LogLevel.Err);
                }
                else
                {
                    //this.library = new MovieLibrary(mediaLibrary, services.PluginConfiguration, services.Logger, languageTools.EditDistance);
                }
            }
        }

        public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Skip);
            //if (this.library == null)
            //{
            //    return new PluginResult(Result.Skip);
            //}

            //NLPTools languageTools = services.GetLanguageToolsForLocale("en-US");
            //if (languageTools == null || languageTools.Pronouncer == null)
            //{
            //    return new PluginResult(Result.Failure)
            //    {
            //        ErrorMessage = "NLP tools are not available for the given locale en-US"
            //    };
            //}

            //switch (queryWithContext.Result.Intent)
            //{
            //case "play_movie":
            //    {
            //        string movieTitle = DialogHelpers.TryGetSlotValue(queryWithContext.Result, "media_title");
            //        if (string.IsNullOrWhiteSpace(movieTitle))
            //        {
            //            // No movie title extracted - assume it's a false trigger
            //            return new PluginResult(Result.Skip);
            //        }

            //        IList<MovieFileEntry> results = this.library.FindMatchingMovies(movieTitle, string.Empty, string.Empty, languageTools.EditDistance);

            //        if (results.Count == 0)
            //        {
            //            string text = "I'm sorry. I couldn't find the movie " + movieTitle;
            //            return new PluginResult(Result.Success)
            //                {
            //                    ResponseText = text,
            //                    ResponseSsml = text
            //                };
            //        }

            //        using (VLC vlcInterface = VLC.TryConnectOrLaunchNew(services.PluginConfiguration.GetString("vlcPath"), 9999, services.Logger.Clone("VLC Bridge")))
            //        {
            //            if (vlcInterface != null)
            //            {
            //                vlcInterface.OpenAndPlayFile(results[0].FilePath);
            //                vlcInterface.SetFullscreen(true);
            //                vlcInterface.Disconnect();
            //            }
            //        }
            //        return new PluginResult(Result.Success);
            //    }
            //case "play_tv_episode":
            //    {
            //        string showTitle = DialogHelpers.TryGetSlotValue(queryWithContext.Result, "media_title");
            //        if (string.IsNullOrWhiteSpace(showTitle))
            //        {
            //            // No movie title extracted - assume it's a false trigger
            //            return new PluginResult(Result.Skip);
            //        }

            //        string season = DialogHelpers.TryGetSlotValue(queryWithContext.Result, "season");
            //        string episode = DialogHelpers.TryGetSlotValue(queryWithContext.Result, "episode");

            //        IList<MovieFileEntry> results = this.library.FindMatchingMovies(showTitle, season, episode, languageTools.EditDistance);

            //        if (results.Count == 0)
            //        {
            //            string text = "I'm sorry. I couldn't find the video " + showTitle;
            //            return new PluginResult(Result.Success)
            //                {
            //                    ResponseText = text,
            //                    ResponseSsml = text
            //                };
            //        }

            //        using (VLC vlcInterface = VLC.TryConnectOrLaunchNew(services.PluginConfiguration.GetString("vlcPath"), 9999, services.Logger.Clone("VLC Bridge")))
            //        {
            //            if (vlcInterface != null)
            //            {
            //                vlcInterface.OpenAndPlayFile(results[0].FilePath);
            //                vlcInterface.SetFullscreen(true);
            //                vlcInterface.Disconnect();
            //            }
            //        }
            //        return new PluginResult(Result.Success);
            //    }
            //default:
            //    {
            //        // Unsupported intent
            //        return new PluginResult(Result.Failure);
            //    }
            //}
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
                InternalName = "VLC Media",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
                IconPngData = new ArraySegment<byte>(pngStream.ToArray())
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "VLC Media",
                ShortDescription = "Plays movies and videos from your local library"
            });

            return returnVal;
        }
    }
}
