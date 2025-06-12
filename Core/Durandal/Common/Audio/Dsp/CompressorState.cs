using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Enumeration of compressor activity states: attack, sustain, release, etc.
    /// </summary>
    public enum CompressorState
    {
        Neutral,
        Attack,
        Sustain,
        Release
    }
}
