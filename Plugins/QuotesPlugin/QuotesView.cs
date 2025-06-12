//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Quotes
{
    public class QuotesView
    {
        private StringWriter Output;
        public IList<string> Quotes {get; set;}
        public string AuthorName {get; set;}
        public QuotesView()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n    <link href=\"/views/quotes/quotes.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    <link href=\"/views/common/flexbox.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    <title>QuotesDaddy</title>\r\n</head>\r\n<body class=\"globalFontStyle\">\r\n    <span class=\"title\">Quotes by ");
    #line default
            try
            {
    #line 10 "QuotesView"
                                      Output.Write(AuthorName);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${AuthorName}");
            }
    #line hidden
            Output.Write("</span><br/>");
    #line default
            {
    #line 10 "QuotesView"
                                                               foreach(string quote in Quotes)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n        <p class=\"quote\"><span class=\"oq\">&ldquo;</span>");
    #line default
                    try
                    {
    #line 12 "QuotesView"
                                                            Output.Write(quote);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${quote}");
                    }
    #line hidden
                    Output.Write("<span class=\"oq\">&rdquo;</span></p>\r\n        <br/>");
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
