using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Codecs
{
    internal static class RiffWaveConstants
    {
        // https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ksmedia/ns-ksmedia-ksaudio_channel_config
        public const int SPEAKER_FRONT_LEFT = 0x1;
        public const int SPEAKER_FRONT_RIGHT = 0x2;
        public const int SPEAKER_FRONT_CENTER = 0x4;
        public const int SPEAKER_LOW_FREQUENCY = 0x8;
        public const int SPEAKER_BACK_LEFT = 0x10;
        public const int SPEAKER_BACK_RIGHT = 0x20;
        public const int SPEAKER_FRONT_LEFT_OF_CENTER = 0x40;
        public const int SPEAKER_FRONT_RIGHT_OF_CENTER = 0x80;
        public const int SPEAKER_BACK_CENTER = 0x100;
        public const int SPEAKER_SIDE_LEFT = 0x200;
        public const int SPEAKER_SIDE_RIGHT = 0x400;
        public const int SPEAKER_TOP_CENTER = 0x800;
        public const int SPEAKER_TOP_FRONT_LEFT = 0x1000;
        public const int SPEAKER_TOP_FRONT_CENTER = 0x2000;
        public const int SPEAKER_TOP_FRONT_RIGHT = 0x4000;
        public const int SPEAKER_TOP_BACK_LEFT = 0x8000;
        public const int SPEAKER_TOP_BACK_CENTER = 0x10000;
        public const int SPEAKER_TOP_BACK_RIGHT = 0x20000;

        // KSAUDIO_SPEAKER_DIRECTOUT
        public const int SPEAKER_LAYOUT_PACKED = 0;

        // KSAUDIO_SPEAKER_MONO
        public const int SPEAKER_LAYOUT_MONO = SPEAKER_FRONT_CENTER;

        // KSAUDIO_SPEAKER_STEREO
        public const int SPEAKER_LAYOUT_STEREO =
            SPEAKER_FRONT_LEFT |
            SPEAKER_FRONT_RIGHT;

        // KSAUDIO_SPEAKER_QUAD
        public const int SPEAKER_LAYOUT_QUAD =
            SPEAKER_FRONT_LEFT |
            SPEAKER_FRONT_RIGHT |
            SPEAKER_BACK_LEFT |
            SPEAKER_BACK_RIGHT;

        // KSAUDIO_SPEAKER_SURROUND
        public const int SPEAKER_LAYOUT_SURROUND =
            SPEAKER_FRONT_LEFT |
            SPEAKER_FRONT_RIGHT |
            SPEAKER_FRONT_CENTER |
            SPEAKER_BACK_CENTER;

        // KSAUDIO_SPEAKER_5POINT1
        public const int SPEAKER_LAYOUT_5_1 =
            SPEAKER_FRONT_LEFT |
            SPEAKER_FRONT_RIGHT |
            SPEAKER_FRONT_CENTER |
            SPEAKER_LOW_FREQUENCY |
            SPEAKER_BACK_LEFT |
            SPEAKER_BACK_RIGHT;

        // KSAUDIO_SPEAKER_5POINT1_SURROUND
        public const int SPEAKER_LAYOUT_5_1_SIDE =
            SPEAKER_FRONT_LEFT |
            SPEAKER_FRONT_RIGHT |
            SPEAKER_FRONT_CENTER |
            SPEAKER_LOW_FREQUENCY |
            SPEAKER_SIDE_LEFT |
            SPEAKER_SIDE_RIGHT;

        // KSAUDIO_SPEAKER_7POINT1
        public const int SPEAKER_LAYOUT_7_1 =
            SPEAKER_FRONT_LEFT |
            SPEAKER_FRONT_RIGHT |
            SPEAKER_FRONT_CENTER |
            SPEAKER_LOW_FREQUENCY |
            SPEAKER_BACK_LEFT |
            SPEAKER_BACK_RIGHT |
            SPEAKER_FRONT_LEFT_OF_CENTER |
            SPEAKER_FRONT_RIGHT_OF_CENTER;

        // KSAUDIO_SPEAKER_7POINT1_SURROUND
        public const int SPEAKER_LAYOUT_7_1_SIDE =
            SPEAKER_FRONT_LEFT |
            SPEAKER_FRONT_RIGHT |
            SPEAKER_FRONT_CENTER |
            SPEAKER_LOW_FREQUENCY |
            SPEAKER_BACK_LEFT |
            SPEAKER_BACK_RIGHT |
            SPEAKER_SIDE_LEFT |
            SPEAKER_SIDE_RIGHT;

        /// <summary>
        /// WAVEfmt format code for uncompressed PCM
        /// </summary>
        public const ushort WAVEFORMATTAG_PCM = 0x0001;

        /// <summary>
        /// WAVEfmt format code for Microsoft Adpcm-xq
        /// </summary>
        public const ushort WAVEFORMATTAG_MS_ADPCM = 0x0002;

        /// <summary>
        /// WAVEfmt format code for floating-point PCM
        /// </summary>
        public const ushort WAVEFORMATTAG_IEEE_FLOAT = 0x0003;

        /// <summary>
        /// WAVEfmt format code for aLaw encoding
        /// </summary>
        public const ushort WAVEFORMATTAG_ALAW = 0x0006;

        /// <summary>
        /// WAVEfmt format code for uLaw encoding
        /// </summary>
        public const ushort WAVEFORMATTAG_ULAW = 0x0007;

        /// <summary>
        /// WAVEfmt format code for IMA Adpcm-xq
        /// </summary>
        public const ushort WAVEFORMATTAG_IMA_ADPCM = 0x0011;

        /// <summary>
        /// Seen in the wild for some old Adpcm-xq-encoded files with blocksize 36
        /// </summary>
        public const ushort WAVEFORMATTAG_UNKNOWN_ADPCM = 0x0069;

        /// <summary>
        /// ?? I am guessing this is the format tag in WAVEFORMATEX which indicates that you need to read
        /// the actual format from WAVEFORMATEXTENSIBLE
        /// </summary>
        public const ushort WAVEFORMATTAG_REFER_TO_WAVEFORMAT_EXTENSIBLE = 0xFFFE;

        // https://github.com/tpn/winsdk-10/blob/master/Include/10.0.16299.0/shared/ksmedia.h#L837C20-L837C56
        // Endianness of GUIDs applies to first half so the bytes are in a slightly different order
        // DEFINE_GUIDSTRUCT("00000001-0000-0010-8000-00aa00389b71", KSDATAFORMAT_SUBTYPE_PCM);
        // in little endian:  01000000-0000-1000-8000-00aa00389b71
        public static readonly byte[] KSDATAFORMAT_SUBTYPE_PCM = new byte[]
        {
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00,
            0x10, 0x00,
            0x80, 0x00,
            0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71
        };

        // DEFINE_GUIDSTRUCT("00000003-0000-0010-8000-00aa00389b71", KSDATAFORMAT_SUBTYPE_IEEE_FLOAT);
        // in little endian:  03000000-0000-1000-8000-00aa00389b71
        public static readonly byte[] KSDATAFORMAT_SUBTYPE_IEEE_FLOAT = new byte[]
        {
            0x03, 0x00, 0x00, 0x00,
            0x00, 0x00,
            0x10, 0x00,
            0x80, 0x00,
            0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71
        };

        // DEFINE_GUIDSTRUCT("00000002-0000-0010-8000-00aa00389b71", KSDATAFORMAT_SUBTYPE_ADPCM);
        // in little endian:  02000000-0000-1000-8000-00aa00389b71
        public static readonly byte[] KSDATAFORMAT_SUBTYPE_MS_ADPCM = new byte[]
        {
            0x02, 0x00, 0x00, 0x00,
            0x00, 0x00,
            0x10, 0x00,
            0x80, 0x00,
            0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71
        };

        // the first two bytes are just the 16-bit little-endian old format code,
        // if you haven't noticed
        public static readonly byte[] KSDATAFORMAT_SUBTYPE_IMA_ADPCM = new byte[]
        {
            0x11, 0x00, 0x00, 0x00,
            0x00, 0x00,
            0x10, 0x00,
            0x80, 0x00,
            0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71
        };

        public static readonly byte[] KSDATAFORMAT_SUBTYPE_ALAW = new byte[]
        {
            0x06, 0x00, 0x00, 0x00,
            0x00, 0x00,
            0x10, 0x00,
            0x80, 0x00,
            0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71
        };

        public static readonly byte[] KSDATAFORMAT_SUBTYPE_ULAW = new byte[]
        {
            0x07, 0x00, 0x00, 0x00,
            0x00, 0x00,
            0x10, 0x00,
            0x80, 0x00,
            0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71
        };

        //004000C0-00F0-0000-CC01-30FF880118FF // ?? Seen in the wild to denote MS-Adpcm
    }
}
