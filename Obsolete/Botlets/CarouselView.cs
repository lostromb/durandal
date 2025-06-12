//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Answers.Botlets
{
    public class CarouselView
    {
        private StringWriter Output;
        public IList<CarouselItem> Items {get; set;}
        public string HeaderText {get; set;}
        public CarouselView()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n    <head>\r\n        <link rel=\"stylesheet\" href=\"http://yui.yahooapis.com/pure/0.5.0/base-min.css\"/>\r\n        <link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n        <link href=\"/views/common/flexbox.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n        <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\"/>\r\n        <title>Results</title>\r\n        ");
    #line default
            try
            {
    #line 9 "CarouselView"
            Output.Write(new Durandal.CommonViews.DynamicTheme().Render());
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${new Durandal.CommonViews.DynamicTheme().Render()}");
            }
    #line hidden
            Output.Write("\r\n    </head>\r\n    <body class=\"globalBgColor globalFgColor globalFontStyle globalFontColor\">\r\n        <h1>");
    #line default
            try
            {
    #line 12 "CarouselView"
                Output.Write(HeaderText);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${HeaderText}");
            }
    #line hidden
            Output.Write("</h1>");
    #line default
            {
    #line 12 "CarouselView"
                                  foreach(var item in Items)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n            <div>\r\n                <img width=\"150px\" class=\"pure-img\" style=\"display:inline\" src=\"");
    #line default
                    try
                    {
    #line 15 "CarouselView"
                                                                                    Output.Write(item.ImageUrl);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${item.ImageUrl}");
                    }
    #line hidden
                    Output.Write("\"/>");
    #line default
    #line 15 "CarouselView"
                                                                                                        if (!string.IsNullOrWhiteSpace(item.LinkUrl))
    #line default
                    {
    #line hidden
                        Output.Write("\r\n                    <a href=\"");
    #line default
                        try
                        {
    #line 17 "CarouselView"
                                 Output.Write(item.LinkUrl);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${item.LinkUrl}");
                        }
    #line hidden
                        Output.Write("\" class=\"globalLinkColor\">");
    #line default
                        try
                        {
    #line 17 "CarouselView"
                                                                          Output.Write(item.LinkText);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${item.LinkText}");
                        }
    #line hidden
                        Output.Write("</a><br/>");
    #line default
                    }
                    else
    #line default
                    {
    #line hidden
                        Output.Write("\r\n                    ");
    #line default
                        try
                        {
    #line 20 "CarouselView"
                        Output.Write(item.LinkText);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${item.LinkText}");
                        }
    #line hidden
                        Output.Write("<br/>");
    #line default
                    }
    #line hidden
                    Output.Write("\r\n            </div>");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n    </body>\r\n</html>\r\n");
    #line default
        }
    }
}
