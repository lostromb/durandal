using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Audio;
using System.IO;
using Durandal.Common.IO;
using System.Threading;

namespace Durandal.Common.Dialog.Services
{
    public interface IStreamingAudioCache : IDisposable
    {
        /// <summary>
        /// Asynchronously opens a stream that plays the audio. This task will finish as soon
        /// as the stream is available and has some data; the stream does not need to be complete.
        /// </summary>
        /// <param name="key">The key to use for looking up the stream</param>
        /// <param name="queryLogger">A logger for trace operations</param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <param name="maxSpinTime"></param>
        /// <returns>A retrieve result containing a stream if one was successfully opened</returns>
        Task<RetrieveResult<IAudioDataSource>> TryGetAudioReadStream(string key, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan? maxSpinTime = null);

        /// <summary>
        /// Creates a stream to which encoded audio data can be written.
        /// </summary>
        /// <param name="key">The key to use for storing the stream</param>
        /// <param name="codec">The codec that the audio will be encoded with</param>
        /// <param name="codecParams">The codec parameters from the encoder</param>
        /// <param name="queryLogger">A logger for trace operations</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>A stream which you can write audio data to</returns>
        Task<NonRealTimeStream> CreateAudioWriteStream(string key, string codec, string codecParams, ILogger queryLogger, IRealTimeProvider realTime);
    }
}
