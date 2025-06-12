using Durandal.API;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Components;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Common audio graph functions such as encoding or decoding data using a codec factory
    /// </summary>
    public static class AudioHelpers
    {
        public static async Task<AudioData> EncodeAudioSampleUsingCodec(AudioSample sample, IAudioCodecFactory codec, string codecName, ILogger traceLogger)
        {
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (RecyclableMemoryStream stream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            using (NonRealTimeStreamWrapper nrtStream = new NonRealTimeStreamWrapper(stream, false))
            using (AudioEncoder encoder = codec.CreateEncoder(codecName, new WeakPointer<IAudioGraph>(graph), sample.Format, traceLogger, nodeCustomName: null))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), sample, nodeCustomName: null))
            {
                source.ConnectOutput(encoder);
                await encoder.Initialize(nrtStream, false, cancelToken, realTime).ConfigureAwait(false);
                await source.WriteFully(cancelToken, realTime).ConfigureAwait(false);
                await encoder.Finish(cancelToken, realTime).ConfigureAwait(false);
                return new AudioData()
                {
                    Data = new ArraySegment<byte>(stream.ToArray()),
                    Codec = encoder.Codec,
                    CodecParams = encoder.CodecParams
                };
            }
        }

        public static async Task EncodeAudioSampleUsingCodec(AudioSample sample, Stream targetStream, IAudioCodecFactory codec, string codecName, ILogger traceLogger)
        {
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (NonRealTimeStreamWrapper nrtStream = new NonRealTimeStreamWrapper(targetStream, false))
            using (AudioEncoder encoder = codec.CreateEncoder(codecName, new WeakPointer<IAudioGraph>(graph), sample.Format, traceLogger, nodeCustomName: null))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), sample, nodeCustomName: null))
            {
                source.ConnectOutput(encoder);
                await encoder.Initialize(nrtStream, false, cancelToken, realTime).ConfigureAwait(false);
                await source.WriteFully(cancelToken, realTime).ConfigureAwait(false);
                await encoder.Finish(cancelToken, realTime).ConfigureAwait(false);
            }
        }

        public static async Task<AudioSample> DecodeAudioDataUsingCodec(AudioData encodedAudio, IAudioCodecFactory codec, ILogger traceLogger)
        {
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (MemoryStream stream = new MemoryStream(encodedAudio.Data.Array, encodedAudio.Data.Offset, encodedAudio.Data.Count, false))
            using (NonRealTimeStreamWrapper nrtStream = new NonRealTimeStreamWrapper(stream, false))
            using (AudioDecoder decoder = codec.CreateDecoder(encodedAudio.Codec, encodedAudio.CodecParams, new WeakPointer<IAudioGraph>(graph), traceLogger, nodeCustomName: null))
            {
                AudioInitializationResult initializationResult = await decoder.Initialize(nrtStream, false, cancelToken, realTime).ConfigureAwait(false);
                if (initializationResult != AudioInitializationResult.Success)
                {
                    throw new Exception("Audio decoder could not be initialized. Result was " + initializationResult.ToString());
                }

                using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, nodeCustomName: null))
                {
                    decoder.ConnectOutput(bucket);
                    await bucket.ReadFully(cancelToken, realTime).ConfigureAwait(false);
                    return bucket.GetAllAudio();
                }
            }
        }

        public static async Task<AudioSample> DecodeAudioDataUsingCodec(IAudioDataSource dataSource, IAudioCodecFactory codec, ILogger traceLogger)
        {
            return await DecodeAudioStream(dataSource.AudioDataReadStream, codec, dataSource.Codec, dataSource.CodecParams, traceLogger).ConfigureAwait(false);
        }

        public static async Task<AudioSample> DecodeAudioStream(NonRealTimeStream inputStream, IAudioCodecFactory codecFactory, string codec, string codecParams, ILogger traceLogger)
        {
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (AudioDecoder decoder = codecFactory.CreateDecoder(codec, codecParams, new WeakPointer<IAudioGraph>(graph), traceLogger, nodeCustomName: null))
            {
                AudioInitializationResult initializationResult = await decoder.Initialize(inputStream, false, cancelToken, realTime).ConfigureAwait(false);
                if (initializationResult != AudioInitializationResult.Success)
                {
                    throw new Exception("Audio decoder could not be initialized. Result was " + initializationResult.ToString());
                }

                using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, nodeCustomName: null))
                {
                    decoder.ConnectOutput(bucket);
                    await bucket.ReadFully(cancelToken, realTime).ConfigureAwait(false);
                    return bucket.GetAllAudio();
                }
            }
        }

        /// <summary>
        /// Takes the given audio sample and encodes it in .wav format to the given output stream (usually a file).
        /// </summary>
        /// <param name="sample"></param>
        /// <param name="outStream"></param>
        /// <returns></returns>
        public static async Task WriteWaveToStream(AudioSample sample, Stream outStream)
        {
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (NonRealTimeStreamWrapper nrtStream = new NonRealTimeStreamWrapper(outStream, false))
            using (AudioEncoder encoder = new RiffWaveEncoder(new WeakPointer<IAudioGraph>(graph), sample.Format, nodeCustomName: null, logger: NullLogger.Singleton))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), sample, nodeCustomName: null))
            {
                source.ConnectOutput(encoder);
                AudioInitializationResult ir = await encoder.Initialize(nrtStream, false, cancelToken, realTime).ConfigureAwait(false);
                if (ir != AudioInitializationResult.Success)
                {
                    throw new Exception("Failed audio initialization: " + ir.ToString());
                }

                await source.WriteFully(cancelToken, realTime).ConfigureAwait(false);
                await encoder.Finish(cancelToken, realTime).ConfigureAwait(false);
            }
        }

        public static async Task<AudioSample> ReadWaveFromStream(Stream inStream)
        {
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (NonRealTimeStreamWrapper nrtStream = new NonRealTimeStreamWrapper(inStream, false))
            using (AudioDecoder decoder = new RiffWaveDecoder(new WeakPointer<IAudioGraph>(graph), nodeCustomName: null))
            {
                AudioInitializationResult ir = await decoder.Initialize(nrtStream, false, cancelToken, realTime);
                if (ir != AudioInitializationResult.Success)
                {
                    throw new Exception("Failed audio initialization: " + ir.ToString());
                }

                using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), decoder.OutputFormat, nodeCustomName: null))
                {
                    decoder.ConnectOutput(target);
                    await target.ReadFully(cancelToken, realTime).ConfigureAwait(false);
                    return target.GetAllAudio();
                }
            }
        }

        public static void BuildAudioNodeNames(string implementationTypeName, string nodeCustomName, out string nodeName, out string nodeFullName)
        {
            implementationTypeName.AssertNonNullOrEmpty(nameof(implementationTypeName));
            if (string.IsNullOrEmpty(nodeCustomName))
            {
                nodeName = implementationTypeName;
                nodeFullName = implementationTypeName;
            }
            else
            {
                nodeName = implementationTypeName;
                nodeFullName = string.Format("{0} ({1})", nodeCustomName, implementationTypeName);
            }
        }

        /// <summary>
        /// Returns a suggested audio quality based on the power of the current machine's processor.
        /// </summary>
        /// <returns></returns>
        public static AudioProcessingQuality GetAudioQualityBasedOnMachinePerformance()
        {
            PerformanceClass machinePerf = NativePlatformUtils.GetMachinePerformanceClass();
            switch (machinePerf)
            {
                case PerformanceClass.Unknown:
                case PerformanceClass.Low:
                    return AudioProcessingQuality.Fastest;
                case PerformanceClass.Medium:
                default:
                    return AudioProcessingQuality.Balanced;
                case PerformanceClass.High:
                    return AudioProcessingQuality.BestQuality;
            }
        }
    }
}
