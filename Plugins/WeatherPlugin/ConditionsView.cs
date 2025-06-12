//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using Durandal.ExternalServices.Darksky;
namespace Durandal.Plugins.Weather.Views
{
    public class ConditionsView
    {
        private StringWriter Output;
        public string Location {get; set;}
        public string Temperature {get; set;}
        public string DetailedConditions {get; set;}
        public string ConditionImageName {get; set;}
        public string BackgroundImageName {get; set;}
        public string WindCondition {get; set;}
        public string RainCondition {get; set;}
        public string HumidityCondition {get; set;}
        public string PressureCondition {get; set;}
        public DarkskyWeatherResult FullWeatherResult {get; set;}
        public string RefreshUrl {get; set;}
        public ConditionsView()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n<head>\r\n<meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\"/>\r\n<meta name=\"viewport\" content=\"width=device-width, minimum-scale=1.0, initial-scale=1.0, maximum-scale=1.0, user-scalable=no\"/>\r\n<link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n<link href=\"/views/common/flexbox.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n<link href=\"/views/weather/weather.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n<title>Local weather</title>\r\n");
    #line default
            try
            {
    #line 10 "ConditionsView"
    Output.Write(new Durandal.CommonViews.DynamicTheme().Render());
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${new Durandal.CommonViews.DynamicTheme().Render()}");
            }
    #line hidden
            Output.Write("\r\n<script src=\"/views/weather/js/jquery.min.js\"></script>\r\n<script src=\"/views/weather/js/backstretch.js\"></script>");
    #line default
    #line 12 "ConditionsView"
                                                            if (!string.IsNullOrEmpty(RefreshUrl))
    #line default
            {
    #line hidden
                Output.Write("\r\n        <!-- Refresh URL is given - rely on javascript to drive automatic refresh -->\r\n        <script type=\"text/javascript\">\r\n            $(document).ready(function () {\r\n                $.backstretch(\"/views/weather/bg/");
    #line default
                try
                {
    #line 17 "ConditionsView"
                                                     Output.Write(BackgroundImageName);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${BackgroundImageName}");
                }
    #line hidden
                Output.Write("\");\r\n                window.setTimeout(function () { window.location = \"");
    #line default
                try
                {
    #line 18 "ConditionsView"
                                                                       Output.Write(RefreshUrl);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${RefreshUrl}");
                }
    #line hidden
                Output.Write("\"; }, 1800000); // Refresh the weather every 30 minutes\r\n            });\r\n        </script>");
    #line default
            }
            else
    #line default
            {
    #line hidden
                Output.Write("\r\n        <!-- No refresh URL - the client itself will trigger refresh via DelayedDialogAction -->\r\n        <script type=\"text/javascript\">\r\n            $(document).ready(function () {\r\n                $.backstretch(\"/views/weather/bg/");
    #line default
                try
                {
    #line 26 "ConditionsView"
                                                     Output.Write(BackgroundImageName);
    #line default
                }
                catch(System.NullReferenceException)
                {
                    Output.Write("${BackgroundImageName}");
                }
    #line hidden
                Output.Write("\");\r\n            });\r\n        </script>");
    #line default
            }
    #line hidden
            Output.Write("\r\n</head>\r\n<body class=\"globalFontStyle\">\r\n\t<div class=\"flex flexAppContainer\">\r\n\t\t<div class=\"flex flexBoxColumn flex-stretch\">\r\n\t\t\t<div class=\"flex alignCenter\">\r\n\t\t\t\t<div class=\"flex flexBoxColumn\">\r\n\t\t\t\t\t<span id=\"locationLabel\">");
    #line default
            try
            {
    #line 36 "ConditionsView"
                                                 Output.Write(Location);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Location}");
            }
    #line hidden
            Output.Write("</span>\r\n\t\t\t\t\t<span id=\"temperature\">");
    #line default
            try
            {
    #line 37 "ConditionsView"
                                               Output.Write(Temperature);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Temperature}");
            }
    #line hidden
            Output.Write("</span>\r\n\t\t\t\t\t<span id=\"conditionLabel\">");
    #line default
            try
            {
    #line 38 "ConditionsView"
                                                  Output.Write(DetailedConditions);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${DetailedConditions}");
            }
    #line hidden
            Output.Write("</span>\r\n\t\t\t\t</div>\r\n\t\t\t</div>\r\n\t\t\t<div class=\"flex lightBackground\">\r\n\t\t\t\t<div class=\"flex flexBoxColumn\">\r\n\t\t\t\t\t<div class=\"flex flexBoxRow\">\r\n\t\t\t\t\t\t<div class=\"flex flex1\"></div>\r\n\t\t\t\t\t\t<div class=\"flex noFlexShrink weatherDetailItem\">\r\n\t\t\t\t\t\t\tWind<br/>");
    #line default
            try
            {
    #line 46 "ConditionsView"
                                         Output.Write(WindCondition);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${WindCondition}");
            }
    #line hidden
            Output.Write("\r\n\t\t\t\t\t\t</div>\r\n\t\t\t\t\t\t<div class=\"flex noFlexShrink weatherDetailItem\">\r\n\t\t\t\t\t\t\tRain<br/>");
    #line default
            try
            {
    #line 49 "ConditionsView"
                                         Output.Write(RainCondition);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${RainCondition}");
            }
    #line hidden
            Output.Write("\r\n\t\t\t\t\t\t</div>\r\n\t\t\t\t\t\t<div class=\"flex noFlexShrink weatherDetailItem\">\r\n\t\t\t\t\t\t\tHumidity<br/>");
    #line default
            try
            {
    #line 52 "ConditionsView"
                                             Output.Write(HumidityCondition);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${HumidityCondition}");
            }
    #line hidden
            Output.Write("\r\n\t\t\t\t\t\t</div>\r\n\t\t\t\t\t\t<div class=\"flex noFlexShrink weatherDetailItem\">\r\n\t\t\t\t\t\t\tPressure<br/>");
    #line default
            try
            {
    #line 55 "ConditionsView"
                                             Output.Write(PressureCondition);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${PressureCondition}");
            }
    #line hidden
            Output.Write("\r\n\t\t\t\t\t\t</div>\r\n\t\t\t\t\t\t<div class=\"flex flex1\"></div>\r\n\t\t\t\t\t</div>\r\n\t\t\t\t</div>\r\n\t\t\t</div>");
    #line default
    #line 60 "ConditionsView"
                      if (FullWeatherResult.Daily == null || FullWeatherResult.Daily.Data.Count == 0)
    #line default
            {
    #line hidden
                Output.Write("\r\n\t\t\t\t<div class=\"flex medBackground\">\r\n\t\t\t\t\t<div class=\"flex flexBoxColumn flex-center\">\r\n\t\t\t\t\t\t<span id=\"forecastLabel\">Forecast not available</span>\r\n\t\t\t\t\t</div>\r\n\t\t\t\t</div>");
    #line default
            }
            else
    #line default
            {
    #line hidden
                Output.Write("\r\n\t\t\t\t<div class=\"flex medBackground\">\r\n\t\t\t\t\t<div class=\"flex flexBoxColumn flex-center\">\r\n\t\t\t\t\t\t<span id=\"forecastLabel\">Forecast</span>\r\n\t\t\t\t\t\t<div class=\"flex flexBoxRow\">\r\n\t\t\t\t\t\t\t<div class=\"flex flex1\"></div>");
    #line default
    #line 74 "ConditionsView"
                                 int daysToDisplay = 5;
    #line default
                {
    #line 74 "ConditionsView"
                                                       foreach(DarkskyWeatherDataPoint dayForecast in FullWeatherResult.Daily.Data)
    #line default
                    {
    #line hidden
    #line default
    #line 75 "ConditionsView"
                                                                                                                if (daysToDisplay-- > 0)
    #line default
                        {
    #line hidden
                            Output.Write("\r\n                                    <div class=\"flex noFlexShrink weatherDetailItem\">\r\n                                        ");
    #line default
                            try
                            {
    #line 78 "ConditionsView"
                                            Output.Write(dayForecast.Time.ToString("ddd"));
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${dayForecast.Time.ToString(\"ddd\")}");
                            }
    #line hidden
                            Output.Write("<br><img src=\"/views/weather/icons/");
    #line default
                            try
                            {
    #line 78 "ConditionsView"
                                                                                                                  Output.Write(dayForecast.Icon);
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${dayForecast.Icon}");
                            }
    #line hidden
                            Output.Write(".png\" class=\"weatherConditionIcon\"><br>");
    #line default
                            try
                            {
    #line 78 "ConditionsView"
                                                                                                                                                                            Output.Write(dayForecast.ApparentTemperatureHigh.Value.ToString("F0"));
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${dayForecast.ApparentTemperatureHigh.Value.ToString(\"F0\")}");
                            }
    #line hidden
                            Output.Write("°<br>");
    #line default
                            try
                            {
    #line 78 "ConditionsView"
                                                                                                                                                                                                                                            Output.Write(dayForecast.ApparentTemperatureLow.Value.ToString("F0"));
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${dayForecast.ApparentTemperatureLow.Value.ToString(\"F0\")}");
                            }
    #line hidden
                            Output.Write("°\r\n                                    </div>");
    #line default
                        }
    #line hidden
    #line default
                    }
                }
    #line hidden
                Output.Write("\r\n\t\t\t\t\t\t\t<div class=\"flex flex1\"></div>\r\n\t\t\t\t\t\t</div>\r\n\t\t\t\t\t</div>\r\n\t\t\t\t</div>");
    #line default
            }
    #line hidden
            Output.Write("\r\n\t\t\t<div class=\"flex medBackground flex1\"></div>\r\n\t\t\t<div id=\"copyrightField\">Powered by Dark Sky</div>\r\n\t\t</div>\r\n\t</div>\r\n</body>\r\n</html>\r\n");
    #line default
        }
    }
}
