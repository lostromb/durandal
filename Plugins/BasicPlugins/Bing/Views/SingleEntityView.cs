//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Bing.Views
{
    public class SingleEntityView
    {
        private StringWriter Output;
        public string EntityImage {get; set;}
        public string EntityDescription {get; set;}
        public string EntityText {get; set;}
        public SingleEntityView()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n    <link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    <link href=\"/views/bing/entitycards.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    <title>");
    #line default
            try
            {
    #line 7 "SingleEntityView"
               Output.Write(EntityText);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${EntityText}");
            }
    #line hidden
            Output.Write("</title>\r\n</head>\r\n<body class=\"globalBgColor globalFontStyle globalFontColor\">\r\n    <h1>");
    #line default
            try
            {
    #line 10 "SingleEntityView"
            Output.Write(EntityText);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${EntityText}");
            }
    #line hidden
            Output.Write("</h1>\r\n    <img height=\"300px\" src=\"");
    #line default
            try
            {
    #line 11 "SingleEntityView"
                                 Output.Write(EntityImage);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${EntityImage}");
            }
    #line hidden
            Output.Write("\">\r\n    <p class=\"singleEntityDescription\">");
    #line default
    #line 12 "SingleEntityView"
                                           if (string.IsNullOrWhiteSpace(EntityDescription))
    #line default
            {
    #line hidden
                Output.Write("\r\n            I would like to show a description here, but no further information is available");
    #line default
            }
            else
    #line default
            {
    #line hidden
                Output.Write("\r\n            ");
    #line default
                try
                {
    #line 17 "SingleEntityView"
                Output.Write(EntityDescription);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${EntityDescription}");
                }
            }
    #line hidden
            Output.Write("\r\n    </p>\r\n</body>\r\n</html>\r\n");
    #line default
        }
    }
}
