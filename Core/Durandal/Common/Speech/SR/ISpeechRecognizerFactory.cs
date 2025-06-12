using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.SR
{
    public interface ISpeechRecognizerFactory : IDisposable
    {
        /// <summary>
        /// Begins recognizing speech in the given language (locale), and returns the recognition stream.
        /// </summary>
        /// <param name="audioGraph">The audio graph that the created recognizer will be a part of.</param>
        /// <param name="graphNodeName">The friendly name for the created node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="locale">The locale to interpret the speech in, i.e. "en-US"</param>
        /// <param name="queryLogger">A logger attached to the specific stream</param>
        /// <param name="cancelToken">In case you want to cancel initializing the recognizer</param>
        /// <param name="realTime">A definition of real time.</param>
        Task<ISpeechRecognizer> CreateRecognitionStream(
            WeakPointer<IAudioGraph> audioGraph,
            string graphNodeName,
            LanguageCode locale,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime);

        /// <summary>
        /// Tests to see if a given locale is supported by the recognizer factory
        /// </summary>
        /// <param name="locale"></param>
        /// <returns></returns>
        bool IsLocaleSupported(LanguageCode locale);
    }
}
