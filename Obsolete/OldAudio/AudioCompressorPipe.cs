using Durandal.Common.Audio.Interfaces;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    public class AudioCompressorPipe : AudioWritePipe
    {
        private readonly IAudioCodec _codec;
        private readonly IAudioCompressionStream _compressor;
        private readonly int _inputSampleRate;
        
        public AudioCompressorPipe(IAudioCodec codec, int inputSampleRate, Guid? traceId = null) : base (2)
        {
            _codec = codec;
            _inputSampleRate = inputSampleRate;
            _compressor = _codec.CreateCompressionStream(_inputSampleRate, traceId);
        }

        protected override string GetCodec()
        {
            if (_codec == null)
            {
                return string.Empty;
            }
            
            return _codec.GetFormatCode();
        }

        protected override string GetCodecParams()
        {
            if (_compressor == null)
            {
                return string.Empty;
            }

            return _compressor.GetEncodeParams();
        }

        protected override byte[] TransformOutput(byte[] input)
        {
            if (input == null || _compressor == null)
            {
                return null;
            }
            
            if (input.Length % 2 != 0)
                throw new ArgumentException("Samples that are passed into AudioCompressorTransport must have an even # of bytes!");

            AudioChunk rawAudio = new AudioChunk(input, _inputSampleRate);
            return _compressor.Compress(rawAudio);
        }

        protected override byte[] FinalizeOutput()
        {
            if (_compressor == null)
            {
                return null;
            }
            
            return _compressor.Close();
        }
    }
}
