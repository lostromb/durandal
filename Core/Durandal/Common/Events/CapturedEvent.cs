using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Events
{
    /// <summary>
    /// Represents an event that was intercepted by an <see cref="EventRecorder{T}"/>.
    /// </summary>
    /// <typeparam name="E">The type of EventArgs that is expected to come from this event.</typeparam>
    public class CapturedEvent<E> where E : EventArgs
    {
        /// <summary>
        /// The source of the event.
        /// </summary>
        public object Source { get; set; }

        /// <summary>
        /// The event arguments.
        /// </summary>
        public E Args { get; set; }

        public CapturedEvent(object source, E args)
        {
            Source = source;
            Args = args;
        }
    }
}
