using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    public enum DialogEventType
    {
        Unknown,

        /// <summary>
        /// The primary dialog event is a text or audio query consisting of language input, which triggers invocation of LU and dialog engine.
        /// Example: text query, speech query
        /// </summary>
        Query,

        /// <summary>
        /// The primary dialog event is a client invoking a dialog action by its key, usually by interacting with the client-side view or by a time-delayed client action.
        /// The dialog action gets converted into synthetic LU before dialog engine runs.
        /// Example: pressing an HTML button, delayed page refresh, SPA action
        /// </summary>
        DialogAction,

        /// <summary>
        /// The primary dialog event is a directly invoked action caused by the client passing understanding data in the client request, bypassing LU and running dialog engine.
        /// Example: greet page, proactive canvas
        /// </summary>
        DirectAction
    }
}
