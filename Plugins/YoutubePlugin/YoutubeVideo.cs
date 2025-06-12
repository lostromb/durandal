namespace Durandal.Plugins.Youtube
{
    using Newtonsoft.Json.Linq;

    using System.Collections.Generic;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Web;

    using Durandal.Common.Utils;

    public class YoutubeVideo
    {
        public string Name;
        public string Description;
        public string Id;
        public string ThumbUrl;

        public YoutubeVideo(string id, string name = "", string desc = "", string thumbUrl = "")
        {
            this.Name = name;
            this.Description = desc;
            this.Id = id;
            this.ThumbUrl = thumbUrl;
        }

        public YoutubeVideo(JToken videoResult)
        {
            this.Name = videoResult["snippet"]["title"].Value<string>();
            this.Description = videoResult["snippet"]["description"].Value<string>();
            this.Id = videoResult["id"]["videoId"].Value<string>();
            this.ThumbUrl = videoResult["snippet"]["thumbnails"]["medium"]["url"].Value<string>();
        }

        public static YoutubeVideo ParseYoutubeVideo(string videoId)
        {
            // Query the page to find out what's up
            return null;
        }

        public string PageUrl
        {
            get
            {
                return "http://www.youtube.com/watch?v=" + this.Id;
            }
        }

        public string EmbedUrl
        {
            get
            {
                return "http://www.youtube.com/embed/" + this.Id + "?autoplay=1";
            }
        }

        public List<YoutubeLink> GetVideoLinks()
        {
            List<YoutubeLink> returnVal = new List<YoutubeLink>();

            returnVal.AddRange(GetVideoLinksFromAPI());
            returnVal.AddRange(GetVideoLinksFromPageScript());

            return returnVal;
        }

        public YoutubeLink GetIdealVideoLink(VideoFormat format, int maximumVerticalResolution)
        {
            YoutubeLink returnVal = null;
            
            List<YoutubeLink> allLinks = GetVideoLinks();
            if (allLinks == null)
            {
                return returnVal;
            }

            foreach (YoutubeLink l in allLinks)
            {
                if (l.Format == format && l.VerticalResolution <= maximumVerticalResolution &&
                    (returnVal == null || l.VerticalResolution > returnVal.VerticalResolution))
                {
                    returnVal = l;
                }
            }

            return returnVal;
        }

        private List<YoutubeLink> GetVideoLinksFromAPI()
        {
            List<YoutubeLink> returnVal = new List<YoutubeLink>();
            
            WebClient client = new WebClient();
            string allInfo = client.DownloadString("http://www.youtube.com/get_video_info?video_id=" + this.Id);
            allInfo = WebUtility.UrlDecode(allInfo);
            Regex formatStreamRegex = new Regex("url_encoded_fmt_stream_map=(.+)");
            string videoInfoBlock = formatStreamRegex.Match(allInfo).Groups[1].Value;
            Regex videoInfoRegex = new Regex("(url=.+?)(?:,|show_content_thumbnail)");
            MatchCollection vidMatches = videoInfoRegex.Matches(videoInfoBlock);
            char[] equals = { '=' };
            
            foreach (Match m in vidMatches)
            {
                string[] lines = m.Groups[1].Value.Split('&');
                Dictionary<string, string> dict = new Dictionary<string, string>();
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string[] parts = line.Split(equals, 2);
                        dict[parts[0]] = WebUtility.UrlDecode(parts[1]);
                    }
                }
                YoutubeLink link = new YoutubeLink(dict);
                if (!string.IsNullOrWhiteSpace(link.Url))
                {
                    returnVal.Add(link);
                }
            }

            return returnVal;
        }

        private List<YoutubeLink> GetVideoLinksFromPageScript()
        {
            List<YoutubeLink> returnVal = new List<YoutubeLink>();
            WebClient client = new WebClient();
            string playerPage = client.DownloadString("http://www.youtube.com/watch?v=" + this.Id);
            
            // While we're here, try and parse the video title and info
            if (string.IsNullOrEmpty(this.Name))
            {
                this.Name = StringUtils.RegexRip(new Regex("<title>(.+?)</title>"), playerPage, 1);
                if (!string.IsNullOrEmpty(this.Name))
                {
                    this.Name = WebUtility.HtmlDecode(this.Name);
                    if (this.Name.EndsWith(" - YouTube"))
                    {
                        this.Name = this.Name.Substring(0, this.Name.IndexOf(" - YouTube"));
                    }
                }
            }

            string videoMetaBlock = StringUtils.RegexRip(new Regex("url_encoded_fmt_stream_map([\\w\\W]+?)</script>"), playerPage, 1);

            if (string.IsNullOrEmpty(videoMetaBlock))
            {
                return returnVal;
            }

            Regex type1Matcher = new Regex("(?:quality_label|quality)=(.+?)\\\\u0026.*?type=(.+?)\\\\u0026.*?url=(.+?)\\\\u0026");
            MatchCollection vidMatches = type1Matcher.Matches(videoMetaBlock);

            foreach (Match m in vidMatches)
            {
                string quality = m.Groups[1].Value;
                string type = WebUtility.UrlDecode(m.Groups[2].Value);
                string url = WebUtility.UrlDecode(m.Groups[3].Value);
                YoutubeLink link = new YoutubeLink(type, quality, url);
                if (!string.IsNullOrWhiteSpace(link.Url))
                {
                    returnVal.Add(link);
                }
            }

            return returnVal;
        }
    }
}
