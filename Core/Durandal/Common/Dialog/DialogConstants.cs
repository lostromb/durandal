using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.API
{
    /// <summary>
    /// Static holder class for constant values that are internal to the dialog runtime.
    /// This includes the names of hardcoded domains/intents such as "reflection" and "noreco"
    /// </summary>
    public static class DialogConstants
    {
        public static readonly string COMMON_DOMAIN = "common";
        public static readonly string NORECO_INTENT = "noreco";
        public static readonly string SIDE_SPEECH_DOMAIN = "side_speech";
        public static readonly string SIDE_SPEECH_INTENT = "side_speech";
        public static readonly string SIDE_SPEECH_HIGHCONF_INTENT = "side_speech_highconf";
        public static readonly string REFLECTION_DOMAIN = "reflection";
        public static readonly string CONFIGURE_INTENT = "configure";
        public static readonly string CALLBACK_DOMAIN_SLOT_NAME = "_callback_domain";
        public static readonly string CALLBACK_INTENT_SLOT_NAME = "_callback_intent";

        internal const int DIALOG_PRIORITY_BOOST = 3;
        internal const int DIALOG_PRIORITY_NORMAL = 2;
        internal const int DIALOG_PRIORITY_SUPPRESS = 1;
        internal const int DIALOG_PRIORITY_INTERNAL = 0;

        internal const int MAX_CLIENT_ID_LENGTH = 255;
        internal const int MAX_USER_ID_LENGTH = 255;
    }
}
