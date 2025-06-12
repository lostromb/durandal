//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Time
{
    public class ClockView
    {
        private StringWriter Output;
        public string StartTime {get; set;}
        public ClockView()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\"/>\r\n    <script src=\"/views/time/jquery-3.2.1.min.js\"></script>\r\n    <script src=\"/views/time/moment.min.js\"></script>\r\n    <link href=\"/views/time/clock.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n\r\n    <script>\r\nvar local_start_time = Date.now();\r\n$(function(){\r\n\r\n    // Cache some selectors\r\n\r\n    var clock = $('#clock'),\r\n        alarm = clock.find('.alarm'),\r\n        ampm = clock.find('.ampm');\r\n\r\n    // Map digits to their names (this will be an array)\r\n    var digit_to_name = 'zero one two three four five six seven eight nine'.split(' ');\r\n\r\n    // This object will hold the digit elements\r\n    var digits = {};\r\n\r\n    // Positions for the hours, minutes, and seconds\r\n    var positions = [\r\n        'h1', 'h2', ':', 'm1', 'm2', ':', 's1', 's2'\r\n    ];\r\n\r\n    // Generate the digits with the needed markup,\r\n    // and add them to the clock\r\n\r\n    var digit_holder = clock.find('.digits');\r\n\r\n    $.each(positions, function(){\r\n\r\n        if(this == ':'){\r\n            digit_holder.append('<div class=\"dots\">');\r\n        }\r\n        else{\r\n\r\n            var pos = $('<div>');\r\n\r\n            for(var i=1; i<8; i++){\r\n                pos.append('<span class=\"d' + i + '\">');\r\n            }\r\n\r\n            // Set the digits as key:value pairs in the digits object\r\n            digits[this] = pos;\r\n\r\n            // Add the digit elements to the page\r\n            digit_holder.append(pos);\r\n        }\r\n\r\n    });\r\n\r\n    // Add the weekday names\r\n\r\n    var weekday_names = 'MON TUE WED THU FRI SAT SUN'.split(' '),\r\n        weekday_holder = clock.find('.weekdays');\r\n\r\n    $.each(weekday_names, function(){\r\n        weekday_holder.append('<span>' + this + '</span>');\r\n    });\r\n\r\n    var weekdays = clock.find('.weekdays span');\r\n\r\n    // Run a timer every second and update the clock\r\n\r\n    (function update_time(){\r\n\r\n        // Use moment.js to output the current time as a string\r\n        // hh is for the hours in 12-hour format,\r\n        // mm - minutes, ss-seconds (all with leading zeroes),\r\n        // d is for day of week and A is for AM/PM\r\n        var time_since_page_load_ms = Date.now() - local_start_time;\r\n        var now = moment(\"");
    #line default
            try
            {
    #line 77 "ClockView"
                              Output.Write(StartTime);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${StartTime}");
            }
    #line hidden
            Output.Write("\").add(time_since_page_load_ms, \"ms\").utc().format(\"hhmmssdA\");\r\n\r\n        digits.h1.attr('class', digit_to_name[now[0]]);\r\n        digits.h2.attr('class', digit_to_name[now[1]]);\r\n        digits.m1.attr('class', digit_to_name[now[2]]);\r\n        digits.m2.attr('class', digit_to_name[now[3]]);\r\n        digits.s1.attr('class', digit_to_name[now[4]]);\r\n        digits.s2.attr('class', digit_to_name[now[5]]);\r\n\r\n        // The library returns Sunday as the first day of the week.\r\n        // Stupid, I know. Lets shift all the days one position down,\r\n        // and make Sunday last\r\n\r\n        var dow = now[6];\r\n        dow--;\r\n\r\n        // Sunday!\r\n        if(dow < 0){\r\n            // Make it last\r\n            dow = 6;\r\n        }\r\n\r\n        // Mark the active day of the week\r\n        weekdays.removeClass('active').eq(dow).addClass('active');\r\n\r\n        // Set the am/pm text:\r\n        ampm.text(now[7]+now[8]);\r\n\r\n        // Schedule this function to be run again in 1 sec\r\n        setTimeout(update_time, 1000);\r\n\r\n    })();\r\n});\r\n    </script>\r\n</head>\r\n<body>\r\n    <div id=\"clock\" class=\"dark\">\r\n        <div class=\"display\">\r\n            <div class=\"weekdays\"></div>\r\n            <div class=\"ampm\"></div>\r\n            <div class=\"alarm\"></div>\r\n            <div class=\"digits\"></div>\r\n        </div>\r\n    </div>\r\n</body>\r\n</html>\r\n");
    #line default
        }
    }
}
