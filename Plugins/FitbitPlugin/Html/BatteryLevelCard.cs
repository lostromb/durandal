//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace RazorViews
{
    public class BatteryLevelCard
    {
        private StringWriter Output;
        public BatteryLevelCard()
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
            Output.Write("<div class=\"container\">\r\n  <div class=\"container-background\"></div>\r\n  <div class=\"container-border\">\r\n    <div class=\"battery_status_container\">\r\n      <div class=\"battery_message\">\r\n        <p class=\"title\">\r\n          <span class=\"device_name\">{{entity.device_name}}</span> <span>battery level</span>\r\n        </p>\r\n        <div class=\"battery_status\" *ngIf=\"entity.battery_level == \\\"Full\\\"> \r\n          <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_battery_full.png\">\r\n        </div>\r\n        <div class=\"battery_status\" *ngIf=\"entity.battery_level == \\\"Medium\\\"> \r\n          <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_battery_medium.png\">\r\n        </div>\r\n        <div class=\"battery_status\" *ngIf=\"entity.battery_level == \\\"Low\\\"> \r\n          <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_battery_low.png\">\r\n        </div>\r\n        <div class=\"battery_status\" *ngIf=\"entity.battery_level == \\\"Empty\\\"> \r\n          <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_battery_empty.png\">\r\n        </div>\r\n      </div>\r\n    </div>\r\n  </div>\r\n</div>\r\n");
    #line default
        }
    }
}
