//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Bing.Views
{
    public class BasicEntityCard
    {
        private StringWriter Output;
        public string TypeName {get; set;}
        public string EntityText {get; set;}
        public string Image {get; set;}
        public string Description {get; set;}
        public BasicEntityCard()
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
            Output.Write("<img class=\"entityImage\" src=\"");
    #line default
            try
            {
    #line 1 "BasicEntityCard"
                                  Output.Write(Image);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Image}");
            }
    #line hidden
            Output.Write("\">\r\n<div class=\"entityTopShadow\"></div>\r\n<div class=\"entityBottomShadow\"></div>\r\n<span class=\"entityTypeName\">Type: ");
    #line default
            try
            {
    #line 4 "BasicEntityCard"
                                       Output.Write(TypeName);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${TypeName}");
            }
    #line hidden
            Output.Write("</span>\r\n<span class=\"entityTitle\">");
    #line default
            try
            {
    #line 5 "BasicEntityCard"
                              Output.Write(EntityText);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${EntityText}");
            }
    #line hidden
            Output.Write("</span>\r\n<p class=\"entityDescription\">");
    #line default
            try
            {
    #line 6 "BasicEntityCard"
                                 Output.Write(Description);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Description}");
            }
    #line hidden
            Output.Write("</p>\r\n");
    #line default
        }
    }
}
