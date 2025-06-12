using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public enum ContextualEntitySource
    {
        /// <summary>
        /// Entity came from an unknown source
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Entity came from LU engine tagging an entity in the query
        /// </summary>
        LanguageUnderstanding = 1,

        /// <summary>
        /// Entity came from a dialog plugin explicitly injecting this entity into the conversation history
        /// </summary>
        ConversationHistory = 2,

        /// <summary>
        /// Entity came directly from the client in the request
        /// </summary>
        ClientInput = 3
    }
}
