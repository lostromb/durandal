//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using DurandalServices.Instrumentation.Analytics.Charting;
using System.Drawing;
namespace DurandalServices.Instrumentation.Analytics.Html
{
    public class DateTimeSingleLineChart
    {
        private StringWriter Output;
        public string ChartId {get; set;}
        public string Label {get; set;}
        public float Tension {get; set;}
        public IEnumerable<DateTimeChartPoint> Data {get; set;}
        public Color LineColor {get; set;}
        public DateTimeOffset MinTime {get; set;}
        public DateTimeOffset MaxTime {get; set;}
        public DateTimeSingleLineChart()
        {
            ChartId = "chart";
            Label = "Value";
            Tension = 0.2f;
            LineColor = Color.FromArgb(0, 255, 255);
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
            Output.Write("<canvas id=\"");
    #line default
            try
            {
    #line 1 "DateTimeSingleLineChart"
                Output.Write(ChartId);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${ChartId}");
            }
    #line hidden
            Output.Write("\"></canvas>\r\n<script>\r\nvar ctx = document.getElementById(\"");
    #line default
            try
            {
    #line 3 "DateTimeSingleLineChart"
                                       Output.Write(ChartId);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${ChartId}");
            }
    #line hidden
            Output.Write("\").getContext('2d');\r\nvar myChart = new Chart(ctx, {\r\n    type: 'line',\r\n    data: {\r\n        datasets: [\r\n        {\r\n            label: '");
    #line default
            try
            {
    #line 9 "DateTimeSingleLineChart"
                        Output.Write(Label);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Label}");
            }
    #line hidden
            Output.Write("',\r\n            data: [");
    #line default
            {
    #line 10 "DateTimeSingleLineChart"
                       foreach(var point in Data)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                { x: '");
    #line default
                    try
                    {
    #line 12 "DateTimeSingleLineChart"
                          Output.Write(point.Time.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${point.Time.ToUniversalTime().ToString(\"yyyy-MM-ddTHH:mm:ss\")}");
                    }
    #line hidden
                    Output.Write("', y: ");
    #line default
                    try
                    {
    #line 12 "DateTimeSingleLineChart"
                                                                                               Output.Write(point.Value);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${point.Value}");
                    }
    #line hidden
                    Output.Write(" },");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n            ],\r\n            borderWidth: 3,\r\n            borderColor: 'rgba(");
    #line default
            try
            {
    #line 16 "DateTimeSingleLineChart"
                                   Output.Write(LineColor.R);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor.R}");
            }
    #line hidden
            Output.Write(", ");
    #line default
            try
            {
    #line 16 "DateTimeSingleLineChart"
                                                   Output.Write(LineColor.G);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor.G}");
            }
    #line hidden
            Output.Write(", ");
    #line default
            try
            {
    #line 16 "DateTimeSingleLineChart"
                                                                   Output.Write(LineColor.B);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor.B}");
            }
    #line hidden
            Output.Write(", 1.0)',\r\n            pointRadius: 1,\r\n            pointHitRadius: 10,\r\n            fill: false\r\n        }\r\n        ]\r\n    },\r\n    options: {\r\n      responsive: true,\r\n      maintainAspectRatio: false,\r\n      legend: {\r\n        display: false\r\n      },\r\n      elements: {\r\n        line: {\r\n            tension: ");
    #line default
            try
            {
    #line 31 "DateTimeSingleLineChart"
                         Output.Write(Tension);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Tension}");
            }
    #line hidden
            Output.Write("\r\n        }\r\n      },\r\n      title: {\r\n        display: true,\r\n        text: '");
    #line default
            try
            {
    #line 36 "DateTimeSingleLineChart"
                   Output.Write(Label);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Label}");
            }
    #line hidden
            Output.Write("'\r\n      },\r\n      scales: {\r\n        xAxes: [{\r\n          type: \"time\",\r\n          time: {\r\n            displayFormats: {\r\n              day: \"M-D\",\r\n              hour: \"M-D ha\"\r\n            },\r\n            //min: \"");
    #line default
            try
            {
    #line 46 "DateTimeSingleLineChart"
                        Output.Write(MinTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${MinTime.ToUniversalTime().ToString(\"yyyy-MM-ddTHH:mm:ss\")}");
            }
    #line hidden
            Output.Write("\",\r\n            //max: \"");
    #line default
            try
            {
    #line 47 "DateTimeSingleLineChart"
                        Output.Write(MaxTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${MaxTime.ToUniversalTime().ToString(\"yyyy-MM-ddTHH:mm:ss\")}");
            }
    #line hidden
            Output.Write("\"\r\n          }\r\n        }],\r\n        yAxes: [{\r\n          ticks: {\r\n            beginAtZero:true\r\n          }\r\n        }]\r\n      }\r\n    }\r\n});\r\n</script>\r\n");
    #line default
        }
    }
}
