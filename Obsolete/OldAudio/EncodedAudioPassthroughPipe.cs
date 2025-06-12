using Durandal.Common.Audio.Interfaces;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Used when we want to transport compressed audio of an arbitrary codec without having to decode it.
    /// </summary>
    public class EncodedAudioPassthroughPipe : AudioWritePipe
    {
        private string _codecName;
        private string _codecParams;

        public EncodedAudioPassthroughPipe(string codecName, string codecParams) : base(1)
        {
            _codecName = codecName;
            _codecParams = codecParams;
        }

        protected override string GetCodec()
        {
            return _codecName;
        }

        protected override string GetCodecParams()
        {
            return _codecParams;
        }
    }
}
