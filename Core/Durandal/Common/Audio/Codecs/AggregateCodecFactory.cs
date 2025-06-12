using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using System;
using System.Collections.Generic;

namespace Durandal.Common.Audio.Codecs
{
    /// <summary>
    /// An audio codec which aggregates the capabilities of multiple codecs at a time, allowing
    /// support of a wide range of formats
    /// </summary>
    public class AggregateCodecFactory : IAudioCodecFactory
    {
        private readonly IAudioCodecFactory[] _subCodecs;
        private readonly HashSet<string> _supportedDecodeFormats;
        private readonly HashSet<string> _supportedEncodeFormats;

        public AggregateCodecFactory(params IAudioCodecFactory[] subCodecs)
        {
            _subCodecs = subCodecs;
            _supportedDecodeFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _supportedEncodeFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (IAudioCodecFactory codec in _subCodecs)
            {
                _supportedDecodeFormats.UnionWith(codec.SupportedDecodeFormats);
                _supportedEncodeFormats.UnionWith(codec.SupportedEncodeFormats);
            }
        }

        public Durandal.Common.Collections.IReadOnlySet<string> SupportedEncodeFormats => new ReadOnlySetWrapper<string>(_supportedEncodeFormats);

        public Durandal.Common.Collections.IReadOnlySet<string> SupportedDecodeFormats => new ReadOnlySetWrapper<string>(_supportedDecodeFormats);

        public bool CanDecode(string codecName)
        {
            return _supportedDecodeFormats.Contains(codecName);
        }

        public bool CanEncode(string codecName)
        {
            return _supportedEncodeFormats.Contains(codecName);
        }

        public AudioDecoder CreateDecoder(
            string codecName,
            string codecParams,
            WeakPointer<IAudioGraph> graph,
            ILogger traceLogger,
            string nodeCustomName)
        {
            if (!CanDecode(codecName))
            {
                throw new ArgumentException("Cannot create a decoder for \"" + codecName + "\". Supported decode formats are " + string.Join(",", SupportedDecodeFormats));
            }

            foreach (IAudioCodecFactory subCodec in _subCodecs)
            {
                if (subCodec.CanDecode(codecName))
                {
                    return subCodec.CreateDecoder(codecName, codecParams, graph, traceLogger, nodeCustomName);
                }
            }

            throw new ArgumentException("Cannot create a decoder for \"" + codecName + "\". Aggregate codec claimed to support this format but decoder creation failed. This should be impossible");
        }

        public AudioEncoder CreateEncoder(
            string codecName,
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat desiredInputFormat,
            ILogger traceLogger,
            string nodeCustomName)
        {
            if (!CanEncode(codecName))
            {
                throw new ArgumentException("Cannot create an encoder for \"" + codecName + "\". Supported encode formats are " + string.Join(",", SupportedEncodeFormats));
            }

            foreach (IAudioCodecFactory subCodec in _subCodecs)
            {
                if (subCodec.CanDecode(codecName))
                {
                    return subCodec.CreateEncoder(codecName, graph, desiredInputFormat, traceLogger, nodeCustomName);
                }
            }

            throw new ArgumentException("Cannot create an encoder for \"" + codecName + "\". Aggregate codec claimed to support this format but encoder creation failed. This should be impossible");
        }
    }
}
