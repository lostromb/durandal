//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Time
{
    public class TimerView
    {
        private StringWriter Output;
        public bool countDown {get; set;}
        public long targetTimeEpoch {get; set;}
        public TimerView()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n\r\n<head>\r\n    <title>Timer</title>\r\n    <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n    <meta name=\"viewport\" content=\"width=device-width, minimum-scale=1.0, initial-scale=1.0, maximum-scale=1.0, user-scalable=no\"/>\r\n    <link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n    <link href=\"/views/common/flexbox.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n    <link href=\"/views/time/timer.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n    <script type=\"text/javascript\" src=\"/views/common/durandal_assistant.js\"></script>\r\n    <script type=\"text/javascript\" src=\"/views/time/timer.js\"></script>\r\n    ");
    #line default
            try
            {
    #line 13 "TimerView"
        Output.Write(new Durandal.CommonViews.DynamicTheme().Render());
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${new Durandal.CommonViews.DynamicTheme().Render()}");
            }
    #line hidden
            Output.Write("\r\n</head>\r\n\r\n<body class=\"globalBgColor\" onload=\"Timer.start(");
    #line default
            try
            {
    #line 16 "TimerView"
                                                    Output.Write(countDown.ToString().ToLower());
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${countDown.ToString().ToLower()}");
            }
    #line hidden
            Output.Write(", ");
    #line default
            try
            {
    #line 16 "TimerView"
                                                                                       Output.Write(targetTimeEpoch);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${targetTimeEpoch}");
            }
    #line hidden
            Output.Write(")\">\r\n    <div id=\"timer\"><span id=\"hoursField\">00</span>:<span id=\"minutesField\">00</span>:<span id=\"secondsField\">00</span>.<span id=\"msField\">000</span></div>\r\n    <button id=\"pauseButton\" onclick=\"Timer.pause()\">Pause</button>\r\n    <button id=\"stopButton\" onclick=\"Timer.stop()\">Stop</button>\r\n</body>\r\n\r\n</html>\r\n");
    #line default
        }
    }
}
