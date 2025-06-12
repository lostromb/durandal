//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Reflection.Views
{
    public class DisambiguationView
    {
        private StringWriter Output;
        public IEnumerable<DisambiguationRenderItem> Results {get; set;}
        public string Header {get; set;}
        public DisambiguationView()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\"/>\r\n    <meta name=\"viewport\" content=\"width=device-width, minimum-scale=1.0, initial-scale=1.0, maximum-scale=1.0, user-scalable=no\"/>\r\n    <link rel=\"stylesheet\" href=\"/views/common/pure-base-min.css\"/>\r\n    <link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n    <link href=\"/views/common/flexbox.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n    <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\"/>\r\n    <title>Disambiguation</title>\r\n    ");
    #line default
            try
            {
    #line 11 "DisambiguationView"
        Output.Write(new Durandal.CommonViews.DynamicTheme().Render());
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${new Durandal.CommonViews.DynamicTheme().Render()}");
            }
    #line hidden
            Output.Write("\r\n</head>\r\n<body class=\"globalBgColor globalFgColor globalFontStyle\">\r\n    <div class=\"flexAppContainer flex\">\r\n        <div class=\"flex flexBoxColumn\">\r\n            <h1 class=\"globalFontColor\">");
    #line default
            try
            {
    #line 16 "DisambiguationView"
                                            Output.Write(Header);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Header}");
            }
    #line hidden
            Output.Write("</h1>");
    #line default
            {
    #line 16 "DisambiguationView"
                                                          foreach(DisambiguationRenderItem result in Results)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                <div class=\"resultItem\">\r\n                    <a href=\"");
    #line default
                    try
                    {
    #line 19 "DisambiguationView"
                                 Output.Write(result.SelectionUrl);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${result.SelectionUrl}");
                    }
    #line hidden
                    Output.Write("\" class=\"hiddenLink\">\r\n                        <div class=\"flex flexBoxRow flex-center\">\r\n                            <div class=\"flex1\">\r\n                                <img width=\"100%\" class=\"pure-img\" src=\"");
    #line default
                    try
                    {
    #line 22 "DisambiguationView"
                                                                            Output.Write(result.PluginIconUrl);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${result.PluginIconUrl}");
                    }
    #line hidden
                    Output.Write("\"/>\r\n                            </div>\r\n                            <div class=\"descriptionBox flex3\">\r\n                                <span class=\"globalLinkColor size3\">");
    #line default
                    try
                    {
    #line 25 "DisambiguationView"
                                                                        Output.Write(result.ActionName);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${result.ActionName}");
                    }
    #line hidden
                    Output.Write("</span><br/>\r\n                                <span class=\"globalFontColor size4\">");
    #line default
                    try
                    {
    #line 26 "DisambiguationView"
                                                                        Output.Write(result.Description);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${result.Description}");
                    }
    #line hidden
                    Output.Write("</span><br/>\r\n                            </div>\r\n                        </div>\r\n                    </a>\r\n                </div>");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n        </div>\r\n    </div>\r\n</body>\r\n</html>\r\n");
    #line default
        }
    }
}
