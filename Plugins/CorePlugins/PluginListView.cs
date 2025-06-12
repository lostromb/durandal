//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Reflection.Views
{
    public class PluginListView
    {
        private StringWriter Output;
        public IEnumerable<PluginRenderingInfo> Plugins {get; set;}
        public PluginListView()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n    <head>\r\n        <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n\t\t<meta name=\"viewport\" content=\"width=device-width, minimum-scale=1.0, initial-scale=1.0, maximum-scale=1.0, user-scalable=no\"/>\r\n        <link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\">\r\n        <link href=\"/views/reflection/responsive_grid.css\" rel=\"stylesheet\" type=\"text/css\">\r\n        <title>Installed Plugins</title>\r\n        ");
    #line default
            try
            {
    #line 9 "PluginListView"
            Output.Write(new Durandal.CommonViews.DynamicTheme().Render());
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${new Durandal.CommonViews.DynamicTheme().Render()}");
            }
    #line hidden
            Output.Write("\r\n        <script src=\"/views/common/durandal_assistant.js\"></script>\r\n        <script>\r\n            DurandalAssistant.logMessage(\"I am over here\");\r\n            DurandalAssistant.updateRequestData(\"key1\", \"value1\");\r\n            DurandalAssistant.updateRequestData(\"key2\", \"value2\");\r\n        </script>\r\n    </head>\r\n    <body class=\"globalBgColor globalFontStyle globalFontColor\">\r\n        <h1>Active dialog plugins</h1>\r\n        <ul class=\"cbp-rfgrid\">");
    #line default
            {
    #line 19 "PluginListView"
                                   foreach(PluginRenderingInfo plugin in Plugins)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                <li>\r\n                    <a href=\"");
    #line default
                    try
                    {
    #line 22 "PluginListView"
                                 Output.Write(plugin.InfoLink);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${plugin.InfoLink}");
                    }
    #line hidden
                    Output.Write("\">\r\n                        <img src=\"");
    #line default
                    try
                    {
    #line 23 "PluginListView"
                                      Output.Write(plugin.IconUrl);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${plugin.IconUrl}");
                    }
    #line hidden
                    Output.Write("\"/>\r\n                        <div>\r\n                            <h3>");
    #line default
                    try
                    {
    #line 25 "PluginListView"
                                    Output.Write(plugin.Name);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${plugin.Name}");
                    }
    #line hidden
                    Output.Write("</h3>\r\n                        </div>\r\n                    </a>\r\n                </li>");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n        </ul>\r\n    </body>\r\n</html>\r\n");
    #line default
        }
    }
}
