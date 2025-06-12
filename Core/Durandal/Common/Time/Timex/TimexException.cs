using System;

namespace Durandal.Common.Time.Timex
{
    /// <summary>
    /// Indicates an error occurred in timex processing, generally that a value was invalid or does
    /// not make sense in the given interpretation context.
    /// </summary>
    public class TimexException : Exception
    {
        public TimexException() { }

        public TimexException(string message)
            : base(message) { }

        public TimexException(string message, Exception innerException)
            : base(message, innerException) { }

        public TimexException(string message, string ruleId)
            : base(ruleId + ": " + message) { }
    }
}
