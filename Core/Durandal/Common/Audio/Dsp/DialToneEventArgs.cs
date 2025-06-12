using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Dsp
{
    /// <summary>
    /// Event args associated with dial tones detected within an audio stream.
    /// </summary>
    public class DialToneEventArgs : EventArgs
    {
        public DialTone Tone;
        public int ChannelIdx;
    }
}
