//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Color
{
    public class ColorDisplay
    {
        private StringWriter Output;
        public string Text {get; set;}
        public ColorForDisplay MainColor {get; set;}
        public IList<ColorForDisplay> NearbyColors {get; set;}
        public ColorDisplay()
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
            Output.Write("<!DOCTYPE HTML>\r\n<html>\r\n    <head>\r\n        <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n        <link rel=\"stylesheet\" href=\"/views/common/pure-base-min.css\"/>\r\n        <link href=\"/views/common/flexbox.css\" rel=\"stylesheet\" type=\"text/css\">\r\n        <link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\">\r\n        <link href=\"/views/color/color.css\" rel=\"stylesheet\" type=\"text/css\">\r\n        <title>Color</title>\r\n        ");
    #line default
            try
            {
    #line 10 "ColorDisplay"
            Output.Write(new Durandal.CommonViews.DynamicTheme().Render());
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${new Durandal.CommonViews.DynamicTheme().Render()}");
            }
    #line hidden
            Output.Write("\r\n    </head>\r\n    <body class=\"globalBgColor globalFontStyle\" style=\"background-color: #");
    #line default
            try
            {
    #line 12 "ColorDisplay"
                                                                              Output.Write(MainColor.Hex);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${MainColor.Hex}");
            }
    #line hidden
            Output.Write("\">\r\n        <div class=\"flex flexAppContainer\">\r\n            <div class=\"flex flexBoxColumn flex-stretch screenPadding\">\r\n                <div class=\"flex5\"></div>");
    #line default
    #line 15 "ColorDisplay"
                                             if (MainColor.IsBright)
    #line default
            {
    #line hidden
                Output.Write("\r\n                    <div align=\"center\" style=\"text-align: center\"><span class=\"flex1 size2\" style=\"color: #000000;\">");
    #line default
                try
                {
    #line 17 "ColorDisplay"
                                                                                                                         Output.Write(Text);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${Text}");
                }
    #line hidden
                Output.Write("</span></div>");
    #line default
            }
            else
    #line default
            {
    #line hidden
                Output.Write("\r\n                    <div align=\"center\" style=\"text-align: center\"><span class=\"flex1 size2\" style=\"color: #FFFFFF;\">");
    #line default
                try
                {
    #line 20 "ColorDisplay"
                                                                                                                         Output.Write(Text);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${Text}");
                }
    #line hidden
                Output.Write("</span></div>");
    #line default
            }
    #line hidden
            Output.Write("\r\n                <div class=\"flex5\"></div>");
    #line default
    #line 22 "ColorDisplay"
                                             if (NearbyColors != null)
    #line default
            {
    #line hidden
                Output.Write("\r\n                    <div align=\"center\" style=\"text-align: center\">\r\n                        <span class=\"size4\">Similar Colors</span>\r\n                    </div>\r\n                    <div class=\"flex flex1 flexBoxRow flex-stretch\">\r\n                        <div class=\"flex1\"></div>");
    #line default
                {
    #line 28 "ColorDisplay"
                                                     foreach(ColorForDisplay similarColor in NearbyColors)
    #line default
                    {
    #line hidden
    #line default
    #line hidden
                        Output.Write("\r\n                            <a href=\"");
    #line default
                        try
                        {
    #line 30 "ColorDisplay"
                                         Output.Write(similarColor.ActionUrl);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${similarColor.ActionUrl}");
                        }
    #line hidden
                        Output.Write("\" class=\"hiddenLink\">");
    #line default
    #line 30 "ColorDisplay"
                                                                                       if (similarColor.IsBright)
    #line default
                        {
    #line hidden
                            Output.Write("\r\n                                    <div class=\"colorSuggestionDiv flex-stretch\" style=\"background-color: #");
    #line default
                            try
                            {
    #line 32 "ColorDisplay"
                                                                                                               Output.Write(similarColor.Hex);
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${similarColor.Hex}");
                            }
    #line hidden
                            Output.Write(";\"><span class=\"size4\" style=\"color: #000000;\">");
    #line default
                            try
                            {
    #line 32 "ColorDisplay"
                                                                                                                                                                                 Output.Write(similarColor.LocalizedName);
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${similarColor.LocalizedName}");
                            }
    #line hidden
                            Output.Write("</span></div>");
    #line default
                        }
                        else
    #line default
                        {
    #line hidden
                            Output.Write("\r\n                                    <div class=\"colorSuggestionDiv flex-stretch\" style=\"background-color: #");
    #line default
                            try
                            {
    #line 35 "ColorDisplay"
                                                                                                               Output.Write(similarColor.Hex);
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${similarColor.Hex}");
                            }
    #line hidden
                            Output.Write(";\"><span class=\"size4\" style=\"color: #FFFFFF;\">");
    #line default
                            try
                            {
    #line 35 "ColorDisplay"
                                                                                                                                                                                 Output.Write(similarColor.LocalizedName);
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${similarColor.LocalizedName}");
                            }
    #line hidden
                            Output.Write("</span></div>");
    #line default
                        }
    #line hidden
                        Output.Write("\r\n                            </a>");
    #line default
    #line hidden
    #line default
                    }
                }
    #line hidden
                Output.Write("\r\n                        <div class=\"flex1\"></div>\r\n                    </div>");
    #line default
            }
    #line hidden
            Output.Write("\r\n            </div>\r\n        </div>\r\n    </body>\r\n</html>\r\n");
    #line default
        }
    }
}
