//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Maps
{
    public class MapView
    {
        private StringWriter Output;
        public string Title {get; set;}
        public string Content {get; set;}
        public string Image {get; set;}
        public IDictionary<string,string> ClientContextData {get; set;}
        public MapView()
        {
            ClientContextData = null;
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
            Output.Write("<!DOCTYPE HTML>\r\n<html>\r\n<head>\r\n    <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n    <meta name=\"viewport\" content=\"width=device-width, minimum-scale=1.0, initial-scale=1.0, maximum-scale=1.0, user-scalable=no\"/>\r\n    <link href=\"/views/common/pure-base-min.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    <link href=\"/views/common/flexbox.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    <link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    ");
    #line default
            try
            {
    #line 9 "MapView"
        Output.Write(new Durandal.CommonViews.DynamicTheme() { ClientContextData = ClientContextData }.Render());
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${new Durandal.CommonViews.DynamicTheme() { ClientContextData = ClientContextData }.Render()}");
            }
    #line hidden
            Output.Write("\r\n    <title>");
    #line default
            try
            {
    #line 10 "MapView"
               Output.Write(Title);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Title}");
            }
    #line hidden
            Output.Write("</title>\r\n</head>\r\n<body class=\"globalBgColor globalFontStyle globalFontColor\">\r\n    <div class=\"flex flexAppContainer\">\r\n        <div class=\"flex flexBoxColumn flex-stretch screenPadding\">\r\n            <div class=\"flex1\"></div>\r\n            <div align=\"center\" style=\"text-align: center\"><span class=\"flex1 size2 globalFontColorAccent\">");
    #line default
            try
            {
    #line 16 "MapView"
                                                                                                               Output.Write(Content);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Content}");
            }
    #line hidden
            Output.Write("</span></div>\r\n            <div align=\"center\"><img class=\"pure-img screenPadding\" src=\"");
    #line default
            try
            {
    #line 17 "MapView"
                                                                             Output.Write(Image);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Image}");
            }
    #line hidden
            Output.Write("\"></div>\r\n            <div class=\"flex1\"></div>\r\n        </div>\r\n    </div>\r\n</body>\r\n</html>\r\n");
    #line default
        }
    }
}
