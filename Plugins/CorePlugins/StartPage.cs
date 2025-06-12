//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using Durandal.API;
namespace Durandal.Plugins.Reflection.Views
{
    public class StartPage
    {
        private StringWriter Output;
        public string Revision {get; set;}
        public string Date {get; set;}
        public bool UseHtml5 {get; set;}
        public ISet<string> SampleQueries {get; set;}
        public string ListPluginsLink {get; set;}
        public ClientAuthenticationLevel AuthLevel {get; set;}
        public StartPage()
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
    #line 1 "StartPage"
    if (UseHtml5)
    #line default
            {
    #line hidden
                Output.Write("\r\n    <!DOCTYPE HTML>\r\n    <html>\r\n        <head>\r\n            <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n            <meta name=\"viewport\" content=\"width=device-width, minimum-scale=1.0, initial-scale=1.0, maximum-scale=1.0, user-scalable=no\">\r\n            <link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\">\r\n            <link href=\"/views/reflection/start.css\" rel=\"stylesheet\" type=\"text/css\">\r\n            ");
    #line default
                try
                {
    #line 9 "StartPage"
                Output.Write(new Durandal.CommonViews.DynamicTheme().Render());
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${new Durandal.CommonViews.DynamicTheme().Render()}");
                }
    #line hidden
                Output.Write("\r\n            <script src=\"/views/common/jquery-3.2.1.min.js\"></script>\r\n            <script>\r\n                var glowTime = 0;\r\n                var suggestionTimer = 0;\r\n                var sampleQueries = [");
    #line default
    #line 14 "StartPage"
                                         if (SampleQueries != null)
    #line default
                {
                    {
                        bool sampleQueryIsFirst = true;
    #line 15 "StartPage"
                                                          foreach(string sampleQuery in SampleQueries)
    #line default
                        {
    #line hidden
    #line default
    #line default
                            if (sampleQueryIsFirst)
    #line default
                            {
    #line hidden
                                Output.Write("\r\n\t\t            ");
    #line default
                            }
    #line hidden
                            Output.Write("\"\\\"");
    #line default
                            try
                            {
    #line 16 "StartPage"
                                                                           Output.Write(sampleQuery);
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${sampleQuery}");
                            }
    #line hidden
                            Output.Write("\\\"\",");
    #line default
    #line hidden
                            sampleQueryIsFirst = false;
    #line default
                        }
                    }
                }
    #line hidden
                Output.Write("\r\n                ];\r\n                var sampleQueryIndex = 0;\r\n                var step = function () {\r\n                    glowTime += 0.1;\r\n                    suggestionTimer += 0.1;\r\n                    document.getElementById(\"glowImg\").style.opacity = (Math.sin(glowTime) + 1.0) / 3.0;\r\n\r\n                    // Switch suggested queries every 4 seconds\r\n                    if (suggestionTimer > 4) {\r\n                        suggestionTimer = 0;\r\n                        sampleQueryIndex += 1;\r\n                        if (sampleQueryIndex >= sampleQueries.length) {\r\n                            sampleQueryIndex = 0;\r\n                        }\r\n                        $(\"#suggestionSpan\").fadeOut(500, function () {\r\n                            if (sampleQueries[sampleQueryIndex]) {\r\n                                document.getElementById(\"suggestionText\").innerHTML = sampleQueries[sampleQueryIndex];\r\n                            }\r\n                            $(\"#suggestionSpan\").fadeIn(500);\r\n                        });\r\n                    }\r\n                };\r\n            </script>\r\n        </head>\r\n        <body class=\"globalBgColor globalFontStyle globalFontColor\" onload=\"setInterval(step, 100);\">\r\n\t\t\t<div id=\"centerDiv\">\r\n                <div id=\"containerDiv\" class=\"smallFont\">\r\n                    <img id=\"logoImg\" src=\"/views/reflection/logo.png\">\r\n                    <img id=\"glowImg\" src=\"/views/reflection/logoglow.png\">\r\n                    <a class=\"hiddenLink\" href=\"");
    #line default
                try
                {
    #line 47 "StartPage"
                                                    Output.Write(ListPluginsLink);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${ListPluginsLink}");
                }
    #line hidden
                Output.Write("\">\r\n                        <div id=\"suggestionSpan\">\r\n                            <span class=\"globalFontColor\">Try </span><span id=\"suggestionText\" class=\"globalFontColorAccent\">\"Hello!\"</span>\r\n                        </div>\r\n                    </a>\r\n                </div>\r\n            </div>\r\n\t\t\t<div id=\"authIndicator\">");
    #line default
    #line 54 "StartPage"
                                        if (AuthLevel.HasFlag(ClientAuthenticationLevel.UserAuthorized))
    #line default
                {
    #line hidden
                    Output.Write("\r\n\t\t\t\t\t<img src=\"/views/reflection/auth_authorized.png\" width=\"16px\" height=\"16px\">");
    #line default
                }
    #line 57 "StartPage"
                         if (AuthLevel.HasFlag(ClientAuthenticationLevel.UserUnauthorized))
    #line default
                {
    #line hidden
                    Output.Write("\r\n\t\t\t\t\t<img src=\"/views/reflection/auth_unauthorized.png\" width=\"16px\" height=\"16px\">");
    #line default
                }
    #line 60 "StartPage"
                         if (AuthLevel.HasFlag(ClientAuthenticationLevel.UserUnverified))
    #line default
                {
    #line hidden
                    Output.Write("\r\n\t\t\t\t\t<img src=\"/views/reflection/auth_unverified.png\" width=\"16px\" height=\"16px\">");
    #line default
                }
    #line 63 "StartPage"
                         if (AuthLevel.HasFlag(ClientAuthenticationLevel.UserUnknown))
    #line default
                {
    #line hidden
                    Output.Write("\r\n\t\t\t\t\t<img src=\"/views/reflection/auth_unknown.png\" width=\"16px\" height=\"16px\">");
    #line default
                }
    #line hidden
                Output.Write("\r\n\t\t\t</div>\r\n\t\t\t<div id=\"versionSpan\">\r\n\t\t\t\tDurandal ");
    #line default
                try
                {
    #line 69 "StartPage"
                             Output.Write(Revision);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${Revision}");
                }
    #line hidden
                Output.Write(" Logan Stromberg ");
    #line default
                try
                {
    #line 69 "StartPage"
                                                         Output.Write(Date);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${Date}");
                }
    #line hidden
                Output.Write("\r\n\t\t\t</div>\r\n        </body>\r\n    </html>");
    #line default
            }
            else
    #line default
            {
    #line hidden
                Output.Write("\r\n    <!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.0 Transitional//EN\">\r\n    <html>\r\n        <head>\r\n            <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n            <meta name=\"viewport\" content=\"width=device-width, minimum-scale=1.0, initial-scale=1.0, maximum-scale=1.0, user-scalable=no\">\r\n            <link href=\"/views/common/global_html4.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n            <link href=\"/views/reflection/start.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n        </head>\r\n        <body class=\"globalBgColor globalFontStyle globalFontColor\">\r\n\t\t\t<div id=\"centerDiv\">\r\n                <div id=\"containerDiv\">\r\n                    <br/><br/><br/>\r\n                    <img id=\"logoImgNoGlow\" src=\"/views/reflection/logo.png\"><br>\r\n                    <span>Durandal Prototype ");
    #line default
                try
                {
    #line 88 "StartPage"
                                                 Output.Write(Revision);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${Revision}");
                }
    #line hidden
                Output.Write("</span><br>\r\n                    <span>Logan Stromberg ");
    #line default
                try
                {
    #line 89 "StartPage"
                                              Output.Write(Date);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${Date}");
                }
    #line hidden
                Output.Write("</span>\r\n                </div>\r\n            </div>\r\n\t\t\t<div id=\"authIndicator\">");
    #line default
    #line 92 "StartPage"
                                        if (AuthLevel.HasFlag(ClientAuthenticationLevel.UserAuthorized))
    #line default
                {
    #line hidden
                    Output.Write("\r\n                    <img src=\"/views/reflection/auth_authorized.png\" width=\"16px\" height=\"16px\">");
    #line default
                }
    #line 95 "StartPage"
                         if (AuthLevel.HasFlag(ClientAuthenticationLevel.UserUnauthorized))
    #line default
                {
    #line hidden
                    Output.Write("\r\n                    <img src=\"/views/reflection/auth_unauthorized.png\" width=\"16px\" height=\"16px\">");
    #line default
                }
    #line 98 "StartPage"
                         if (AuthLevel.HasFlag(ClientAuthenticationLevel.UserUnverified))
    #line default
                {
    #line hidden
                    Output.Write("\r\n                    <img src=\"/views/reflection/auth_unverified.png\" width=\"16px\" height=\"16px\">");
    #line default
                }
    #line 101 "StartPage"
                         if (AuthLevel.HasFlag(ClientAuthenticationLevel.UserUnknown))
    #line default
                {
    #line hidden
                    Output.Write("\r\n                    <img src=\"/views/reflection/auth_unknown.png\" width=\"16px\" height=\"16px\">");
    #line default
                }
    #line hidden
                Output.Write("\r\n\t\t\t</div>\r\n        </body>\r\n    </html>");
    #line default
            }
    #line hidden
            Output.Write("\r\n");
    #line default
        }
    }
}
