namespace Durandal.Plugins.Youtube
{
    using System.Collections.Generic;

    public class YoutubeLink
    {
        public VideoFormat Format;
        public string Quality;
        public string Url;

        public YoutubeLink(string formatString, string qualityString, string url)
        {
            this.Quality = qualityString;
            this.Format = ParseFormatString(formatString);
            this.Url = url;
        }

        public YoutubeLink(IDictionary<string, string> parameters)
        {
            if (parameters.ContainsKey("type") &&
                parameters.ContainsKey("url") &&
                parameters.ContainsKey("quality"))
            {
                this.Format = ParseFormatString(parameters["type"]);
                this.Url = parameters["url"];
                this.Quality = parameters["quality"];
            }
        }

        private static VideoFormat ParseFormatString(string fmt)
        {
            if (fmt.Contains("/webm"))
                return VideoFormat.Webm;
            else
                return VideoFormat.Mp4;
        }

        public int VerticalResolution
        {
            get
            {
                if (this.Quality.Contains("1080"))
                    return 1080;
                else if (this.Quality.Contains("720"))
                    return 720;
                else if (this.Quality.Contains("480") || this.Quality.Equals("medium"))
                    return 480;
                else if (this.Quality.Contains("360") || this.Quality.Equals("small"))
                    return 360;
                else
                    return 240;
            }
        }

        public override string ToString()
        {
            return this.Format.ToString() + " " + this.Quality + " " + this.Url;
        }
    }
}
