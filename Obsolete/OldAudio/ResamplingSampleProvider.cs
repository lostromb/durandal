using Durandal.Common.Audio.Codecs.Opus.Common;
using Durandal.Common.Audio.Interfaces;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// A sample provider which resamples output from another provider.
    /// </summary>
    public class ResamplingSampleProvider : IAudioSampleProvider
    {
        private SpeexResampler _resampler;
        private IAudioSampleProvider _source;
        private int _sourceSampleRate;
        private int _targetSampleRate;

        private int _inBufCur = 0;
        private float[] _inBuffer;
        private int _outBufCur = 0;
        private float[] _outBuffer;
        private float[] _scratch;

        /// <summary>
        /// Creates a new resampling sample provider
        /// </summary>
        /// <param name="source">The sample source to read input from</param>
        /// <param name="resampler">The resampler to use</param>
        /// <param name="bufferSize">The maximum length of a buffer that can be resampled in a single read</param>
        public ResamplingSampleProvider(IAudioSampleProvider source, SpeexResampler resampler, TimeSpan bufferSize)
        {
            _source = source ?? NullSampleProvider.Singleton;
            _resampler = resampler;
            _resampler.GetRates(out _sourceSampleRate, out _targetSampleRate);

            _inBuffer = new float[(int)(bufferSize.TotalSeconds * _sourceSampleRate)];
            _outBuffer = new float[(int)(bufferSize.TotalSeconds * _targetSampleRate)];
            _scratch = new float[(int)(bufferSize.TotalSeconds * _sourceSampleRate)];
        }

        public IAudioSampleProvider SampleProvider
        {
            get
            {
                return _source;
            }
            set
            {
                _source = value;
            }
        }

        public async Task<int> ReadSamples(float[] buffer, int offset, int count, IRealTimeProvider realTime)
        {
            if (_sourceSampleRate == _targetSampleRate)
            {
                return await _source.ReadSamples(buffer, offset, count, realTime).ConfigureAwait(false);
            }

            // Find out how many new samples are requested in the output sample rate
            // This number doesn't count what's already in the output buffer
            int outSamplesRequested = count - _outBufCur;
            outSamplesRequested = Math.Min(outSamplesRequested, _outBuffer.Length - _outBufCur); // clamp it to actual buffer size

            // Find out how many that translates to in the input sample rate
            int inSamplesRequested = (int)((long)(outSamplesRequested + 10) * _sourceSampleRate / _targetSampleRate); // + 10 in order to round up, so we can guarantee we can produce the requested number of samples
            inSamplesRequested = Math.Min(inSamplesRequested, _inBuffer.Length) - _inBufCur; // clamp it to actual buffer size

            // Try and read that many from the source
            int samplesReadFromSource = await _source.ReadSamples(_scratch, 0, inSamplesRequested, realTime).ConfigureAwait(false);

            // Copy the samples to the input buffer
            Buffer.BlockCopy(_scratch, 0, _inBuffer, _inBufCur * sizeof(float), samplesReadFromSource * sizeof(float));

            int samplesInInBuffer = _inBufCur + samplesReadFromSource;
            // Rescale input because the resampler is dumb
            for (int c = 0; c < samplesReadFromSource; c++)
            {
                _inBuffer[_inBufCur + c] *= 32768;
            }

            // Now resample
            int inLen = _inBufCur + samplesReadFromSource;
            int outLen = _outBufCur + outSamplesRequested;
            _resampler.Process(0, _inBuffer, 0, ref inLen, _outBuffer, _outBufCur, ref outLen);

            // Rescale output because the resampler is dumb
            for (int c = 0; c < outLen; c++)
            {
                _outBuffer[_outBufCur + c] /= 32768;
            }

            int samplesInOutBuffer = outLen + _outBufCur;
            int samplesProvided = Math.Min(samplesInOutBuffer, outSamplesRequested);

            // Copy output samples to output buffer
            Buffer.BlockCopy(_outBuffer, 0, buffer, offset * sizeof(float), samplesProvided * sizeof(float));

            // And shrink our buffers again
            int outSamplesUsed = (_outBufCur + outSamplesRequested);
            int outSamplesOrphaned = samplesInOutBuffer - outSamplesUsed;
            if (outSamplesOrphaned > 0)
            {
                for (int c = 0; c < outSamplesOrphaned; c++)
                {
                    // memmove() to the left
                    _outBuffer[c] = _outBuffer[c + outSamplesUsed];
                }

                _outBufCur = outSamplesOrphaned;
            }
            else
            {
                _outBufCur = 0;
            }

            int inSamplesOrphaned = samplesInInBuffer - inLen;
            if (inSamplesOrphaned > 0)
            {
                for (int c = 0; c < inSamplesOrphaned; c++)
                {
                    // memmove() to the left
                    _inBuffer[c] = _inBuffer[c + inLen];
                }

                _inBufCur = inSamplesOrphaned;
            }
            else
            {
                _inBufCur = 0;
            }

            return samplesInOutBuffer;
        }
    }
}
