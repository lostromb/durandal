using Durandal.Common.IO;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Defines a stream for reading encoded audio data, coupled with the codec name and codec params metadata which are required to correctly decode the stream.
    /// </summary>
    public interface IAudioDataSource : IDisposable
    {
        /// <summary>
        /// Gets the short code for the codec that this audio data is formatted with, e.g. "opus".
        /// May be null if the data source has not been initialized yet.
        /// </summary>
        string Codec { get; }

        /// <summary>
        /// Gets a codec-specific string of parameters that may be required to decode this stream properly.
        /// Null value is allowed for some codecs that don't require parameters, although empty string is preferred.
        /// </summary>
        string CodecParams { get; }

        /// <summary>
        /// The stream from which encoded audio data can be read
        /// </summary>
        NonRealTimeStream AudioDataReadStream { get; }
    }
}
