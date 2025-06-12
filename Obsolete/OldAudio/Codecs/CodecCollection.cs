using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Interfaces;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Speech.TTS.Bing;
using Durandal.Common.Speech.SR.Cortana;
using Durandal.Common.Net;

namespace Durandal.Common.Audio.Codecs
{
    /// <summary>
    /// Provides static methods to create speech reco, speech synth, and audio codec objects, by choosing from a list of available providers.
    /// </summary>
    public class CodecCollection
    {
        private readonly Dictionary<string, IAudioCodec> _codecs = new Dictionary<string, IAudioCodec>();
        private readonly ILogger _logger;

        public CodecCollection(ILogger logger)
        {
            _logger = logger;

            // PCM is always available regardless of config
            RegisterCodec(new PCMCodec(logger.Clone("PCMCodec")));
        }

        /// <summary>
        /// Attempts to initialize an audio codec using the given provider name
        /// </summary>
        /// <param name="providerName"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public IAudioCodec TryGetAudioCodec(string providerName)
        {
            IAudioCodec codec;
            if (_codecs.TryGetValue(providerName, out codec))
            {
                return codec;
            }

            return null;
        }

        public void RegisterCodec(IAudioCodec codec)
        {
            // Does it already exist? If so, ignore the duplicate
            if (_codecs.ContainsKey(codec.GetFormatCode().ToLowerInvariant()))
            {
                return;
            }

            if (codec.Initialize())
            {
                _codecs[codec.GetFormatCode().ToLowerInvariant()] = codec;
            }
            else
            {
                _logger.Log("Error occurred while initializing \"" + codec.GetFormatCode() + "\" audio codec", LogLevel.Err);
            }
        }
    }
}
