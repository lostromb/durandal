
namespace Durandal.Answers.WinampAnswer
{
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Config;
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

    public class WinampAnswer : DurandalPlugin
    {
        //private WinampLibrary _musicLibrary;
        
        public WinampAnswer()
            : base("winamp")
        {
        }

        public override async Task OnLoad(IPluginServices services)
        {
            IConfiguration config = services.PluginConfiguration;
            if (!config.ContainsKey("libraryRootDirectory"))
            {
                config.Set("libraryRootDirectory", "./");
            }

            // Get the local NL tools
            // oh man this will not work well for other languages will it
            NLPTools languageTools = null;
            if (languageTools == null || languageTools.Pronouncer == null)
            {
                services.Logger.Log("NLP tools are not available for the given locale en-US", LogLevel.Err);
            }
            else
            {
                //this._musicLibrary = new WinampLibrary(config.GetString("libraryRootDirectory", "./"), services.Logger, languageTools.EditDistance);
            }

            await DurandalTaskExtensions.NoOpTask;
        }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            tree.AddStartState("play_media", PlayMedia);
            tree.AddStartState("pause_media", PauseMedia);
            tree.AddStartState("stop_media", StopMedia);
            tree.AddStartState("next_track", NextTrack);
            tree.AddStartState("enqueue_songs", EnqueueSongs);
            return tree;
        }

        //private void EnqueueInWinamp(IList<WinampLibraryEntry> items, Winamp handle)
        //{
        //    List<string> fileNames = new List<string>();
        //    foreach (WinampLibraryEntry item in items)
        //    {
        //        string path = item.FilePath;
        //        if (File.Exists(path))
        //        {
        //            fileNames.Add(path);
        //        }
        //    }
        //    if (fileNames.Count > 0)
        //    {
        //        handle.Stop();
        //        handle.ClearPlaylist();
        //        handle.AppendToPlaylist(fileNames.ToArray());
        //        handle.Play();
        //    }
        //}

        public async Task<PluginResult> PlayMedia(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Attempt to hook into a running Winamp instance
            //Winamp handle = Winamp.GetSingleton(false, services.Logger);
            //if (handle == null)
            //    return new PluginResult(Result.Skip);
            //handle.Play();
            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> PauseMedia(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            //Winamp handle = Winamp.GetSingleton(false, services.Logger);
            //if (handle == null)
            //    return new PluginResult(Result.Skip);
            //handle.Pause();
            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> StopMedia(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            //Winamp handle = Winamp.GetSingleton(false, services.Logger);
            //if (handle == null)
            //    return new PluginResult(Result.Skip);
            //handle.Stop();
            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> NextTrack(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            //Winamp handle = Winamp.GetSingleton(false, services.Logger);
            //if (handle == null)
            //    return new PluginResult(Result.Skip);
            //handle.Next();
            return new PluginResult(Result.Success);
        }

        public async Task<PluginResult> EnqueueSongs(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            //string query;
            //IList<WinampLibraryEntry> itemsToEnqueue;
            //Winamp handle = null;

            //NLPTools nlTools = services.GetLanguageToolsForLocale(queryWithContext.ClientContext.Locale);

            //query = DialogHelpers.TryGetSlotValue(queryWithContext.Result, "artist");
            //if (!string.IsNullOrWhiteSpace(query))
            //{
            //    itemsToEnqueue = this._musicLibrary.SearchForArtist(query, nlTools.EditDistance);
            //    if (itemsToEnqueue.Count > 0)
            //    {
            //        handle = Winamp.GetSingleton(true, services.Logger);
            //        if (handle == null)
            //            return new PluginResult(Result.Skip);
            //        this.EnqueueInWinamp(itemsToEnqueue, handle);
            //        return new PluginResult(Result.Success);
            //    }
            //    else
            //    {
            //        services.Logger.Log("I couldn't find the artist " + query);
            //    }
            //}

            //query = DialogHelpers.TryGetSlotValue(queryWithContext.Result, "albumtitle");
            //if (!string.IsNullOrWhiteSpace(query))
            //{
            //    itemsToEnqueue = this._musicLibrary.SearchForAlbum(query, nlTools.EditDistance);
            //    if (itemsToEnqueue.Count > 0)
            //    {
            //        handle = Winamp.GetSingleton(true, services.Logger);
            //        if (handle == null)
            //            return new PluginResult(Result.Skip);
            //        this.EnqueueInWinamp(itemsToEnqueue, handle);
            //        return new PluginResult(Result.Success);
            //    }
            //    else
            //    {
            //        services.Logger.Log("I couldn't find the album " + query);
            //    }
            //}

            //query = DialogHelpers.TryGetSlotValue(queryWithContext.Result, "songtitle");
            //if (!string.IsNullOrWhiteSpace(query))
            //{
            //    itemsToEnqueue = this._musicLibrary.SearchForSong(query, nlTools.EditDistance);
            //    if (itemsToEnqueue.Count > 0)
            //    {
            //        handle = Winamp.GetSingleton(true, services.Logger);
            //        if (handle == null)
            //            return new PluginResult(Result.Skip);
            //        this.EnqueueInWinamp(itemsToEnqueue, handle);
            //        return new PluginResult(Result.Success);
            //    }
            //    else
            //    {
            //        services.Logger.Log("I couldn't find the song " + query);
            //    }
            //}

            return new PluginResult(Result.Skip);
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
                InternalName = "Winamp",
                MajorVersion = 1,
                MinorVersion = 0,
                IconPngData = new ArraySegment<byte>(pngStream.ToArray())
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Winamp",
                Creator = "Logan Stromberg",
                ShortDescription = "Control your music library"
            });

            return returnVal;
        }
    }
}
