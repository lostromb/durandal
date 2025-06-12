using Durandal.Common.Audio.Interfaces;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    public class AudioDecompressorPipe : AudioWritePipe
    {
        private IAudioCodec _codec;
        private IAudioDecompressionStream _decompressor;

        public AudioDecompressorPipe(IAudioCodec codec, string encodeParams, Guid? traceId = null)
            : base(1)
        {
            _codec = codec;
            _decompressor = _codec.CreateDecompressionStream(encodeParams, traceId);
        }

        protected override byte[] TransformOutput(byte[] input)
        {
            AudioChunk audio = _decompressor.Decompress(new ArraySegment<byte>(input));

            if (audio == null)
            {
                return BinaryHelpers.EMPTY_BYTE_ARRAY;
            }

            return audio.GetDataAsBytes();
        }

        protected override byte[] FinalizeOutput()
        {
            AudioChunk audio = _decompressor.Close();

            if (audio == null)
            {
                return BinaryHelpers.EMPTY_BYTE_ARRAY;
            }

            return audio.GetDataAsBytes();
        }
    }
}
