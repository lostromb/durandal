using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit
{
    public static class Constants
    {
        public const string FITBIT_DOMAIN = "fitbit";

        public const double DEFAULT_DISTANCE_GOAL_KM = 5;
        public const int DEFAULT_STEPS_GOAL = 10000;
        public const int DEFAULT_ACTIVE_MINUTES_GOAL = 30;
        public const int DEFAULT_FLOORS_GOAL = 10;
        public const int DEFAULT_CALORIES_OUT_GOAL = 2000;

        public const string DEVICE_TYPE_TRACKER = "TRACKER";

        public const string CANONICAL_STAT_STEPS = "STEPS";
        public const string CANONICAL_STAT_CALORIES = "CALORIES";
        public const string CANONICAL_STAT_FLOORS = "FLOORS";
        public const string CANONICAL_STAT_DISTANCE = "DISTANCE";
        public const string CANONICAL_STAT_MILES = "MILES";
        public const string CANONICAL_STAT_KILOMETERS = "KILOMETERS";
        public const string CANONICAL_STAT_ACTIVE_MINUTES = "ACTIVE_MINUTES";
        public const string CANONICAL_STAT_WATER = "WATER";

        public const string CANONICAL_GOAL_STEPS = "STEPS";
        public const string CANONICAL_GOAL_CALORIES = "CALORIES";
        public const string CANONICAL_GOAL_FLOORS = "FLOORS";
        public const string CANONICAL_GOAL_DISTANCE = "DISTANCE";
        public const string CANONICAL_GOAL_MILES = "MILES";
        public const string CANONICAL_GOAL_KILOMETERS = "KILOMETERS";
        public const string CANONICAL_GOAL_ACTIVE_MINUTES = "ACTIVE_MINUTES";

        public const string CANONICAL_MEASUREMENT_WEIGHT = "WEIGHT";
        public const string CANONICAL_MEASUREMENT_HEIGHT = "HEIGHT";
        public const string CANONICAL_MEASUREMENT_BMI = "BMI";
        public const string CANONICAL_MEASUREMENT_AGE = "AGE";
        public const string CANONICAL_MEASUREMENT_BATTERY = "BATTERY";
        
        public const string CANONICAL_ACTIVITY_WALK = "WALK";
        public const string CANONICAL_ACTIVITY_RUN = "RUN";
        public const string CANONICAL_ACTIVITY_BURN = "BURN";
        public const string CANONICAL_ACTIVITY_LOG = "LOG";
        public const string CANONICAL_ACTIVITY_CLIMB = "CLIMB";

        public const string CANONICAL_ORDER_REF_PAST = "PAST";

        public const string CANONICAL_MERIDIAN_AM = "AM";
        public const string CANONICAL_MERIDIAN_PM = "PM";

        public const string INTENT_GET_ACTIVITY = "get_activity";
        public const string INTENT_GET_MEASUREMENT = "get_measurement";
        public const string INTENT_GET_GOALS = "get_goals";
        public const string INTENT_GET_REMAINING = "get_remaining";
        public const string INTENT_GET_LEADERBOARD = "get_leaderboard";
        public const string INTENT_GET_COUNT = "get_count";
        public const string INTENT_LOGOUT = "log_out";
        public const string INTENT_HELP = "help";
        public const string INTENT_SET_ALARM = "set_alarm";
        public const string INTENT_FIND_ALARM = "find_alarm";
        public const string INTENT_SET_GOAL = "set_goal";
        public const string INTENT_LOG_ACTIVITY = "log_activity";
        public const string INTENT_ENTER_TIME = "enter_time";
        public const string INTENT_ENTER_MERIDIAN = "enter_meridian";
        public const string INTENT_ENTER_DEVICE = "enter_device";
        
        public const string SLOT_ORDER_REF = "order_ref";
        public const string SLOT_STAT_TYPE = "stat_type";
        public const string SLOT_ACTIVITY_TYPE = "activity_type";
        public const string SLOT_MEASUREMENT = "measurement";
        public const string SLOT_GOAL_TYPE = "goal_type";
        public const string SLOT_DATE = "date";
        public const string SLOT_TIME = "time";
        public const string SLOT_MERIDIAN = "meridian";
        public const string SLOT_DEVICE = "device";
        public const string SLOT_GOAL_VALUE = "goal_value";
        public const string SLOT_QUANTITY = "quantity";
        public const string SLOT_UNIT = "unit";
        public const string SLOT_FOOD = "food";

        public const string SESSION_USER_PROFILE = "userProfile";
    }
}
