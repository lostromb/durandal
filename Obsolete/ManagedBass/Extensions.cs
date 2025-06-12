﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ManagedBass
{
    /// <summary>
    /// Contains Helper and Extension methods.
    /// </summary>
    public static class Extensions
    {
        internal static readonly ReferenceHolder ChannelReferences = new ReferenceHolder();
        
        /// <summary>
        /// Clips a value between a Minimum and a Maximum.
        /// </summary>
        public static T Clip<T>(this T Item, T MinValue, T MaxValue)
            where T : IComparable<T>
        {
            if (Item.CompareTo(MaxValue) > 0)
                return MaxValue;

            return Item.CompareTo(MinValue) < 0 ? MinValue : Item;
        }

        /// <summary>
        /// Checks for equality of an item with any element of an array of items.
        /// </summary>
        public static bool Is<T>(this T Item, params T[] Args)
        {
            return Args.Contains(Item);
        }

        /// <summary>
        /// Converts <see cref="Resolution"/> to <see cref="BassFlags"/>
        /// </summary>
        public static BassFlags ToBassFlag(this Resolution Resolution)
        {
            switch (Resolution)
            {
                case Resolution.Byte:
                    return BassFlags.Byte;
                case Resolution.Float:
                    return BassFlags.Float;
                default:
                    return BassFlags.Default;
            }
        }
        
        /// <summary>
        /// Returns the <param name="N">n'th (max 15)</param> pair of Speaker Assignment Flags
        /// </summary>
        public static BassFlags SpeakerN(int N)
        {
            return (BassFlags)(N << 24);
        }

        static bool? _floatable;

        /// <summary>
        /// Check whether Floating point streams are supported in the Current Environment.
        /// </summary>
        public static bool SupportsFloatingPoint
        {
            get
            {
                if (_floatable.HasValue) 
                    return _floatable.Value;

                // try creating a floating-point stream
                var hStream = Bass.CreateStream(44100, 1, BassFlags.Float, StreamProcedureType.Dummy);

                _floatable = hStream != 0;

                // floating-point channels are supported! (free the test stream)
                if (_floatable.Value) 
                    Bass.StreamFree(hStream);

                return _floatable.Value;
            }
        }

        internal static Version GetVersion(int Num)
        {
            return new Version(Num >> 24 & 0xff,
                               Num >> 16 & 0xff,
                               Num >> 8 & 0xff,
                               Num & 0xff);
        }
        
        /// <summary>
        /// Returns a string representation for given number of channels.
        /// </summary>
        public static string ChannelCountToString(int Channels)
        {
            switch (Channels)
            {
                case 1:
                    return "Mono";
                case 2:
                    return "Stereo";
                case 3:
                    return "2.1";
                case 4:
                    return "Quad";
                case 5:
                    return "4.1";
                case 6:
                    return "5.1";
                case 7:
                    return "6.1";
                default:
                    if (Channels < 1)
                        throw new ArgumentException("Channels must be greater than Zero.");
                    return Channels + " Channels";
            }
        }

        /// <summary>
        /// Extract an array of strings from a pointer to ANSI null-terminated string ending with a double null.
        /// </summary>
        public static string[] ExtractMultiStringAnsi(IntPtr Ptr)
        {
            var l = new List<string>();

            while (true)
            {
                var str = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(Ptr);

                if (string.IsNullOrEmpty(str))
                    break;
                
                l.Add(str);

                // char '\0'
                Ptr += str.Length + 1;
            }

            return l.ToArray();
        }
    }
}
