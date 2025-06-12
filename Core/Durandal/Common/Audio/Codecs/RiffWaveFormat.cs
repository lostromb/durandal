using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Codecs
{
    public enum RiffWaveFormat
    {
        /// <summary>
        /// Unknown wave format
        /// </summary>
        Unknown,

        /// <summary>
        /// 16-bit signed little-endian samples
        /// </summary>
        PCM_S16LE,

        /// <summary>
        /// 24-bit signed little-endian samples
        /// </summary>
        PCM_S24LE,

        /// <summary>
        /// 32-bit signed little-endian samples
        /// </summary>
        PCM_S32LE,

        /// <summary>
        /// 32-bit floating point little-endian samples
        /// </summary>
        PCM_F32LE,

        /// <summary>
        /// Adpcm-xq data encoded in blocks (Microsoft standard)
        /// </summary>
        ADPCM_MS,

        /// <summary>
        /// Adpcm-xq data encoded in blocks (IMA standard)
        /// </summary>
        ADPCM_IMA,

        /// <summary>
        /// aLaw encoded samples
        /// </summary>
        ALAW,

        /// <summary>
        /// uLaw encoded samples
        /// </summary>
        ULAW,
    }
}
