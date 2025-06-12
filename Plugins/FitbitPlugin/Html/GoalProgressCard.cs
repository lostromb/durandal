//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace RazorViews
{
    public class GoalProgressCard
    {
        private StringWriter Output;
        public GoalProgressCard()
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
            Output.Write("<div class=\"container\"> \r\n  <div class=\"container-background\"></div>\r\n  <div class=\"container-border\">\r\n    <div class=\"top_message_container\">\r\n      <div class=\"logo\"> \r\n        <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_fitbit_logo.png\">\r\n      </div>\r\n      <div class=\"top_message\">\r\n        <p class=\"title\">Fitbit</p>\r\n        <p class=\"subtitle\">Your goals</p>\r\n      </div>\r\n    </div>\r\n    <hr/>\r\n    <div class=\"data_container\">\r\n      <div class=\"steps_container\">\r\n        <div class=\"steps_circle c100 p100\">\r\n          <div class=\"slice\">\r\n            <div class=\"bar\"></div>\r\n            <div class=\"fill\"></div>\r\n          </div>\r\n          <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_steps_50.png\">\r\n        </div>\r\n        <div class=\"steps\">\r\n          <p class=\"steps_data\">\r\n            <span class=\"steps_number\">{{entity.goal_steps}}</span>\r\n            <span class=\"steps_name\">steps</span>\r\n          </p>\r\n        </div>\r\n      </div>\r\n      <div class=\"bottom_container\">\r\n        <div class=\"exercise_container\">\r\n          <div class=\"exercise_icon\">\r\n            <div class=\"small_exercise_circle c100 p100 small\">\r\n              <div class=\"slice\">\r\n                <div class=\"bar\"></div>\r\n                <div class=\"fill\"></div>\r\n              </div>\r\n              <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_floors_30.png\">\r\n            </div>\r\n          </div>\r\n          <div class=\"exercise_data\">\r\n            <p class=\"exercise_number\">{{entity.goal_floors}}</p>\r\n            <p class=\"exercise_type\">floors</p>\r\n          </div>\r\n        </div>\r\n        <div class=\"exercise_container\">\r\n          <div class=\"exercise_icon\">\r\n            <div class=\"small_exercise_circle c100 p100 small\">\r\n              <div class=\"slice\">\r\n                <div class=\"bar\"></div>\r\n                <div class=\"fill\"></div>\r\n              </div>\r\n              <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_miles_30.png\">\r\n            </div>\r\n          </div>\r\n          <div class=\"exercise_data\">\r\n            <p class=\"exercise_number\">{{entity.goal_distance}}</p>\r\n            <p class=\"exercise_type\">{{entity.distance_unit}}</p>\r\n          </div>\r\n        </div>\r\n        <div class=\"exercise_container\">\r\n          <div class=\"exercise_icon\">\r\n            <div class=\"small_exercise_circle c100 p100 small\">\r\n              <div class=\"slice\">\r\n                <div class=\"bar\"></div>\r\n                <div class=\"fill\"></div>\r\n              </div>\r\n              <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_calories_30.png\">\r\n            </div>\r\n          </div>\r\n          <div class=\"exercise_data\">\r\n            <p class=\"exercise_number\">{{entity.goal_calories}}</p>\r\n            <p class=\"exercise_type\">calories</p>\r\n          </div>\r\n        </div>\r\n        <div class=\"exercise_container\">\r\n          <div class=\"exercise_icon\">\r\n            <div class=\"small_exercise_circle c100 p100 small\">\r\n              <div class=\"slice\">\r\n                <div class=\"bar\"></div>\r\n                <div class=\"fill\"></div>\r\n              </div>\r\n              <img src=\"https://botletstorage.blob.core.windows.net/static-template-images/fitbit%20ic_calories_30.png\">\r\n            </div>\r\n          </div>\r\n          <div class=\"exercise_data\">\r\n            <p class=\"exercise_number\">60</p>\r\n            <p class=\"exercise_type\">minutes</p>\r\n          </div>\r\n        </div>\r\n      </div>\r\n    </div>\r\n  </div>\r\n</div>\r\n");
    #line default
        }
    }
}
