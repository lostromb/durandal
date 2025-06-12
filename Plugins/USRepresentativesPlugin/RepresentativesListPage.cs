//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace Durandal.Plugins.Plugins.USRepresentatives
{
    public class RepresentativesListPage
    {
        private StringWriter Output;
        public IEnumerable<Legislator> Legislators {get; set;}
        public string UserState {get; set;}
        public int UserRepresentativeDistrict {get; set;}
        public RepresentativesListPage()
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
            Output.Write("<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\">\r\n    <link href=\"/views/common/flexbox.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    <link href=\"/views/common/global_html5.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    <link href=\"/views/common/pure-base-min.css\" rel=\"stylesheet\" type=\"text/css\">\r\n    <style>\r\n        body {\r\n            padding: 0px;\r\n            margin: 0px;\r\n        }\r\n\r\n        .container {\r\n            background: #7d7e7d;\r\n            background: -moz-linear-gradient(top, #7d7e7d 0%, #0e0e0e 100%);\r\n            background: -webkit-linear-gradient(top, #7d7e7d 0%,#0e0e0e 100%);\r\n            background: linear-gradient(to bottom, #7d7e7d 0%,#0e0e0e 100%);\r\n            filter: progid:DXImageTransform.Microsoft.gradient( startColorstr='#7d7e7d', endColorstr='#0e0e0e',GradientType=0 );\r\n        }\r\n\r\n        .headerLine {\r\n            color: white;\r\n            font-size: 24pt;\r\n            padding-left: 10px;\r\n            padding-top: 20px;\r\n            padding-bottom: 20px;\r\n        }\r\n\r\n        .personBlock {\r\n            margin-left: 20px;\r\n            margin-right: 20px;\r\n            background-color: white;\r\n            z-index: -50;\r\n        }\r\n\r\n        .repName {\r\n            font-size: 30pt;\r\n            display: block;\r\n        }\r\n\r\n        .democrat {\r\n            width: 30px;\r\n            background-color: blue;\r\n        }\r\n\r\n        .republican {\r\n            width: 30px;\r\n            background-color: red;\r\n        }\r\n\r\n        .personInfo {\r\n        }\r\n\r\n        .portrait {\r\n            width: 200px;\r\n            height: auto;\r\n            display: block;\r\n        }\r\n    </style>\r\n    <title>Outlook</title>\r\n</head>\r\n<body>\r\n    <div class=\"container flex flexAppContainer\">\r\n        <div class=\"flex flexBoxColumn flex-stretch\">\r\n            <span class=\"headerLine\">Senate</span>");
    #line default
            {
    #line 66 "RepresentativesListPage"
                                                      foreach(var legislator in Legislators)
    #line default
                {
    #line hidden
    #line default
    #line 67 "RepresentativesListPage"
                                                          if (legislator.Office == GovernmentOffice.Senate)
    #line default
                    {
    #line hidden
                        Output.Write("\r\n                    <div class=\"personBlock\">\r\n                        <div class=\"flex flexBoxRow\">\r\n                            <div>\r\n                                <img src=\"");
    #line default
                        try
                        {
    #line 72 "RepresentativesListPage"
                                              Output.Write(legislator.PortaitThumbnail);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${legislator.PortaitThumbnail}");
                        }
    #line hidden
                        Output.Write("\" class=\"portrait\">\r\n                                <div class=\"republican\"></div>\r\n                            </div>\r\n                            <div class=\"flex5 personInfo\">\r\n                                <span class=\"repName\">");
    #line default
                        try
                        {
    #line 76 "RepresentativesListPage"
                                                          Output.Write(legislator.FullName);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${legislator.FullName}");
                        }
    #line hidden
                        Output.Write("</span>\r\n                                Age: 67<br/>\r\n                                Years in office: 12 (Since 2005)<br/>\r\n                                Phone: ");
    #line default
                        try
                        {
    #line 79 "RepresentativesListPage"
                                           Output.Write(legislator.PhoneNumber);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${legislator.PhoneNumber}");
                        }
    #line hidden
                        Output.Write("<br/>\r\n                                Fax: ");
    #line default
                        try
                        {
    #line 80 "RepresentativesListPage"
                                         Output.Write(legislator.FaxNumber);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${legislator.FaxNumber}");
                        }
    #line hidden
                        Output.Write("<br/>\r\n                                Address: ");
    #line default
                        try
                        {
    #line 81 "RepresentativesListPage"
                                             Output.Write(legislator.CapitolAddress);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${legislator.CapitolAddress}");
                        }
    #line hidden
                        Output.Write(", Washington D.C.<br/>\r\n                                <a href=\"http://www.cantwell.senate.gov/public/index.cfm/email-maria\">Website</a><br/>\r\n                                Contact via</br>\r\n                                <a href=\"http://www.cantwell.senate.gov/public/index.cfm/email-maria\">Email</a>\r\n                                <a href=\"http://www.cantwell.senate.gov/public/index.cfm/email-maria\">Facebook</a>\r\n                                <a href=\"http://www.cantwell.senate.gov/public/index.cfm/email-maria\">Twitter</a>\r\n                                <a href=\"http://www.cantwell.senate.gov/public/index.cfm/email-maria\">Youtube</a>\r\n                            </div>\r\n                        </div>\r\n                    </div>");
    #line default
                    }
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n                <span class=\"headerLine\">House of Representatives - ");
    #line default
            try
            {
    #line 93 "RepresentativesListPage"
                                                                        Output.Write(UserState);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${UserState}");
            }
    #line hidden
            Output.Write(" - District ");
    #line default
            try
            {
    #line 93 "RepresentativesListPage"
                                                                                                Output.Write(UserRepresentativeDistrict);
    #line default
            }
            catch(System.NullReferenceException)
            {
                Output.Write("${UserRepresentativeDistrict}");
            }
    #line hidden
            Output.Write("</span>");
    #line default
            {
    #line 93 "RepresentativesListPage"
                                                                                                                                    foreach(var legislator in Legislators)
    #line default
                {
    #line hidden
    #line default
    #line 94 "RepresentativesListPage"
                                                          if (legislator.Office == GovernmentOffice.HouseOfRepresentatives)
    #line default
                    {
    #line hidden
                        Output.Write("\r\n                    <div class=\"personBlock\">\r\n                        <div class=\"flex flexBoxRow\">\r\n                            <div>\r\n                                <img src=\"");
    #line default
                        try
                        {
    #line 99 "RepresentativesListPage"
                                              Output.Write(legislator.PortaitThumbnail);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${legislator.PortaitThumbnail}");
                        }
    #line hidden
                        Output.Write("\" class=\"portrait\">\r\n                                <div class=\"republican\"></div>\r\n                            </div>\r\n                            <div class=\"flex5 personInfo\">\r\n                                <span class=\"repName\">");
    #line default
                        try
                        {
    #line 103 "RepresentativesListPage"
                                                          Output.Write(legislator.FullName);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${legislator.FullName}");
                        }
    #line hidden
                        Output.Write("</span>\r\n                                Age: 67<br/>\r\n                                Years in office: 12 (Since 2005)<br/>\r\n                                Phone: ");
    #line default
                        try
                        {
    #line 106 "RepresentativesListPage"
                                           Output.Write(legislator.PhoneNumber);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${legislator.PhoneNumber}");
                        }
    #line hidden
                        Output.Write("<br/>\r\n                                Fax: ");
    #line default
                        try
                        {
    #line 107 "RepresentativesListPage"
                                         Output.Write(legislator.FaxNumber);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${legislator.FaxNumber}");
                        }
    #line hidden
                        Output.Write("<br/>\r\n                                Address: ");
    #line default
                        try
                        {
    #line 108 "RepresentativesListPage"
                                             Output.Write(legislator.CapitolAddress);
    #line default
                        }
                        catch(System.NullReferenceException)
                        {
                            Output.Write("${legislator.CapitolAddress}");
                        }
    #line hidden
                        Output.Write(", Washington D.C.<br/>\r\n                                <a href=\"http://www.cantwell.senate.gov/public/index.cfm/email-maria\">Website</a><br/>\r\n                                Contact via</br>\r\n                                <a href=\"http://www.cantwell.senate.gov/public/index.cfm/email-maria\">Email</a>\r\n                                <a href=\"http://www.cantwell.senate.gov/public/index.cfm/email-maria\">Facebook</a>\r\n                                <a href=\"http://www.cantwell.senate.gov/public/index.cfm/email-maria\">Twitter</a>\r\n                                <a href=\"http://www.cantwell.senate.gov/public/index.cfm/email-maria\">Youtube</a>\r\n                            </div>\r\n                        </div>\r\n                    </div>");
    #line default
                    }
    #line hidden
    #line default
                }
            }
    #line hidden
            Output.Write("\r\n        </div>\r\n    </div>\r\n</body>\r\n</html>\r\n");
    #line default
        }
    }
}
