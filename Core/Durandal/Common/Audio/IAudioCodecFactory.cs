using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    public interface IAudioCodecFactory
    {
        /// <summary>
        /// The list of format codes that this codec can encode to.
        /// </summary>
        Durandal.Common.Collections.IReadOnlySet<string> SupportedEncodeFormats { get; }

        /// <summary>
        /// The list of format codes that this codec can decode from.
        /// </summary>
        Durandal.Common.Collections.IReadOnlySet<string> SupportedDecodeFormats { get; }

        /// <summary>
        /// Tests whether this codec can encode the given format.
        /// </summary>
        /// <param name="codecName">The name of the codec to test, e.g. "opus"</param>
        /// <returns>Whether or not this codec can handle that format for encoding.</returns>
        bool CanEncode(string codecName);

        /// <summary>
        /// Tests whether this codec can decode from the given format.
        /// </summary>
        /// <param name="codecName">The name of the codec to test, e.g. "opus"</param>
        /// <returns>Whether or not this codec can handle that format for decoding.</returns>
        bool CanDecode(string codecName);

        /// <summary>
        /// Creates an audio decoder.
        /// </summary>
        /// <param name="codecName">The name of the format to decode from e.g. "opus"</param>
        /// <param name="codecParams">The codec parameters which capture information about the encoded audio</param>
        /// <param name="graph">The audio graph which the decoder will be a part of</param>
        /// <param name="traceLogger">A trace logger for outputting decoding status</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <returns>A newly created audio decoder. The decoder will NOT be initialized to a particular input stream.</returns>
        AudioDecoder CreateDecoder(
            string codecName,
            string codecParams,
            WeakPointer<IAudioGraph> graph,
            ILogger traceLogger,
            string nodeCustomName);

        /// <summary>
        /// Creates an audio encoder.
        /// </summary>
        /// <param name="codecName">The name of the format to encode to e.g. "opus"</param>
        /// <param name="graph">The audio graph which the encoder will be a part of</param>
        /// <param name="desiredInputFormat">The audio format of samples that will go to the encoder.
        /// The encoder is NOT GUARANTEED TO exactly match this format; it can choose a similar format if, for example, it doesn't support
        /// the exact sample rate given.</param>
        /// <param name="traceLogger">A trace logger for outputting encoding status</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <returns>A newly created audio encoder. The encoder will NOT be initialized to a particular output stream.</returns>
        AudioEncoder CreateEncoder(
            string codecName,
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat desiredInputFormat,
            ILogger traceLogger,
            string nodeCustomName);
    }
}
