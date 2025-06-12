//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Bing.Views
{
    public class MultiEntityView
    {
        private StringWriter Output;
        public List<SelectableEntity> Entities {get; set;}
        public string Header {get; set;}
        public MultiEntityView()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n    <link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    <link href=\"/views/bing/entitycards.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    <title>Bing Search Results</title>\r\n</head>\r\n<body class=\"globalBgColor globalFontStyle globalFontColor\">\r\n    <h1 class=\"entityPageHeader\">");
    #line default
            try
            {
    #line 10 "MultiEntityView"
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
    #line 10 "MultiEntityView"
                                                   foreach(SelectableEntity e in Entities)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n        <a href=\"");
    #line default
                    try
                    {
    #line 12 "MultiEntityView"
                     Output.Write(e.SelectActionUrl);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${e.SelectActionUrl}");
                    }
    #line hidden
                    Output.Write("\">\r\n            <div class=\"entityBoxFixed\">\r\n                ");
    #line default
                    try
                    {
    #line 14 "MultiEntityView"
                    Output.Write(e.HtmlCard);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${e.HtmlCard}");
                    }
    #line hidden
                    Output.Write("\r\n            </div>\r\n        </a>");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n</body>\r\n</html>\r\n");
    #line default
        }
    }
}
