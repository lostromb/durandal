//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace RazorViews
{
    public class FloorsCard
    {
        private StringWriter Output;
        public FloorsCard()
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
            Output.Write("<div class=\"container\">\r\n  <div class=\"container-background\"></div>\r\n  <div class=\"container-border\">\r\n    <p class=\"top_text\">\r\n      <span class=\"day\">{{entity.date}}</span>\r\n    </p>\r\n    <div class=\"data_container\">\r\n      <div class=\"exercise_circle\">\r\n        <div *ngIf=\"entity.percent < 100\" class=\"steps_circle c100 p{{entity.percent}}\">\r\n          <div class=\"slice\">\r\n            <div class=\"bar\"></div>\r\n            <div class=\"fill\"></div>\r\n          </div>\r\n          <div *ngIf=\"entity.stairs_climbed == 0\"> \r\n            <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_floors_50.png\">\r\n          </div>\r\n          <div *ngIf=\"entity.stairs_climbed > 0 && entity.percent < 100\"> \r\n            <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_floors_50.png\">\r\n          </div>\r\n        </div>\r\n        <div *ngIf=\"entity.percent == 100\" class=\"steps_circle c100 p100 green\">\r\n          <div class=\"slice\">\r\n            <div class=\"bar\"></div>\r\n            <div class=\"fill\"></div>\r\n          </div>\r\n          <div> \r\n            <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_floors_goal_50.png\">\r\n          </div>\r\n        </div>\r\n      </div>\r\n    </div>\r\n    <p class=\"exercise_data_container\">\r\n      <span class=\"exercise_number\">{{entity.stairs_climbed}}</span>\r\n      <span class=\"exercise_type\">floors</span>\r\n    </p>\r\n    \r\n    <p class=\"bottom_message\" *ngIf=\"entity.stairs_to_goal >= 0\">\r\n      <span class=\"number_left\">{{entity.stairs_to_goal}}</span>\r\n      <span class=\"exercise_left\">floors left to reach your goal!</span>\r\n    </p>\r\n    <p class=\"bottom_message\" *ngIf=\"entity.stairs_to_goal < 0\">\r\n      <span class=\"number_left\">{{0 - entity.stairs_to_goal}}</span>\r\n      <span class=\"exercise_left\">floors above your goal!</span>\r\n    </p>\r\n  </div>\r\n</div>\r\n");
    #line default
        }
    }
}
