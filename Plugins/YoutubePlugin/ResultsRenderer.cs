
namespace Durandal.Plugins.Youtube
{
    using System.Collections.Generic;

    using Durandal.API;

    using Durandal.Common.IO;
    using Common.File;

    public static class ResultsRenderer
    {
        public static string GenerateHtml(IList<YoutubeVideo> results, VirtualPath viewDirectory, ClientContext clientInfo)
        {
            if (clientInfo.GetCapabilities().HasFlag(ClientCapabilities.DisplayHtml5))
            {
                return new YoutubeResultsView { Videos = results }.Render();
            }
            else if (clientInfo.GetCapabilities().HasFlag(ClientCapabilities.DisplayBasicHtml))
            {
                return "<html><body>Todo: Write a youtube results view for html4 clients</body></html>";
            }
            else
            {
                return null;
            }
        }
    }
}
