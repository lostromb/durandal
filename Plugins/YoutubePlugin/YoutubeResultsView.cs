//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Youtube
{
    public class YoutubeResultsView
    {
        private StringWriter Output;
        public IEnumerable<YoutubeVideo> Videos {get; set;}
        public YoutubeResultsView()
        {
        }
        public string Render()
        {
            StringBuilder returnVal = new StringBuilder();
            Output = new StringWriter(returnVal);
            RenderViewLevel0();
            return returnVal.ToString();
        }
        private void RenderViewLevel0()
        {
    #line hidden
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n\r\n<head>\r\n\t<link rel=\"stylesheet\" href=\"http://yui.yahooapis.com/pure/0.5.0/base-min.css\"/>\r\n\t<link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n\t<link href=\"/views/common/flexbox.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n\t<link href=\"/views/youtube/html5/youtube.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n\t<meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\"/>\r\n\t<title>Youtube search results</title>\r\n    ");
    #line default
            try
            {
    #line 11 "YoutubeResultsView"
        Output.Write(new Durandal.CommonViews.DynamicTheme().Render());
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${new Durandal.CommonViews.DynamicTheme().Render()}");
            }
    #line hidden
            Output.Write("\r\n</head>\r\n<body class=\"globalBgColor globalFgColor globalFontStyle globalFontColor\">\r\n\t<div class=\"flexAppContainer flex\">\r\n\t\t<div class=\"flex flexBoxRow\">\r\n\t\t\t<div class=\"flex sidebar globalBgColor\"></div>\r\n\t\t\t<div class=\"flex flex3 globalBgColorLight\">\r\n\t\t\t\t<div class=\"flex flexBoxColumn\">");
    #line default
            {
    #line 18 "YoutubeResultsView"
                                                    foreach(var video in Videos)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                        <div class=\"flex1 resultItem\">\r\n                            <div class=\"flex flexBoxRow flex-center\">\r\n                                <div class=\"flex2\">\r\n                                    <img width=\"100%\" class=\"pure-img\" src=\"");
    #line default
                    try
                    {
    #line 23 "YoutubeResultsView"
                                                                                Output.Write(video.ThumbUrl);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${video.ThumbUrl}");
                    }
    #line hidden
                    Output.Write("\"/>\r\n                                </div>\r\n                                <div class=\"descriptionBox flex3\">\r\n                                    <a href=\"");
    #line default
                    try
                    {
    #line 26 "YoutubeResultsView"
                                                 Output.Write(video.PageUrl);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${video.PageUrl}");
                    }
    #line hidden
                    Output.Write("\" class=\"globalLinkColor\">");
    #line default
                    try
                    {
    #line 26 "YoutubeResultsView"
                                                                                           Output.Write(video.Name);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${video.Name}");
                    }
    #line hidden
                    Output.Write("</a><br/>\r\n                                    ");
    #line default
                    try
                    {
    #line 27 "YoutubeResultsView"
                                        Output.Write(video.Description);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${video.Description}");
                    }
    #line hidden
                    Output.Write("<br/>\r\n                                </div>\r\n                            </div>\r\n                        </div>");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n\t\t\t\t</div>\r\n\t\t\t</div>\r\n\t\t\t<div class=\"flex sidebar globalBgColor\"></div>\r\n\t\t</div>\r\n\t</div>\r\n</body>\r\n</html>\r\n");
    #line default
        }
    }
}
