using Durandal.Common.Audio.WebRtc;
using Durandal.Common.Security;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components
{
    public sealed class AcousticEchoCancelizer : AbstractAudioSampleFilter
    {
        private readonly WebRtcFilter _filter;
        private readonly byte[] _microphoneBuffer;
        private readonly byte[] _speakerBuffer;

        public AcousticEchoCancelizer(WeakPointer<IAudioGraph> graph, int sampleRate, string nodeCustomName = null)
            : base(graph, nameof(AcousticEchoCancelizer), nodeCustomName)
        {
            InputFormat = new AudioSampleFormat(sampleRate, 2, MultiChannelMapping.Packed_2Ch);
            OutputFormat = new AudioSampleFormat(sampleRate, 1, MultiChannelMapping.Monaural);
            _filter = new WebRtcFilter(
                expectedAudioLatency: 0, // The amount of latency that the operating environment adds (in milliseconds). Since we do the alignment ourselves, this is 0. Except 0 is not allowed (?)
                filterLength: 100, // The length of the echo cancellation filter in milliseconds (typically ~150).
                recordedAudioFormat: new AudioFormat(sampleRate, 20, 1, 16),
                playedAudioFormat: new AudioFormat(sampleRate, 20, 1, 16),
                enableAec: true,
                enableDenoise: false,
                enableAgc: false,
                playedResampler: null,
                recordedResampler: null);
            _microphoneBuffer = new byte[10];
            _speakerBuffer = new byte[10];
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _filter.RegisterFramePlayed(_speakerBuffer);
            _filter.Write(_microphoneBuffer);
            short[] filteredAudio = new short[0];
            bool moreFrames;
            bool frameProcessed = _filter.Read(filteredAudio, out moreFrames);
            await DurandalTaskExtensions.NoOpTask;
            return 0;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await DurandalTaskExtensions.NoOpTask;
        }
    }
}
