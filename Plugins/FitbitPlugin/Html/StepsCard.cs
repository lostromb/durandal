//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Fitbit.Html
{
    public class StepsCard
    {
        private StringWriter Output;
        public int Percent {get; set;}
        public int StepsTaken {get; set;}
        public int StepsToGoal {get; set;}
        public string DateString {get; set;}
        public StepsCard()
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
            Output.Write("<!DOCTYPE HTML>\r\n<html>\r\n    <head>\r\n        <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n        <link href=\"/views/fitbit/fitbit.css\" rel=\"stylesheet\" type=\"text/css\">\r\n        <link href=\"/views/fitbit/percentage_circle.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    </head>\r\n    <body>\r\n        <div class=\"container\">\r\n            <div class=\"container-background\"></div>\r\n            <div class=\"container-border\">\r\n                <p class=\"top_text\">\r\n                    <span class=\"day\">");
    #line default
            try
            {
    #line 13 "StepsCard"
                                          Output.Write(DateString);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${DateString}");
            }
    #line hidden
            Output.Write("</span>\r\n                </p>\r\n                <div class=\"data_container\">\r\n                    <div class=\"exercise_circle\">");
    #line default
    #line 16 "StepsCard"
                                                     if (Percent < 100)
    #line default
            {
    #line hidden
                Output.Write("\r\n\t\t\t\t\t\t\t<div class=\"steps_circle c100 p");
    #line default
                try
                {
    #line 18 "StepsCard"
                                                               Output.Write(Percent);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${Percent}");
                }
    #line hidden
                Output.Write("\">\r\n\t\t\t\t\t\t\t\t<div class=\"slice\">\r\n\t\t\t\t\t\t\t\t\t<div class=\"bar\"></div>\r\n\t\t\t\t\t\t\t\t\t<div class=\"fill\"></div>\r\n\t\t\t\t\t\t\t\t</div>\r\n\t\t\t\t\t\t\t\t<div>");
    #line default
    #line 23 "StepsCard"
                                         if (StepsTaken == 0)
    #line default
                {
    #line hidden
                    Output.Write("\r\n\t\t\t\t\t\t\t\t\t\t<img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_steps_50.png\">");
    #line default
                }
                else
    #line default
                {
    #line hidden
                    Output.Write("\r\n\t\t\t\t\t\t\t\t\t\t<img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_steps_50.png\">");
    #line default
                }
    #line hidden
                Output.Write("\r\n\t\t\t\t\t\t\t\t</div>\r\n\t\t\t\t\t\t\t</div>");
    #line default
            }
            else
    #line default
            {
    #line hidden
                Output.Write("\r\n\t\t\t\t\t\t\t<div class=\"steps_circle c100 p100 green\">\r\n\t\t\t\t\t\t\t\t<div class=\"slice\">\r\n\t\t\t\t\t\t\t\t\t<div class=\"bar\"></div>\r\n\t\t\t\t\t\t\t\t\t<div class=\"fill\"></div>\r\n\t\t\t\t\t\t\t\t</div>\r\n\t\t\t\t\t\t\t\t<div>\r\n\t\t\t\t\t\t\t\t\t<img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_steps_goal_50.png\">\r\n\t\t\t\t\t\t\t\t</div>\r\n\t\t\t\t\t\t\t</div>");
    #line default
            }
    #line hidden
            Output.Write("\r\n                    </div>\r\n                </div>\r\n                <p class=\"exercise_data_container\">\r\n                    <span class=\"exercise_number\">");
    #line default
            try
            {
    #line 47 "StepsCard"
                                                      Output.Write(StepsTaken);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${StepsTaken}");
            }
    #line hidden
            Output.Write("</span>\r\n                    <span class=\"exercise_type\">steps</span>\r\n                </p>");
    #line default
    #line 49 "StepsCard"
                        if (StepsToGoal >= 0)
    #line default
            {
    #line hidden
                Output.Write("\r\n\t\t\t\t\t<p class=\"bottom_message\">\r\n\t\t\t\t\t\t<span class=\"number_left\">");
    #line default
                try
                {
    #line 52 "StepsCard"
                                                      Output.Write(StepsToGoal);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${StepsToGoal}");
                }
    #line hidden
                Output.Write("</span>\r\n\t\t\t\t\t\t<span class=\"exercise_left\">steps left to reach your goal!</span>\r\n\t\t\t\t\t</p>");
    #line default
            }
            else
    #line default
            {
    #line hidden
                Output.Write("\r\n\t\t\t\t\t<p class=\"bottom_message\">\r\n\t\t\t\t\t\t<span class=\"number_left\">");
    #line default
                try
                {
    #line 58 "StepsCard"
                                                      Output.Write(0 - StepsToGoal);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${0 - StepsToGoal}");
                }
    #line hidden
                Output.Write("</span>\r\n\t\t\t\t\t\t<span class=\"exercise_left\">steps above your goal!</span>\r\n\t\t\t\t\t</p>");
    #line default
            }
    #line hidden
            Output.Write("\r\n            </div>\r\n        </div>\r\n    </body>\r\n</html>\r\n");
    #line default
        }
    }
}
