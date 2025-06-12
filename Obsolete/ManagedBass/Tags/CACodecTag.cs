﻿#if __IOS__ || __MAC__
using System;
using System.Runtime.InteropServices;

namespace ManagedBass.Tags
{
    /// <summary>
    /// Core Audio Codec Tag structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class CACodecTag
    {
		/// <summary>
		/// The file format identifier.
		/// </summary>
        public int ftype;

		/// <summary>
		/// The audio format identifier.
		/// </summary>
        public int atype;

        IntPtr name;

		/// <summary>
		/// The description of the audio file format.
		/// </summary>
        public string Name => Marshal.PtrToStringAnsi(name);

        /// <summary>
        /// Read the tag from a Channel.
        /// </summary>
        /// <param name="Channel">The Channel to read the tag from.</param>
        public static CACodecTag Read(int Channel)
        {
            return (CACodecTag)Marshal.PtrToStructure(Bass.ChannelGetTags(Channel, TagType.CoreAudioCodec), typeof(CACodecTag));
        }
    }
}
#endif