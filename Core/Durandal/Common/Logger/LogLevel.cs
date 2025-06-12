namespace Durandal.Common.Logger
{
    using System;

    [Flags]
    public enum LogLevel
    {
        /// <summary>
        /// None (only makes sense for filters and uninitialized values)
        /// </summary>
        None = 0x0,
        
        /// <summary>
        /// Standard
        /// </summary>
        Std = 0x1 << 1,

        /// <summary>
        /// Warning
        /// </summary>
        Wrn = 0x1 << 2,

        /// <summary>
        /// Error
        /// </summary>
        Err = 0x1 << 3,

        /// <summary>
        /// Verbose
        /// </summary>
        Vrb = 0x1 << 4,

        /// <summary>
        /// Instrumentation
        /// </summary>
        Ins = 0x1 << 5,

        /// <summary>
        /// Critical
        /// </summary>
        Crt = 0x1 << 6,

        /// <summary>
        /// All (only makes sense for filters and initializers)
        /// </summary>
        All = Std | Wrn | Err | Vrb | Ins | Crt
    }

    public static class LoggingLevelManipulators
    {
        public static char ToChar(this LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Std:
                    return 'S';
                case LogLevel.Wrn:
                    return 'W';
                case LogLevel.Err:
                    return 'E';
                case LogLevel.Vrb:
                    return 'V';
                case LogLevel.Ins:
                    return 'I';
                case LogLevel.Crt:
                    return 'C';
            }

            return 'N';
        }

        public static LogLevel ParseLevelChar(string input)
        {
            switch (input)
            {
                case "S":
                    return LogLevel.Std;
                case "W":
                    return LogLevel.Wrn;
                case "E":
                    return LogLevel.Err;
                case "V":
                    return LogLevel.Vrb;
                case "I":
                    return LogLevel.Ins;
                case "C":
                    return LogLevel.Crt;
            }

            return LogLevel.None;
        }
    }
}
