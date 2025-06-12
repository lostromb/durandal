//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using Durandal.Common.Monitoring;
using System.Linq;
namespace Photon.StatusReporter.Razor
{
    public class AlertPage
    {
        private StringWriter Output;
        public IEnumerable<SuiteAlertStatus> AllAlertStatus {get; set;}
        public AlertPage()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n    <head>\r\n        <link href=\"/content/pure-min.css\" rel=\"stylesheet\">\r\n        <link href=\"/content/flexbox.css\" rel=\"stylesheet\">\r\n        <link href=\"/content/dashboard.css\" rel=\"stylesheet\">\r\n        <script src=\"/content/jquery-3.2.1.min.js\"></script>");
    #line default
            {
    #line 7 "AlertPage"
                                                                foreach(SuiteAlertStatus suiteStatus in AllAlertStatus)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n            <script>\r\n                $(document).ready(function () {\r\n                    $(\"#suite_");
    #line default
                    try
                    {
    #line 11 "AlertPage"
                                  Output.Write(suiteStatus.SuiteName);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${suiteStatus.SuiteName}");
                    }
    #line hidden
                    Output.Write("_header\").click(function () {\r\n                        $(\"#suite_");
    #line default
                    try
                    {
    #line 12 "AlertPage"
                                      Output.Write(suiteStatus.SuiteName);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${suiteStatus.SuiteName}");
                    }
    #line hidden
                    Output.Write("_div\").toggle(200, \"linear\");\r\n                    });\r\n\r\n                    $(\"#suite_");
    #line default
                    try
                    {
    #line 15 "AlertPage"
                                  Output.Write(suiteStatus.SuiteName);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${suiteStatus.SuiteName}");
                    }
    #line hidden
                    Output.Write("_div\").hide();\r\n                });\r\n            </script>");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n        <script>\r\n\r\n        madeChanges = false;\r\n        changedTests = {};\r\n\r\n        toggleSaveButton = function(enabled)\r\n        {\r\n          if (enabled)\r\n          {\r\n            $(\"#saveButton\").text(\"Save changes\");\r\n            $(\"#saveButton\").removeClass(\"buttonDisabled\");\r\n            $(\"#saveButton\").addClass(\"buttonEnabled\");\r\n            document.getElementById(\"saveButton\").disabled = false;\r\n          }\r\n          else\r\n          {\r\n            $(\"#saveButton\").addClass(\"buttonDisabled\");\r\n            $(\"#saveButton\").removeClass(\"buttonEnabled\");\r\n            document.getElementById(\"saveButton\").disabled = true;\r\n          }\r\n        }\r\n\r\n        selectionChanged = function(source)\r\n        {\r\n          name = source.srcElement.name;\r\n          value = source.srcElement.selectedIndex;\r\n          //console.log(\"Changed \" + name + \" to \" + value);\r\n          madeChanges = true;\r\n          toggleSaveButton(madeChanges);\r\n          changedTests[name] = value;\r\n          console.log(changedTests);\r\n        }\r\n\r\n        $(document).ready(\r\n          function()\r\n          {\r\n              toggleSaveButton(false);\r\n              ");
    #line default
    #line 57 "AlertPage"
                    foreach (SuiteAlertStatus suiteStatus in AllAlertStatus) {
    #line default
    #line 58 "AlertPage"
                      foreach (TestAlertStatus testStatus in suiteStatus.TestStatus.Values) {
    #line default
    #line hidden
            Output.Write("\r\n              document.getElementById(\"alertLevel_");
    #line default
            try
            {
    #line 59 "AlertPage"
                                                      Output.Write(testStatus.TestName);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${testStatus.TestName}");
            }
    #line hidden
            Output.Write("\").addEventListener(\"change\", selectionChanged);");
    #line default
    #line 60 "AlertPage"
                      }
    #line default
    #line 61 "AlertPage"
                    }
    #line default
    #line hidden
            Output.Write("\r\n\r\n            $(\"#saveButton\").click(\r\n              function()\r\n              {\r\n                if (madeChanges)\r\n                {\r\n                  $(\"#saveButton\").text(\"Saving...\");\r\n                  $.ajax({\r\n                    type: \"POST\",\r\n                    url: \"/api/alerts/updateAlertConfig\",\r\n                    data: changedTests,\r\n                    error: function(data, status)\r\n                    {\r\n                      $(\"#saveButton\").text(\"Failed to save\");\r\n                    },\r\n                    success: function(data, status)\r\n                    {\r\n                      madeChanges = false;\r\n                      changedTests = {};\r\n                      toggleSaveButton(madeChanges);\r\n                      $(\"#saveButton\").text(\"Saved!\");\r\n                    }\r\n                  });\r\n                }\r\n              }\r\n            );\r\n          }\r\n        );\r\n\r\n        </script>\r\n    </head>\r\n    <body>\r\n        <div class=\"flex flex-center flexBoxColumnFlow\">\r\n            ");
    #line default
            try
            {
    #line 95 "AlertPage"
                Output.Write(new CommonHeader().Render());
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${new CommonHeader().Render()}");
            }
    #line hidden
            Output.Write("\r\n            <span class=\"inlineHeader\">Alerting Configuration</span>\r\n            <button id=\"saveButton\" class=\"buttonDisabled\" disabled>Save changes</button>\r\n            <br/>");
    #line default
            {
    #line 98 "AlertPage"
                      foreach(SuiteAlertStatus suiteStatus in AllAlertStatus)
    #line default
                {
    #line hidden
    #line default
    #line hidden
                    Output.Write("\r\n                <a href=\"#\" class=\"hiddenLink suiteHeader suiteHeaderPassing flex flex-center flexBoxColumn\" id=\"suite_");
    #line default
                    try
                    {
    #line 100 "AlertPage"
                                                                                                                           Output.Write(suiteStatus.SuiteName);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${suiteStatus.SuiteName}");
                    }
    #line hidden
                    Output.Write("_header\">\r\n                    <div>\r\n                        <span>");
    #line default
                    try
                    {
    #line 102 "AlertPage"
                                  Output.Write(suiteStatus.SuiteName);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${suiteStatus.SuiteName}");
                    }
    #line hidden
                    Output.Write("</span>\r\n                    </div>\r\n                </a>\r\n                <div class=\"suite flex flex-center flexBoxColumn\" id=\"suite_");
    #line default
                    try
                    {
    #line 105 "AlertPage"
                                                                                Output.Write(suiteStatus.SuiteName);
    #line default
                    }
                    catch(System.NullReferenceException)
                    {
                        Output.Write("${suiteStatus.SuiteName}");
                    }
    #line hidden
                    Output.Write("_div\">\r\n                    <table class=\"pure-table pure-table-bordered\">\r\n                        <thead>\r\n                            <tr>\r\n                                <th>Test Name</th>\r\n                                <th>Last Alert Level</th>\r\n                                <th>Last Incident</th>\r\n                                <th>Last Incident Duration</th>\r\n                                <th>Configured Alert Level</th>\r\n                                <th>Owning Team</th>\r\n                            </tr>\r\n                        </thead>\r\n                        <tbody>");
    #line default
                    {
    #line 117 "AlertPage"
                                   foreach(TestAlertStatus testStatus in suiteStatus.TestStatus.Values)
    #line default
                        {
    #line hidden
    #line default
    #line hidden
                            Output.Write("\r\n                                <tr>\r\n                                    <!-- test name -->\r\n                                    <td><a href=\"/dashboard/test/");
    #line default
                            try
                            {
    #line 121 "AlertPage"
                                                                     Output.Write(testStatus.TestName);
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${testStatus.TestName}");
                            }
    #line hidden
                            Output.Write("\">");
    #line default
                            try
                            {
    #line 121 "AlertPage"
                                                                                             Output.Write(testStatus.TestName);
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${testStatus.TestName}");
                            }
    #line hidden
                            Output.Write("</a></td>\r\n                                    <!-- last incident level -->\r\n                                    <!-- color it red if the end of the incident is within the last 15 minutes -->");
    #line default
    #line 123 "AlertPage"
                                                                                                                      if (testStatus.IsIncidentCurrent)
    #line default
                            {
    #line hidden
                                Output.Write("\r\n                                        <td style=\"background-color: red\">");
    #line default
                            }
                            else
    #line default
                            {
    #line hidden
                                Output.Write("\r\n                                        <td>");
    #line default
                            }
    #line hidden
                            Output.Write("\r\n                                    ");
    #line default
                            try
                            {
    #line 130 "AlertPage"
                                        Output.Write(TestAlertStatus.FormatAlertLevel(testStatus.MostRecentFailureLevel));
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${TestAlertStatus.FormatAlertLevel(testStatus.MostRecentFailureLevel)}");
                            }
    #line hidden
                            Output.Write("\r\n                                    </td>\r\n                                    <!-- last incident -->\r\n                                    <td>");
    #line default
                            try
                            {
    #line 133 "AlertPage"
                                            Output.Write(TestAlertStatus.FormatDateTimeRecency(testStatus.MostRecentFailureBegin));
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${TestAlertStatus.FormatDateTimeRecency(testStatus.MostRecentFailureBegin)}");
                            }
    #line hidden
                            Output.Write("</td>\r\n                                    <!-- last incident duration -->\r\n                                    <td>");
    #line default
                            try
                            {
    #line 135 "AlertPage"
                                            Output.Write(testStatus.FormatIncidentDuration());
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${testStatus.FormatIncidentDuration()}");
                            }
    #line hidden
                            Output.Write("</td>\r\n                                    <!-- configured alert level -->\r\n                                    <td>\r\n                                        <select id=\"alertLevel_");
    #line default
                            try
                            {
    #line 138 "AlertPage"
                                                                   Output.Write(testStatus.TestName);
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${testStatus.TestName}");
                            }
    #line hidden
                            Output.Write("\" name=\"");
    #line default
                            try
                            {
    #line 138 "AlertPage"
                                                                                                 Output.Write(testStatus.TestName);
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${testStatus.TestName}");
                            }
    #line hidden
                            Output.Write("\">\r\n                                            <option value=\"NoAlert\"");
    #line default
                            try
                            {
    #line 139 "AlertPage"
                                                                       Output.Write(testStatus.DefaultFailureLevel == AlertLevel.NoAlert ? " selected" : "");
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${testStatus.DefaultFailureLevel == AlertLevel.NoAlert ? \" selected\" : \"\"}");
                            }
    #line hidden
                            Output.Write(">No Alerts</option>\r\n                                            <option value=\"Mute\"");
    #line default
                            try
                            {
    #line 140 "AlertPage"
                                                                    Output.Write(testStatus.DefaultFailureLevel == AlertLevel.Mute ? " selected" : "");
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${testStatus.DefaultFailureLevel == AlertLevel.Mute ? \" selected\" : \"\"}");
                            }
    #line hidden
                            Output.Write(">Mute</option>\r\n                                            <option value=\"Notify\"");
    #line default
                            try
                            {
    #line 141 "AlertPage"
                                                                      Output.Write(testStatus.DefaultFailureLevel == AlertLevel.Notify ? " selected" : "");
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${testStatus.DefaultFailureLevel == AlertLevel.Notify ? \" selected\" : \"\"}");
                            }
    #line hidden
                            Output.Write(">Notify</option>\r\n                                            <option value=\"Alert\"");
    #line default
                            try
                            {
    #line 142 "AlertPage"
                                                                     Output.Write(testStatus.DefaultFailureLevel == AlertLevel.Alert ? " selected" : "");
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${testStatus.DefaultFailureLevel == AlertLevel.Alert ? \" selected\" : \"\"}");
                            }
    #line hidden
                            Output.Write(">Alert</option>\r\n                                        </select>\r\n                                    </td>\r\n                                    <!-- team name -->\r\n                                    <td>");
    #line default
                            try
                            {
    #line 146 "AlertPage"
                                            Output.Write(testStatus.OwningTeamName ?? "Not set");
    #line default
                            }
                            catch(System.NullReferenceException)
                            {
                                Output.Write("${testStatus.OwningTeamName ?? \"Not set\"}");
                            }
    #line hidden
                            Output.Write("</td>\r\n                                </tr>");
    #line default
    #line hidden
    #line default
                        }
                    }
    #line hidden
                    Output.Write("\r\n                        </tbody>\r\n                    </table>\r\n                </div>");
    #line default
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n        </div>\r\n    </body>\r\n</html>\r\n");
    #line default
        }
    }
}
