using Durandal.Common.Logger;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Durandal.Common.Time
{
    /// <summary>
    /// Provides the current system UTC time using the most accurate timer available to the system
    /// </summary>
    public static class HighPrecisionTimer
    {
        /// <summary>
        /// Reconciles the difference in frequency between datetime ticks and that of the high-precision system timer
        /// </summary>
        private static readonly double _stopwatchMultiplier;

        /// <summary>
        /// The current difference in ticks between the Stopwatch high-precision timestamp (arbitrarily based) and the system clock
        /// </summary>
        private static readonly long _stopwatchOffset;
        
        static HighPrecisionTimer()
        {
            _stopwatchMultiplier = (double)TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

            // We need to find a common shared time point between the stopwatch (arbitrary time base) and wallclock times.
            long currentUtcNowTicks = 0;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // If we're on windows, we can query the kernel high precision timer
                try
                {
                    long windowsEpochTicks = new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;
                    long windowsFileTimeTicks;
                    GetSystemTimePreciseAsFileTime(out windowsFileTimeTicks);
                    currentUtcNowTicks = windowsEpochTicks + windowsFileTimeTicks;
                }
                catch (Exception e)
                {
                    DebugLogger.Default.Log(e);
                    if (Debugger.IsAttached)
                    {
                        Debugger.Break();
                    }
                }
            }

            if (currentUtcNowTicks == 0)
            {
                // We have to do something more jank and rely on the CLR's cached DateTime.UtcNow. But depending
                // on the system, this value only refreshes every 16ms. So we have to detect this cache change
                // and grab it at the right time.
                long baseUtcNowTicks = DateTimeOffset.UtcNow.Ticks;
                do
                {
                    currentUtcNowTicks = DateTimeOffset.UtcNow.Ticks;
                } while (currentUtcNowTicks == baseUtcNowTicks);
            }

            _stopwatchOffset = currentUtcNowTicks - (long)((double)Stopwatch.GetTimestamp() * _stopwatchMultiplier);
        }

        /// <summary>
        /// Retrieves the current UTC time according to the most accurate timer available to this system.
        /// Timestamps returned are guaranteed to be monotonously increasing from the perspective of the calling thread.
        /// </summary>
        /// <returns>The current UTC time</returns>
        public static DateTimeOffset GetCurrentUTCTime()
        {
            return new DateTimeOffset(GetCurrentTicks(), TimeSpan.Zero);
        }

        /// <summary>
        /// Returns the number of ticks on the current timer, with a tick defined as 1/10,000,000 of a second.
        /// The absolute offset of the returned tick count is based on UtcNow, so it can be used to construct timestamps
        /// that cross machine boundaries (as opposed to machine-local timestamps such as system uptime)
        /// </summary>
        /// <returns></returns>
        public static long GetCurrentTicks()
        {
            long stopwatchTimestamp = Stopwatch.GetTimestamp();

            // Old code that tried to account for DateTime.UtcNow caching delay
            //// Alter the stopwatch offset by 1% at a time to keep it constantly in sync with the system clock without causing major discontinuities in monotonous readings
            //long updatedOffset = DateTimeOffset.UtcNow.Ticks - (long)(stopwatchTimestamp * _stopwatchMultiplier);

            //// need to be very careful to scale downwards to prevent overflowing int64 here
            //_stopwatchOffset = (_stopwatchOffset / 100L * 99L) + (updatedOffset / 100L);

            return (long)((double)stopwatchTimestamp * _stopwatchMultiplier) + _stopwatchOffset;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void GetSystemTimePreciseAsFileTime(out long lpSystemTimeAsFileTime);
    }
}
