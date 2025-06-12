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
    public class DateTimePercentileLineChart
    {
        private StringWriter Output;
        public string ChartId {get; set;}
        public string Label {get; set;}
        public float Tension {get; set;}
        public IEnumerable<DateTimePercentileChartPoint> Data {get; set;}
        public Color LineColor {get; set;}
        public DateTimeOffset MinTime {get; set;}
        public DateTimeOffset MaxTime {get; set;}
        public DateTimePercentileLineChart()
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
    #line 1 "DateTimePercentileLineChart"
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
    #line 3 "DateTimePercentileLineChart"
                                       Output.Write(ChartId);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${ChartId}");
            }
    #line hidden
            Output.Write("\").getContext('2d');\r\nvar myChart = new Chart(ctx, {\r\n    type: 'line',\r\n    data: {\r\n        datasets: [\r\n            {\r\n                label: '");
    #line default
            try
            {
    #line 9 "DateTimePercentileLineChart"
                            Output.Write(Label);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Label}");
            }
    #line hidden
            Output.Write("-P5',\r\n                data: [");
    #line default
            {
    #line 10 "DateTimePercentileLineChart"
                           foreach(var point in Data)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                    { x: '");
    #line default
                    try
                    {
    #line 12 "DateTimePercentileLineChart"
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
    #line 12 "DateTimePercentileLineChart"
                                                                                                   Output.Write(point.PercentileP5);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${point.PercentileP5}");
                    }
    #line hidden
                    Output.Write(" },");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n                ],\r\n                borderWidth: 1,\r\n                borderColor: 'rgba(");
    #line default
            try
            {
    #line 16 "DateTimePercentileLineChart"
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
    #line 16 "DateTimePercentileLineChart"
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
    #line 16 "DateTimePercentileLineChart"
                                                                       Output.Write(LineColor.B);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor.B}");
            }
    #line hidden
            Output.Write(", 0.5)',\r\n                pointRadius: 1,\r\n                pointHitRadius: 10,\r\n                fill: false\r\n            },\r\n            {\r\n                label: '");
    #line default
            try
            {
    #line 22 "DateTimePercentileLineChart"
                            Output.Write(Label);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Label}");
            }
    #line hidden
            Output.Write("-P25',\r\n                data: [");
    #line default
            {
    #line 23 "DateTimePercentileLineChart"
                           foreach(var point in Data)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                    { x: '");
    #line default
                    try
                    {
    #line 25 "DateTimePercentileLineChart"
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
    #line 25 "DateTimePercentileLineChart"
                                                                                                   Output.Write(point.PercentileP25);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${point.PercentileP25}");
                    }
    #line hidden
                    Output.Write(" },");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n                ],\r\n                borderWidth: 2,\r\n                borderColor: 'rgba(");
    #line default
            try
            {
    #line 29 "DateTimePercentileLineChart"
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
    #line 29 "DateTimePercentileLineChart"
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
    #line 29 "DateTimePercentileLineChart"
                                                                       Output.Write(LineColor.B);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor.B}");
            }
    #line hidden
            Output.Write(", 0.75)',\r\n                pointRadius: 1,\r\n                pointHitRadius: 10,\r\n                fill: false\r\n            },\r\n            {\r\n                label: '");
    #line default
            try
            {
    #line 35 "DateTimePercentileLineChart"
                            Output.Write(Label);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Label}");
            }
    #line hidden
            Output.Write("-P50',\r\n                data: [");
    #line default
            {
    #line 36 "DateTimePercentileLineChart"
                           foreach(var point in Data)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                    { x: '");
    #line default
                    try
                    {
    #line 38 "DateTimePercentileLineChart"
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
    #line 38 "DateTimePercentileLineChart"
                                                                                                   Output.Write(point.PercentileP50);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${point.PercentileP50}");
                    }
    #line hidden
                    Output.Write(" },");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n                ],\r\n                borderWidth: 4,\r\n                borderColor: 'rgba(");
    #line default
            try
            {
    #line 42 "DateTimePercentileLineChart"
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
    #line 42 "DateTimePercentileLineChart"
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
    #line 42 "DateTimePercentileLineChart"
                                                                       Output.Write(LineColor.B);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor.B}");
            }
    #line hidden
            Output.Write(", 1.0)',\r\n                pointRadius: 2,\r\n                pointHitRadius: 10,\r\n                fill: false\r\n            },\r\n            {\r\n                label: '");
    #line default
            try
            {
    #line 48 "DateTimePercentileLineChart"
                            Output.Write(Label);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Label}");
            }
    #line hidden
            Output.Write("-P75',\r\n                data: [");
    #line default
            {
    #line 49 "DateTimePercentileLineChart"
                           foreach(var point in Data)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                    { x: '");
    #line default
                    try
                    {
    #line 51 "DateTimePercentileLineChart"
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
    #line 51 "DateTimePercentileLineChart"
                                                                                                   Output.Write(point.PercentileP75);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${point.PercentileP75}");
                    }
    #line hidden
                    Output.Write(" },");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n                ],\r\n                borderWidth: 2,\r\n                borderColor: 'rgba(");
    #line default
            try
            {
    #line 55 "DateTimePercentileLineChart"
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
    #line 55 "DateTimePercentileLineChart"
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
    #line 55 "DateTimePercentileLineChart"
                                                                       Output.Write(LineColor.B);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor.B}");
            }
    #line hidden
            Output.Write(", 0.75)',\r\n                pointRadius: 1,\r\n                pointHitRadius: 10,\r\n                fill: false\r\n            },\r\n            {\r\n                label: '");
    #line default
            try
            {
    #line 61 "DateTimePercentileLineChart"
                            Output.Write(Label);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${Label}");
            }
    #line hidden
            Output.Write("-P95',\r\n                data: [");
    #line default
            {
    #line 62 "DateTimePercentileLineChart"
                           foreach(var point in Data)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                    { x: '");
    #line default
                    try
                    {
    #line 64 "DateTimePercentileLineChart"
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
    #line 64 "DateTimePercentileLineChart"
                                                                                                   Output.Write(point.PercentileP95);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${point.PercentileP95}");
                    }
    #line hidden
                    Output.Write(" },");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n                ],\r\n                borderWidth: 1,\r\n                borderColor: 'rgba(");
    #line default
            try
            {
    #line 68 "DateTimePercentileLineChart"
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
    #line 68 "DateTimePercentileLineChart"
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
    #line 68 "DateTimePercentileLineChart"
                                                                       Output.Write(LineColor.B);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${LineColor.B}");
            }
    #line hidden
            Output.Write(", 0.5)',\r\n                pointRadius: 1,\r\n                pointHitRadius: 10,\r\n                fill: false\r\n            }\r\n        ]\r\n    },\r\n    options: {\r\n      responsive: true,\r\n      maintainAspectRatio: false,\r\n      legend: {\r\n        display: false\r\n      },\r\n      elements: {\r\n        line: {\r\n            tension: ");
    #line default
            try
            {
    #line 83 "DateTimePercentileLineChart"
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
    #line 88 "DateTimePercentileLineChart"
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
    #line 98 "DateTimePercentileLineChart"
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
    #line 99 "DateTimePercentileLineChart"
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
