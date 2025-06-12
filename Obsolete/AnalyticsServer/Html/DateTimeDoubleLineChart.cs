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
    public class DateTimeDoubleLineChart
    {
        private StringWriter Output;
        public string ChartId {get; set;}
        public string ChartTitle {get; set;}
        public string Label1 {get; set;}
        public string Label2 {get; set;}
        public float Tension {get; set;}
        public IEnumerable<DateTimeChartPoint> Data1 {get; set;}
        public Color LineColor1 {get; set;}
        public IEnumerable<DateTimeChartPoint> Data2 {get; set;}
        public Color LineColor2 {get; set;}
        public DateTimeOffset MinTime {get; set;}
        public DateTimeOffset MaxTime {get; set;}
        public DateTimeDoubleLineChart()
        {
            ChartId = "chart";
            ChartTitle = "Value";
            Label1 = "Series1";
            Label2 = "Series2";
            Tension = 0.2f;
            LineColor1 = Color.FromArgb(0, 255, 255);
            LineColor2 = Color.FromArgb(255, 0, 255);
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
    #line 1 "DateTimeDoubleLineChart"
                Output.Write(ChartId);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${ChartId}");
            }
    #line hidden
            Output.Write("\"></canvas>\r\n<script>\r\n    var ctx = document.getElementById(\"");
    #line default
            try
            {
    #line 3 "DateTimeDoubleLineChart"
                                           Output.Write(ChartId);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${ChartId}");
            }
    #line hidden
            Output.Write("\").getContext('2d');\r\n    var myChart = new Chart(ctx, {\r\n        type: 'line',\r\n        data: {\r\n            datasets: [\r\n            {\r\n                label: '");
    #line default
            try
            {
    #line 9 "DateTimeDoubleLineChart"
                            Output.Write(Label1);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Label1}");
            }
    #line hidden
            Output.Write("',\r\n                data: [");
    #line default
            {
    #line 10 "DateTimeDoubleLineChart"
                           foreach(var point in Data1)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                    { x: '");
    #line default
                    try
                    {
    #line 12 "DateTimeDoubleLineChart"
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
    #line 12 "DateTimeDoubleLineChart"
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
            Output.Write("\r\n                ],\r\n                borderWidth: 3,\r\n                borderColor: 'rgba(");
    #line default
            try
            {
    #line 16 "DateTimeDoubleLineChart"
                                       Output.Write(LineColor1.R);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor1.R}");
            }
    #line hidden
            Output.Write(", ");
    #line default
            try
            {
    #line 16 "DateTimeDoubleLineChart"
                                                        Output.Write(LineColor1.G);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor1.G}");
            }
    #line hidden
            Output.Write(", ");
    #line default
            try
            {
    #line 16 "DateTimeDoubleLineChart"
                                                                         Output.Write(LineColor1.B);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor1.B}");
            }
    #line hidden
            Output.Write(", 1.0)',\r\n                pointRadius: 1,\r\n                pointHitRadius: 10,\r\n                fill: false\r\n            },\r\n            {\r\n                label: '");
    #line default
            try
            {
    #line 22 "DateTimeDoubleLineChart"
                            Output.Write(Label2);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Label2}");
            }
    #line hidden
            Output.Write("',\r\n                data: [");
    #line default
            {
    #line 23 "DateTimeDoubleLineChart"
                           foreach(var point in Data2)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                    { x: '");
    #line default
                    try
                    {
    #line 25 "DateTimeDoubleLineChart"
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
    #line 25 "DateTimeDoubleLineChart"
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
            Output.Write("\r\n                ],\r\n                borderWidth: 3,\r\n                borderColor: 'rgba(");
    #line default
            try
            {
    #line 29 "DateTimeDoubleLineChart"
                                       Output.Write(LineColor2.R);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor2.R}");
            }
    #line hidden
            Output.Write(", ");
    #line default
            try
            {
    #line 29 "DateTimeDoubleLineChart"
                                                        Output.Write(LineColor2.G);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor2.G}");
            }
    #line hidden
            Output.Write(", ");
    #line default
            try
            {
    #line 29 "DateTimeDoubleLineChart"
                                                                         Output.Write(LineColor2.B);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor2.B}");
            }
    #line hidden
            Output.Write(", 1.0)',\r\n                pointRadius: 1,\r\n                pointHitRadius: 10,\r\n                fill: false\r\n                }\r\n        ]\r\n    },\r\n    options: {\r\n            responsive: true,\r\n            maintainAspectRatio: false,\r\n            legend: {\r\n            display: false\r\n            },\r\n        elements: {\r\n                line: {\r\n                    tension: ");
    #line default
            try
            {
    #line 44 "DateTimeDoubleLineChart"
                                 Output.Write(Tension);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Tension}");
            }
    #line hidden
            Output.Write("\r\n                }\r\n        },\r\n        title: {\r\n                display: true,\r\n                text: '");
    #line default
            try
            {
    #line 49 "DateTimeDoubleLineChart"
                           Output.Write(ChartTitle);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${ChartTitle}");
            }
    #line hidden
            Output.Write("'\r\n        },\r\n        scales: {\r\n                xAxes: [{\r\n                    type: \"time\",\r\n                    time: {\r\n                        displayFormats: {\r\n                            day: \"M-D\",\r\n                            hour: \"M-D ha\"\r\n                        },\r\n                        //min: \"");
    #line default
            try
            {
    #line 59 "DateTimeDoubleLineChart"
                                    Output.Write(MinTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${MinTime.ToUniversalTime().ToString(\"yyyy-MM-ddTHH:mm:ss\")}");
            }
    #line hidden
            Output.Write("\",\r\n                        //max: \"");
    #line default
            try
            {
    #line 60 "DateTimeDoubleLineChart"
                                    Output.Write(MaxTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${MaxTime.ToUniversalTime().ToString(\"yyyy-MM-ddTHH:mm:ss\")}");
            }
    #line hidden
            Output.Write("\"\r\n                    }\r\n                }],\r\n                yAxes: [{\r\n                    ticks: {\r\n                        beginAtZero:true\r\n                    }\r\n                }]\r\n        }\r\n    }\r\n    });\r\n</script>\r\n");
    #line default
        }
    }
}
