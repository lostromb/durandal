using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Codecs.Opus.Enums;
using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Extensions.NativeAudio.Codecs
{
    /// <summary>
    /// P/Invoke adapter for opus using native library
    /// </summary>
    internal class NativeOpusCodecFactory : IOpusCodecFactory
    {
        private string _cachedVersionString = null;

        public IOpusDecoder CreateDecoder(int sampleRate, int channelCount)
        {
            return NativeOpusDecoder.Create(sampleRate, channelCount);
        }

        public IOpusMultistreamDecoder CreateMultistreamDecoder(int sampleRate, int channelCount, int streams, int coupledStreams, byte[] channelMapping)
        {
            return NativeOpusMultistreamDecoder.Create(sampleRate, channelCount, streams, coupledStreams, channelMapping);
        }

        public IOpusEncoder CreateEncoder(int sampleRate, int channelCount, OpusApplication application)
        {
            return NativeOpusEncoder.Create(sampleRate, channelCount, application);
        }

        public IOpusMultistreamEncoder CreateMultistreamEncoder(int sampleRate, int channelCount, byte[] channelMapping, out int streams, out int coupledStreams, OpusApplication application)
        {
            return NativeOpusMultistreamEncoder.Create(sampleRate, channelCount, channelMapping, out streams, out coupledStreams, application);
        }

        public string GetVersionString()
        {
            if (_cachedVersionString == null)
            {
                _cachedVersionString = Marshal.PtrToStringAnsi(NativeOpus.opus_get_version_string()); // returned pointer is hardcoded in lib so no need to free anything
            }

            return _cachedVersionString;
        }
    }
}
