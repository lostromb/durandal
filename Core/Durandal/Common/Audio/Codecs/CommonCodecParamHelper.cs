using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Durandal.Common.Audio.Codecs
{
    /// <summary>
    /// Utility class for formatting or parsing "common" codec parameter strings, which are used by several codecs.
    /// These come in the format of "samplerate=16000 channels=1 layout=0"
    /// </summary>
    public static class CommonCodecParamHelper
    {
        private static readonly Regex SAMPLE_RATE_PARSER = new Regex("samplerate=([0-9]+)");
        private static readonly Regex CHANNEL_COUNT_PARSER = new Regex("channels=([0-9]+)");
        private static readonly Regex CHANNEL_LAYOUT_PARSER = new Regex("layout=([0-9]+)");

        public static string CreateCodecParams(AudioSampleFormat format)
        {
            return string.Format("samplerate={0} channels={1} layout={2}", format.SampleRateHz, format.NumChannels, (int)format.ChannelMapping);
        }

        public static bool TryParseCodecParams(string codecParams, out AudioSampleFormat format)
        {
            int sampleRate;
            int numChannels = 1;
            int channelMappingInt = (int)MultiChannelMapping.Monaural;
            format = null;

            Match m = SAMPLE_RATE_PARSER.Match(codecParams);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out sampleRate))
            {
                return false;
            }

            m = CHANNEL_COUNT_PARSER.Match(codecParams);
            if (m.Success)
            {
                if (!int.TryParse(m.Groups[1].Value, out numChannels))
                {
                    throw new FormatException("Channel count field was not an integer. Codec params are: " + codecParams);
                }
            }

            m = CHANNEL_LAYOUT_PARSER.Match(codecParams);
            if (m.Success)
            {
                if (!int.TryParse(m.Groups[1].Value, out channelMappingInt))
                {
                    throw new FormatException("Channel layout field was not an integer. Codec params are: " + codecParams);
                }
            }

            format = new AudioSampleFormat(sampleRate, numChannels, (MultiChannelMapping)channelMappingInt);
            return true;
        }
    }
}
