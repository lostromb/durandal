using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Audio.Interfaces
{
    using System.IO;

    public interface IAudioCodec
    {
        string GetFormatCode();

        string GetDescription();

        bool Initialize();

        IAudioCompressionStream CreateCompressionStream(int inputSampleRate, Guid? traceId = null);

        IAudioDecompressionStream CreateDecompressionStream(string encodeParams, Guid? traceId = null);
    }
}
