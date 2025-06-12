using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Audio.Interfaces
{
    public interface IAudioDecompressionStream : IDisposable
    {
        AudioChunk Decompress(ArraySegment<byte> input);
        AudioChunk Close();
    }
}
