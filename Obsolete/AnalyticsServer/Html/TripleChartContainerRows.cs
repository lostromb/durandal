//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace DurandalServices.Instrumentation.Analytics.Html
{
    public class TripleChartContainerRows
    {
        private StringWriter Output;
        public string ChartElement1 {get; set;}
        public string ChartElement2 {get; set;}
        public string ChartElement3 {get; set;}
        public TripleChartContainerRows()
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
            Output.Write("<div class=\"flex flexBoxColumn\">\r\n    <div class=\"flex flex1 chart\">\r\n        ");
    #line default
            try
            {
    #line 3 "TripleChartContainerRows"
            Output.Write(ChartElement1);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${ChartElement1}");
            }
    #line hidden
            Output.Write("\r\n    </div>\r\n    <div class=\"flex flex1 chart\">\r\n        ");
    #line default
            try
            {
    #line 6 "TripleChartContainerRows"
            Output.Write(ChartElement2);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${ChartElement2}");
            }
    #line hidden
            Output.Write("\r\n    </div>\r\n    <div class=\"flex flex1 chart\">\r\n        ");
    #line default
            try
            {
    #line 9 "TripleChartContainerRows"
            Output.Write(ChartElement3);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${ChartElement3}");
            }
    #line hidden
            Output.Write("\r\n    </div>\r\n</div>\r\n");
    #line default
        }
    }
}
