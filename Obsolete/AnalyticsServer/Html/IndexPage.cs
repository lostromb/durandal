//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using DurandalServices.Instrumentation.Analytics.Charting;
namespace DurandalServices.Instrumentation.Analytics.Html
{
    public class IndexPage
    {
        private StringWriter Output;
        public string Content {get; set;}
        public List<NavbarLink> NavLinks {get; set;}
        public IndexPage()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n    <head>\r\n        <title>Durandal Analytics</title>\r\n        <link href=\"/flexbox.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n        <link href=\"/chart.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n        <script src=\"/Chart.bundle.min.js\"></script>\r\n    </head>\r\n    <body>\r\n        <div class=\"flex flexAppContainer\">\r\n            <div class=\"flex flexBoxColumn\">\r\n                <div id=\"navbar\" class=\"flex\">");
    #line default
            {
    #line 12 "IndexPage"
                                                  foreach(NavbarLink link in NavLinks)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                        <a href=\"");
    #line default
                    try
                    {
    #line 14 "IndexPage"
                                     Output.Write(link.Url);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${link.Url}");
                    }
    #line hidden
                    Output.Write("\" class=\"navbarlink\"><div class=\"navbaritem\">");
    #line default
                    try
                    {
    #line 14 "IndexPage"
                                                                                             Output.Write(link.Text);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${link.Text}");
                    }
    #line hidden
                    Output.Write("</div></a>");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n                </div>\r\n                ");
    #line default
            try
            {
    #line 17 "IndexPage"
                    Output.Write(Content);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Content}");
            }
    #line hidden
            Output.Write("\r\n            </div>\r\n        </div>\r\n    </body>\r\n</html>\r\n");
    #line default
        }
    }
}
