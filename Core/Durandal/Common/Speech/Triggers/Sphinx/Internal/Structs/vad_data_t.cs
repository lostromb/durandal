using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class vad_data_t
    {
        public byte in_speech;
        public short pre_speech_frames;
        public short post_speech_frames;
        public prespch_buf_t prespch_buf;
    }
}
