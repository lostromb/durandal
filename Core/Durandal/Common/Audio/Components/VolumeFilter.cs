using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio filter graph component which applies linear or logarithmic volume attenuation.
    /// </summary>
    public sealed class VolumeFilter : AbstractAudioSampleFilter
    {
        /// <summary>
        /// The maximum amount of volume that can be applied by this filter, in decibels
        /// </summary>
        public const float MAX_VOLUME_DBA = 48;

        /// <summary>
        /// The minimum amount of volume that can be applied by this filter, in decibels
        /// </summary>
        public const float MIN_VOLUME_DBA = -72;

        private static readonly TimeSpan SCRATCH_SPACE_LENGTH = TimeSpan.FromMilliseconds(10);

        private readonly int _processingBufferSizeSamplesPerChannel;
        private PooledBuffer<float> _processingBuffer;

        private int _samplesOfFadeRemaining = 0;
        private float _currentFadeSlopeDba = 0;
        private float _currentFadeSlopeLinear = 0;
        private float _currentFadeTarget = 0;
        private float _currentVolumeLinear = 1.0f;
        private float _currentVolumeDBA = 0.0f;
        private bool _logarithmicFade = false;

        public VolumeFilter(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName) : base(graph, nameof(VolumeFilter), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            SetVolumeLinear(1.0f);

            // Create a buffer long enough to store 10ms of samples
            // Note that this is just scratch space. It doesn't actually impact latency, only the granularity of volume changes
            _processingBufferSizeSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, SCRATCH_SPACE_LENGTH);
            _processingBuffer = BufferPool<float>.Rent(_processingBufferSizeSamplesPerChannel * format.NumChannels);
        }

        /// <summary>
        /// Gets or sets the current volume as a linear scalar
        /// </summary>
        public float VolumeLinear
        {
            get
            {
                return _currentVolumeLinear;
            }
            set
            {
                SetVolumeLinear(value);
            }
        }

        /// <summary>
        /// Gets or sets the current volume in units of decibels amplification (dBA)
        /// </summary>
        public float VolumeDecibels
        {
            get
            {
                return _currentVolumeDBA;
            }
            set
            {
                SetVolumeDecibels(value);
            }
        }
        
        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int totalSamplesPerChannelProcessed = 0;
            while (totalSamplesPerChannelProcessed < count)
            {
                int thisBatchSizePerChannel = await Input.ReadAsync(
                    _processingBuffer.Buffer,
                    0,
                    FastMath.Min(count - totalSamplesPerChannelProcessed, _processingBufferSizeSamplesPerChannel),
                    cancelToken,
                    realTime).ConfigureAwait(false);

                if (thisBatchSizePerChannel < 0)
                {
                    // End of input, apparently.
                    return totalSamplesPerChannelProcessed == 0 ? -1 : totalSamplesPerChannelProcessed;
                }
                else if (thisBatchSizePerChannel == 0)
                {
                    // Input exhausted. Just return what we have.
                    return totalSamplesPerChannelProcessed;
                }

                // Apply volume window and write to output
                int thisBatchSizeTotal = thisBatchSizePerChannel * InputFormat.NumChannels;

                if (_samplesOfFadeRemaining == 0)
                {
                    AudioMath.ScaleAndMoveSamples(_processingBuffer.Buffer, 0, buffer, offset, thisBatchSizeTotal, _currentVolumeLinear);
                }
                else
                {
                    // If necessary, apply a smooth transition during volume changes
                    float currentTransitionVolume = _currentVolumeLinear;
                    int ch = 0;
                    for (int c = 0; c < thisBatchSizeTotal; c++)
                    {
                        buffer[offset + c] = _processingBuffer.Buffer[c] * currentTransitionVolume;
                        if (++ch > InputFormat.NumChannels)
                        {
                            ch = 0;
                            currentTransitionVolume += _currentFadeSlopeLinear;
                        }
                    }
                }
                
                totalSamplesPerChannelProcessed += thisBatchSizePerChannel;
                offset += thisBatchSizeTotal;
                UpdateVolumeFadeLevel(thisBatchSizePerChannel);
            }

            return totalSamplesPerChannelProcessed;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int totalSamplesPerChannelProcessed = 0;
            int totalSamplesProcessed = 0;
            while (totalSamplesPerChannelProcessed < count)
            {
                int thisBatchSizePerChannel = FastMath.Min(_processingBufferSizeSamplesPerChannel, count - totalSamplesPerChannelProcessed);
                int thisBatchSizeTotal = thisBatchSizePerChannel * InputFormat.NumChannels;
                ArrayExtensions.MemCopy(buffer, (offset + totalSamplesProcessed), _processingBuffer.Buffer, 0, thisBatchSizeTotal);

                // Apply volume window
                if (_samplesOfFadeRemaining == 0)
                {
                    AudioMath.ScaleSamples(_processingBuffer.Buffer, 0, thisBatchSizeTotal, _currentVolumeLinear);
                }
                else
                {
                    // If necessary, apply a smooth transition during volume changes
                    float currentTransitionVolume = _currentVolumeLinear;
                    int ch = 0;
                    for (int c = 0; c < thisBatchSizeTotal; c++)
                    {
                        _processingBuffer.Buffer[c] *= currentTransitionVolume;
                        if (++ch > InputFormat.NumChannels)
                        {
                            ch = 0;
                            currentTransitionVolume += _currentFadeSlopeLinear;
                        }
                    }
                }

                // And pipe to output
                await Output.WriteAsync(_processingBuffer.Buffer, 0, thisBatchSizePerChannel, cancelToken, realTime).ConfigureAwait(false);
                
                totalSamplesPerChannelProcessed += thisBatchSizePerChannel;
                totalSamplesProcessed += thisBatchSizeTotal;
                UpdateVolumeFadeLevel(thisBatchSizePerChannel);
            }
        }

        /// <summary>
        /// Sets the volume to a target value in units of decibels of amplification. 6 = double volume, -6 = half volume, etc.
        /// </summary>
        /// <param name="targetVolumeDba">The target volume in units of decibels. Instead of negative infinity use <see cref="MIN_VOLUME_DBA"/></param>
        /// <param name="fadeTime">An optional time stretch the volume change over. If null, volume is applied instantly</param>
        public void SetVolumeDecibels(float targetVolumeDba, TimeSpan? fadeTime = null)
        {
            if (float.IsNaN(targetVolumeDba))
            {
                throw new ArgumentOutOfRangeException("Volume must be a rational number");
            }

            if (float.IsPositiveInfinity(targetVolumeDba) || targetVolumeDba > MAX_VOLUME_DBA)
            {
                targetVolumeDba = MAX_VOLUME_DBA;
            }

            if (float.IsNegativeInfinity(targetVolumeDba) || targetVolumeDba < MIN_VOLUME_DBA)
            {
                targetVolumeDba = MIN_VOLUME_DBA;
            }

            lock (this)
            {
                _logarithmicFade = true;
                if (!fadeTime.HasValue ||
                    fadeTime.Value.Ticks <= 0)
                {
                    // Set volume instantly
                    _currentVolumeLinear = AudioMath.DecibelsToLinear(targetVolumeDba);
                    _currentVolumeDBA = targetVolumeDba;
                    _currentFadeTarget = targetVolumeDba;
                    _samplesOfFadeRemaining = 0;
                    _currentFadeSlopeDba = 0;
                    _currentFadeSlopeLinear = 0;
                }
                else
                {
                    // Otherwise, calculate the fade parameters
                    _samplesOfFadeRemaining = (int)(((long)fadeTime.Value.TotalMilliseconds * InputFormat.SampleRateHz) / 1000L);
                    _currentFadeTarget = targetVolumeDba;
                    _currentFadeSlopeDba = (_currentFadeTarget - _currentVolumeDBA) / (float)_samplesOfFadeRemaining;
                    int samplesInFirstFadeSegment = FastMath.Min(_samplesOfFadeRemaining, _processingBufferSizeSamplesPerChannel);
                    float newLinearVolumeTarget = AudioMath.DecibelsToLinear(_currentVolumeDBA + (_currentFadeSlopeDba * samplesInFirstFadeSegment));
                    _currentFadeSlopeLinear = (newLinearVolumeTarget - _currentVolumeLinear) / (float)samplesInFirstFadeSegment;
                }
            }
        }

        /// <summary>
        /// Sets the volume to a linear value (straight multiplication of each input sample).
        /// </summary>
        /// <param name="targetVolume"></param>
        /// <param name="fadeTime"></param>
        public void SetVolumeLinear(float targetVolume, TimeSpan? fadeTime = null)
        {
            if (targetVolume < 0)
            {
                throw new ArgumentOutOfRangeException("Volume cannot be negative");
            }
            if (float.IsNaN(targetVolume) || float.IsInfinity(targetVolume))
            {
                throw new ArgumentOutOfRangeException("Volume must be a rational number");
            }

            lock (this)
            {
                _logarithmicFade = false;
                if (!fadeTime.HasValue ||
                    fadeTime.Value.Ticks <= 0)
                {
                    // Set volume instantly
                    _currentVolumeLinear = targetVolume;
                    _currentVolumeDBA = AudioMath.LinearToDecibels(_currentVolumeLinear);

                    if (float.IsPositiveInfinity(_currentVolumeDBA) || _currentVolumeDBA > MAX_VOLUME_DBA)
                    {
                        _currentVolumeDBA = MAX_VOLUME_DBA;
                    }

                    if (float.IsNegativeInfinity(_currentVolumeDBA) || _currentVolumeDBA < MIN_VOLUME_DBA)
                    {
                        _currentVolumeDBA = MIN_VOLUME_DBA;
                    }

                    _currentFadeTarget = targetVolume;
                    _samplesOfFadeRemaining = 0;
                    _currentFadeSlopeDba = 0;
                    _currentFadeSlopeLinear = 0;
                }
                else
                {
                    // Otherwise, calculate the fade parameters
                    _samplesOfFadeRemaining = (int)(((long)fadeTime.Value.TotalMilliseconds * InputFormat.SampleRateHz) / 1000L);
                    _currentFadeTarget = targetVolume;
                    _currentFadeSlopeLinear = (_currentFadeTarget - _currentVolumeLinear) / (float)_samplesOfFadeRemaining;
                    _currentFadeSlopeDba = (AudioMath.LinearToDecibels(_currentFadeTarget) - _currentVolumeDBA) / (float)_samplesOfFadeRemaining;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            _processingBuffer?.Dispose();
            _processingBuffer = null;
            base.Dispose(disposing);
        }

        private void UpdateVolumeFadeLevel(int samplesElapsed)
        {
            if (_logarithmicFade)
            {
                if (_samplesOfFadeRemaining > 0)
                {
                    _samplesOfFadeRemaining = FastMath.Max(0, _samplesOfFadeRemaining - samplesElapsed);
                    if (_samplesOfFadeRemaining == 0)
                    {
                        _currentVolumeDBA = _currentFadeTarget;
                        _currentFadeSlopeDba = 0;
                        _currentFadeSlopeLinear = 0;
                    }
                    else
                    {
                        _currentVolumeDBA += (_currentFadeSlopeDba * samplesElapsed);
                        _currentVolumeLinear = AudioMath.DecibelsToLinear(_currentVolumeDBA);
                        int samplesInNextSegment = FastMath.Min(_samplesOfFadeRemaining, _processingBufferSizeSamplesPerChannel);
                        float newLinearVolumeTarget = AudioMath.DecibelsToLinear(_currentVolumeDBA + (_currentFadeSlopeDba * samplesInNextSegment));
                        _currentFadeSlopeLinear = (newLinearVolumeTarget - _currentVolumeLinear) / (float)samplesInNextSegment;
                    }
                }
            }
            else
            {
                if (_samplesOfFadeRemaining > 0)
                {
                    _samplesOfFadeRemaining = FastMath.Max(0, _samplesOfFadeRemaining - samplesElapsed);
                    if (_samplesOfFadeRemaining == 0)
                    {
                        _currentVolumeLinear = _currentFadeTarget;
                        _currentFadeSlopeDba = 0;
                        _currentFadeSlopeLinear = 0;
                    }
                    else
                    {
                        _currentVolumeLinear += (_currentFadeSlopeLinear * samplesElapsed);
                    }

                    _currentVolumeDBA = AudioMath.LinearToDecibels(_currentVolumeLinear);
                }
            }
        }
    }
}
