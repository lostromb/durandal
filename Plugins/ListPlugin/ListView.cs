//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Answers.ListAnswer
{
    public class ListView
    {
        private StringWriter Output;
        public string pageTitle {get; set;}
        public string conversationResponse {get; set;}
        public string cancelLink {get; set;}
        public string doneLink {get; set;}
        public ListEntity list {get; set;}
        public ListView()
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
            Output.Write("<!DOCTYPE HTML>\r\n<html>\r\n    <head>\r\n        <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n        <link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\">\r\n        <link href=\"/views/note/note.css\" rel=\"stylesheet\" type=\"text/css\">\r\n        <title>@pageTitle</title>\r\n        ");
    #line default
            try
            {
    #line 8 "ListView"
            Output.Write(new Durandal.CommonViews.DynamicTheme().Render());
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${new Durandal.CommonViews.DynamicTheme().Render()}");
            }
    #line hidden
            Output.Write("\r\n    </head>\r\n    <body class=\"globalBgColor globalFontStyle globalFontColor\">\r\n        <span class=\"globalFontColorAccent\">@conversationResponse</span>\r\n        <hr>\r\n        @list.Title<br>\r\n        <ul>\r\n            @foreach (string entry in list.Entries)\r\n            {\r\n                <li>@entry</li>\r\n            }\r\n        </ul>\r\n        <div id=\"bottomButtons\" class=\"globalBgColor\" align=\"center\">\r\n            <a class=\"buttonLink\" href=\"@cancelLink\"><span class=\"buttonDiv\">Cancel</span></a>\r\n            <a class=\"buttonLink\" href=\"@doneLink\"><span class=\"buttonDiv\">Done</span></a>\r\n        </div>\r\n    </body>\r\n</html>\r\n");
    #line default
        }
    }
}
