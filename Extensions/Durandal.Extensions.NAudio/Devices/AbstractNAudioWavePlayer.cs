using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.Utils;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Hardware;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.NAudio.Devices
{
    /// <summary>
    /// Base class for an NAudio wave player
    /// </summary>
    internal abstract class AbstractNAudioWavePlayer : AbstractAudioSampleTarget, IAudioRenderDevice
    {
        private IWavePlayer _waveOut;
        private SampleAdapterDurandalToNAudio _sampleAdapter;
        private int _disposed = 0;

        /// <summary>
        /// Constructs an abstract wave player.
        /// </summary>
        /// <param name="graph">The audio graph to associate with</param>
        /// <param name="hardwareFormat">The desired hardware format</param>
        /// <param name="implementingTypeName">The name of the actual non-abstract type being used</param>
        /// <param name="nodeCustomName">The name of this node in the audio graph</param>
        /// <param name="logger">A logger</param>
        /// <exception cref="ArgumentException">Throws if audio sample format is not mono or stereo</exception>
        public AbstractNAudioWavePlayer(WeakPointer<IAudioGraph> graph, AudioSampleFormat hardwareFormat, string implementingTypeName, string nodeCustomName, ILogger logger)
            : base(graph, implementingTypeName, nodeCustomName)
        {
            InputFormat = hardwareFormat.AssertNonNull(nameof(hardwareFormat));
            if (hardwareFormat.NumChannels > 2)
            {
                throw new ArgumentException("NAudio output cannot process more than 2 channels at once");
            }
            if (hardwareFormat.ChannelMapping == MultiChannelMapping.Stereo_R_L)
            {
                throw new ArgumentException("Stereo audio in NAudio must be L-R");
            }

            _sampleAdapter = new SampleAdapterDurandalToNAudio(this, graph, logger);
        }

        /// <summary>
        /// Sets the internal implementation of IWavePlayer inside this object.
        /// </summary>
        /// <param name="wavePlayer"></param>
        protected void InitializeWavePlayer(IWavePlayer wavePlayer)
        {
            _waveOut = wavePlayer.AssertNonNull(nameof(wavePlayer));
            _waveOut.Init(_sampleAdapter);
        }
        
        /// <inheritdoc />
        public override bool IsActiveNode => true;

        /// <inheritdoc />
        public Task StartPlayback(IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractNAudioWavePlayer));
            }

            if (_waveOut == null)
            {
                throw new InvalidOperationException("Wave out device not initialized");
            }

            if (_waveOut.PlaybackState != PlaybackState.Stopped)
            {
                _waveOut.Stop();
            }

            _waveOut.Play();
            return DurandalTaskExtensions.NoOpTask;
        }

        /// <inheritdoc />
        public Task StopPlayback()
        {
            if (_disposed != 0 && _waveOut.PlaybackState != PlaybackState.Stopped)
            {
                _waveOut.Stop();
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        /// <inheritdoc />
        protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new InvalidOperationException("Cannot push audio to a wave out device; it is a pull node in the graph");
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                if (disposing)
                {
                    _waveOut?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
