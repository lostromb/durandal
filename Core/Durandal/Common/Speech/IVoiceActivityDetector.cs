using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Speech
{
    public interface IVoiceActivityDetector : IDisposable
    {
        /// <summary>
        /// Process a single slice of audio.
        /// </summary>
        /// <param name="samples"></param>
        /// <param name="numSamples"></param>
        void ProcessForVad(short[] samples, int numSamples);

        /// <summary>
        /// Returns true if the voice activity detector infers a speech signal in the last processed samples
        /// </summary>
        /// <returns></returns>
        bool IsSpeechDetected();
    }
}
