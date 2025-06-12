
namespace Durandal.Plugins.Youtube
{
    using Durandal.API;
        using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.CommonViews;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    public class YoutubePlugin : DurandalPlugin
    {
        public YoutubePlugin()
            : base("youtube")
        {
        }

        public override async Task OnLoad(IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            Google.Apis.YouTube.v3.YouTubeService service = new Google.Apis.YouTube.v3.YouTubeService();
            service.GetHashCode();
        }

        /// <summary>
        /// FIXME this needs to go in user context
        /// </summary>
        private IList<YoutubeVideo> lastDisplayedVideos = new List<YoutubeVideo>();

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode findVideoNode = returnVal.CreateNode(this.FindVideo);
            IConversationNode selectVideoNode = returnVal.CreateNode(this.SelectVideo);
            IConversationNode downloadVideoNode = returnVal.CreateNode(this.DownloadVideo);

            findVideoNode.CreateNormalEdge("select_video", selectVideoNode);
            findVideoNode.CreateNormalEdge("download_video", downloadVideoNode);

            returnVal.AddStartState("find_video", findVideoNode);
            return returnVal;
        }

        public async Task<PluginResult> FindVideo(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Make sure the client can handle HTML
            if (!queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayBasicHtml) &&
                !queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayHtml5))
            {
                PluginResult returnVal = new PluginResult(Result.Success);
                return await services.LanguageGenerator.GetPattern("NoDisplayMessage", queryWithContext.ClientContext, services.Logger)
                    .ApplyToDialogResult(returnVal);
            }
            
            // Extract the query from the text
            string query = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "query");

            if (string.IsNullOrWhiteSpace(query))
            {
                services.Logger.Log("no query extracted", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }

            services.Logger.Log("Searching Youtube for " + query);
            this.lastDisplayedVideos.Clear();

            string apiKey = "AIzaSyA24qScNi2Ym0W0xVGu3M5oh5ShP09UY34";

            string url = string.Format("https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&key={0}&q={1}", apiKey, WebUtility.UrlEncode(query));
            
            IHttpClient httpClient = services.HttpClientFactory.CreateHttpClient(new Uri("https://www.googleapis.com"));
            using (HttpRequest request = HttpRequest.CreateOutgoing(url))
            {
                request.RequestHeaders.Add("Referer", "http://www.durand.al/");
                using (NetworkResponseInstrumented<HttpResponse> httpResult = await httpClient.SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, services.Logger))
                {
                    try
                    {
                        string youtubeResponse = await httpResult.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(youtubeResponse))
                        {
                            return new PluginResult(Result.Failure)
                            {
                                ErrorMessage = "The Youtube API did not respond"
                            };
                        }
                        else
                        {
                            JsonSerializer ser = JsonSerializer.Create(new JsonSerializerSettings());
                            JObject responseBlob = ser.Deserialize(new JsonTextReader(new StringReader(youtubeResponse))) as JObject;

                            foreach (JToken videoResult in responseBlob["items"])
                            {
                                YoutubeVideo video = new YoutubeVideo(videoResult);
                                this.lastDisplayedVideos.Add(video);
                            }

                            string returnedHtml = ResultsRenderer.GenerateHtml(
                                this.lastDisplayedVideos,
                                services.PluginViewDirectory,
                                queryWithContext.ClientContext);

                            PluginResult returnVal = new PluginResult(Result.Success)
                            {
                                MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                            };

                            if (returnedHtml != null)
                            {
                                returnVal.ResponseHtml = returnedHtml;
                            }

                            return await services.LanguageGenerator.GetPattern("HereAreVideoResults", queryWithContext.ClientContext, services.Logger)
                                .Sub("query", query)
                                .ApplyToDialogResult(returnVal);
                        }
                    }
                    finally
                    {
                        if (httpResult != null)
                        {
                            await httpResult.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        public async Task<PluginResult> SelectVideo(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Are there any videos on screen?
            if (this.lastDisplayedVideos.Count == 0)
            {
                return new PluginResult(Result.Skip);
            }

            SlotValue selectionSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "selection");
            if (selectionSlot == null)
            {
                return new PluginResult(Result.Skip);
            }
            Ordinal selectionIndex = selectionSlot.GetOrdinal();
            if (selectionIndex == null)
            {
                return new PluginResult(Result.Failure);
            }

            YoutubeVideo selectedVideo = this.SelectVideo(selectionIndex);
            if (selectedVideo == null)
            {
                ILGPattern pattern = services.LanguageGenerator.GetPattern("NoSuchVideo", queryWithContext.ClientContext, services.Logger);
                return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        ResponseHtml = new MessageView()
                        {
                            Content = (await pattern.Render()).Text,
                            ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                        }.Render()
                    });
            }

            return new PluginResult(Result.Success)
            {
                ResponseUrl = selectedVideo.EmbedUrl
            };
        }

        public async Task<PluginResult> DownloadVideo(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Are there any videos on screen?
            if (this.lastDisplayedVideos.Count == 0)
            {
                return new PluginResult(Result.Skip);
            }

            SlotValue selectionSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "selection");
            if (selectionSlot == null)
            {
                return new PluginResult(Result.Skip);
            }
            Ordinal selectionIndex = selectionSlot.GetOrdinal();
            if (selectionIndex == null)
            {
                return new PluginResult(Result.Failure);
            }

            YoutubeVideo selectedVideo = this.SelectVideo(selectionIndex);

            if (selectedVideo == null)
            {
                ILGPattern pattern = services.LanguageGenerator.GetPattern("NoSuchVideo", queryWithContext.ClientContext, services.Logger);
                return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await pattern.Render()).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                });
            }

            List<YoutubeLink> links = selectedVideo.GetVideoLinks();

            YoutubeLink downloadLink = SelectBestQualityDownloadLink(links);

            if (downloadLink == null)
            {
                ILGPattern pattern = services.LanguageGenerator.GetPattern("DownloadLinkError", queryWithContext.ClientContext, services.Logger);
                return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        ResponseHtml = new MessageView()
                        {
                            Content = (await pattern.Render()).Text,
                            ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                        }.Render()
                    });
            }

            new Thread(() =>
                {
                    WebClient downloadClient = new WebClient();
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string cleanFileName = StringUtils.SanitizeFileName(selectedVideo.Name + ".mp4");
                    using (Stream readSocket = downloadClient.OpenRead(downloadLink.Url))
                    {
                        using (Stream writeSocket = new FileStream(
                            desktopPath + "\\" + cleanFileName, FileMode.CreateNew))
                        {
                            SimplePipe pipe = new SimplePipe(readSocket, writeSocket);
                            pipe.Drain();
                            writeSocket.Close();
                        }
                        readSocket.Close();
                    }
                }).Start();

            ILGPattern lg = services.LanguageGenerator.GetPattern("DownloadSuccess", queryWithContext.ClientContext, services.Logger);

            return await lg.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await lg.Render()).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                });
        }

        private YoutubeVideo SelectVideo(Ordinal ordinal)
        {
            switch (ordinal.Type)
            {
                case OrdinalType.Number:
                    if (this.lastDisplayedVideos.Count > ordinal.NumericValue)
                    {
                        return this.lastDisplayedVideos[ordinal.NumericValue - 1];
                    }
                    return null;

                case OrdinalType.Top:
                case OrdinalType.First:
                    return this.lastDisplayedVideos[0];

                case OrdinalType.Bottom:
                case OrdinalType.Last:
                    return this.lastDisplayedVideos[this.lastDisplayedVideos.Count - 1];

                default:
                    return null;
            }
        }

        public static YoutubeLink SelectBestQualityDownloadLink(IEnumerable<YoutubeLink> links)
        {
            YoutubeLink bestLink = null;
            int bestQuality = 0;
            foreach (YoutubeLink link in links)
            {
                if (link.Format == VideoFormat.Mp4 && link.VerticalResolution > bestQuality)
                {
                    bestLink = link;
                    bestQuality = link.VerticalResolution;
                }
            }
            return bestLink;
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
                InternalName = "Youtube",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
                IconPngData = new ArraySegment<byte>(pngStream.ToArray())
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "YouTube",
                ShortDescription = "Plays and downloads Youtube videos",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Find music videos by Michael Jackson");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Search Youtube for Sam Tsui");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Search for surfing videos");

            return returnVal;
        }
    }
}
