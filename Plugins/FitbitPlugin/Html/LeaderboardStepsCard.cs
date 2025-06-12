//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace RazorViews
{
    public class LeaderboardStepsCard
    {
        private StringWriter Output;
        public LeaderboardStepsCard()
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
            Output.Write("<div class=\"container\"> \r\n  <div class=\"container-background\"></div>\r\n  <div class=\"container-border\">\r\n  \r\n    <div class=\"top_message_container\">\r\n      <div class=\"logo\"> \r\n        <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_fitbit_logo.png\">\r\n\r\n      </div>\r\n\r\n      <div class=\"top_message\">\r\n        <p class=\"title\">Here's where you stand on the <br>leaderboard for step count</p>\r\n        <!--p class=\"title\">leaderboard compared to your friends</p-->\r\n\r\n      </div>\r\n      \r\n    </div>\r\n\r\n    <hr/>\r\n    <div class=\"data_container\">\r\n      <div *ngFor=\"let user of entity.users\">\r\n        <div *ngIf=\"user.is_current_user\" class=\"ranking_list_box_you\">\r\n          <div class=\"ranking_number_you\">#{{user.rank_steps}}</div>\r\n          <div *ngIf=\"user.avatar_url\" class=\"user_image\"><img src=\"{{user.avatar_url}}\"></div>\r\n          <div *ngIf=\"!user.avatar_url\" class=\"user_image\"><img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20user_image_2.png\"></div>\r\n          <div class=\"actual_data_box\">\r\n            <div class=\"username_you\">{{user.name}}</div>\r\n            <div class=\"data_number_you\">{{user.step_count}}</div>\r\n          </div>\r\n        </div>\r\n        <div *ngIf=\"user.is_current_user\" class=\"ranking_list_box_you_background\"></div>\r\n\r\n        <div *ngIf=\"!user.is_current_user\" class=\"ranking_list_box\">\r\n          <div class=\"ranking_number\">#{{user.rank_steps}}</div>\r\n          <div *ngIf=\"user.avatar_url\" class=\"user_image\"><img src=\"{{user.avatar_url}}\"></div>\r\n          <div *ngIf=\"!user.avatar_url\" class=\"user_image\"><img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20user_image_2.png\"></div>\r\n          <div class=\"actual_data_box\">\r\n            <div class=\"username\">{{user.name}}</div>\r\n            <div class=\"data_number\">{{user.step_count}}</div>\r\n          </div>\r\n        </div>\r\n      </div>\r\n    </div>\r\n  </div>\r\n</div>\r\n");
    #line default
        }
    }
}
