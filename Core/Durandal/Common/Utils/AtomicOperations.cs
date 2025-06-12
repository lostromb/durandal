using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Durandal.Common.Utils
{
    public static class AtomicOperations
    {
        /// <summary>
        /// Represents the semantics that "We only want execution to ever pass this line of code once, using
        /// the given flag as state. Once the flag is set, always return false".
        /// Most commonly used in IDisposable classes to detect multiple disposal of objects.
        /// </summary>
        /// <param name="flag">The flag to store execution state. Initially 0, will be set to 1 after this method is called.</param>
        /// <returns>True if this is the first execution with the given state flag</returns>
        public static bool ExecuteOnce(ref int flag)
        {
            return Interlocked.CompareExchange(ref flag, 1, 0) == 0;
        }

        /// <summary>
        /// Sets a flag to a given value atomically.
        /// </summary>
        /// <param name="flag">The flag to set</param>
        /// <param name="value">The value to give the flag</param>
        public static void SetFlag(ref int flag, bool value = true)
        {
            flag = value ? 1 : 0;
        }

        /// <summary>
        /// Gets the current value of the flag. If the flag is TRUE (1), this operation will atomically set it to FALSE (0).
        /// </summary>
        /// <returns>The current flag status, before it is cleared</returns>
        public static bool GetAndClearFlag(ref int flag)
        {
            return Interlocked.CompareExchange(ref flag, 0, 1) != 0;
        }
    }
}
