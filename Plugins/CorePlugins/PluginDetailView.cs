//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Reflection.Views
{
    public class PluginDetailView
    {
        private StringWriter Output;
        public PluginRenderingInfo Plugin {get; set;}
        public string Backlink {get; set;}
        public PluginDetailView()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n    <link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    <title>Installed Plugins</title>\r\n    ");
    #line default
            try
            {
    #line 7 "PluginDetailView"
        Output.Write(new Durandal.CommonViews.DynamicTheme().Render());
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${new Durandal.CommonViews.DynamicTheme().Render()}");
            }
    #line hidden
            Output.Write("\r\n</head>\r\n<body class=\"globalBgColor globalFontStyle globalFontColor\">\r\n    <img src=\"");
    #line default
            try
            {
    #line 10 "PluginDetailView"
                  Output.Write(Plugin.IconUrl);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Plugin.IconUrl}");
            }
    #line hidden
            Output.Write("\" height=\"192px\">\r\n    <h2>");
    #line default
            try
            {
    #line 11 "PluginDetailView"
            Output.Write(Plugin.Name);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Plugin.Name}");
            }
    #line hidden
            Output.Write("</h2>\r\n    ");
    #line default
            try
            {
    #line 12 "PluginDetailView"
        Output.Write(Plugin.Subtitle);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Plugin.Subtitle}");
            }
    #line hidden
            Output.Write("<br>\r\n    ");
    #line default
            try
            {
    #line 13 "PluginDetailView"
        Output.Write(Plugin.LongDescription);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Plugin.LongDescription}");
            }
    #line hidden
            Output.Write("<br>");
    #line default
    #line 13 "PluginDetailView"
                                     if (Plugin.SampleQueries != null && Plugin.SampleQueries.Count > 0)
    #line default
            {
    #line hidden
                Output.Write("\r\n        <h3>Try these queries:</h3>");
    #line default
                {
    #line 15 "PluginDetailView"
                                       foreach(PluginSampleQuery query in Plugin.SampleQueries)
    #line default
                    {
    #line hidden
    #line default
    #line hidden
                        Output.Write("\r\n            <a href=\"");
    #line default
                        try
                        {
    #line 17 "PluginDetailView"
                         Output.Write(query.Deeplink);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${query.Deeplink}");
                        }
    #line hidden
                        Output.Write("\">");
    #line default
                        try
                        {
    #line 17 "PluginDetailView"
                                            Output.Write(query.Utterance);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${query.Utterance}");
                        }
    #line hidden
                        Output.Write("</a><br>");
    #line default
    #line hidden
    #line default
                    }
                }
    #line hidden
                Output.Write("\r\n        <br>");
    #line default
            }
    #line hidden
            Output.Write("\r\n    <a class=\"globalLinkColor\" href=\"");
    #line default
            try
            {
    #line 21 "PluginDetailView"
                                         Output.Write(Backlink);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Backlink}");
            }
    #line hidden
            Output.Write("\">Go Back</a>\r\n</body>\r\n</html>\r\n");
    #line default
        }
    }
}
