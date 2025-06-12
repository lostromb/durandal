using Durandal.Common.Audio;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using SharpDX;
using SharpDX.Mathematics.Interop;
using SharpDX.MediaFoundation;
using SharpDX.Multimedia;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Extensions.NativeAudio.Codecs
{
    // Copied largely from https://github.com/sharpdx/SharpDX/blob/master/Source/SharpDX.MediaFoundation/AudioDecoder.cs

    /// <summary>
    /// A media decoder backed by Windows MediaFoundation libraries. Can decode a variety of common audio formats
    /// using OS-provided codecs, depending on the version of Windows that is running.
    /// </summary>
    public class MediaFoundationDecoder : Durandal.Common.Audio.AudioDecoder
    {
        private bool _mediaFoundationInitialized = false;
        private readonly ILogger _logger;
        private SourceReader sourceReader;
        private Sample currentSample;
        private int _bytesReadFromCurrentSample;
        private NonRealTimeStream _managedInputStream;
        private bool _ownsStream;
        private bool _endOfStream;
        private int _disposed = 0;

        /// <summary>
        /// Construts a new <see cref="MediaFoundationDecoder"/>
        /// </summary>
        /// <param name="logger">A logger</param>
        /// <param name="graph">The audio graph this is a part of.</param>
        /// <param name="nodeCustomName">An optional custom name for this graph node, for debugging.</param>
        public MediaFoundationDecoder(
            ILogger logger,
            WeakPointer<IAudioGraph> graph,
            string nodeCustomName = null)
            : base("unknown", graph, nameof(MediaFoundationDecoder), nodeCustomName)
        {
            _logger = logger.AssertNonNull(nameof(logger));
        }

        /// <inheritdoc/>
        public override async Task<AudioInitializationResult> Initialize(
            NonRealTimeStream inputStream,
            bool ownsStream,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            if (sourceReader != null)
            {
                await DurandalTaskExtensions.NoOpTask;
                return AudioInitializationResult.Already_Initialized;
            }

            _managedInputStream = inputStream.AssertNonNull(nameof(inputStream));
            _ownsStream = ownsStream;

            try
            {
                MediaManager.Startup();
                _mediaFoundationInitialized = true;

                sourceReader = new SourceReader(_managedInputStream);

                // Invalidate selection for all streams
                sourceReader.SetStreamSelection(SourceReaderIndex.AllStreams, false);

                // Select only audio stream
                sourceReader.SetStreamSelection(SourceReaderIndex.FirstAudioStream, true);

                // Get the media type for the current stream.
                using (var mediaType = sourceReader.GetNativeMediaType(SourceReaderIndex.FirstAudioStream, 0))
                {
                    var majorType = mediaType.Get(MediaTypeAttributeKeys.MajorType);
                    if (majorType != MediaTypeGuids.Audio)
                    {
                        _logger.Log("Input stream doesn't contain an audio stream.", LogLevel.Err);
                        return AudioInitializationResult.Failure_BadFormat;
                    }

                    Codec = "unknown";

                    for (int attributeIdx = 0; attributeIdx < mediaType.Count; attributeIdx++)
                    {
                        Guid attributeKey;
                        mediaType.GetByIndex(attributeIdx, out attributeKey);

                        if (attributeKey == MediaTypeAttributeKeys.Subtype.Guid)
                        {
                            // media types guids listed: https://gix.github.io/media-types/
                            Guid subtype = mediaType.Get(MediaTypeAttributeKeys.Subtype);
                            if (subtype == AudioFormatGuids.Mp3)
                            {
                                Codec = "mp3";
                            }
                            else if (subtype == AudioFormatGuids.Aac)
                            {
                                Codec = "aacm4a";
                            }
                            else if (subtype == AudioFormatGuids.Flac)
                            {
                                Codec = "flac";
                            }
                            else if (subtype == AudioFormatGuids.WMAudioV8)
                            {
                                Codec = "wma8";
                            }
                            else if (subtype == AudioFormatGuids.WMAudioV9)
                            {
                                Codec = "wma9";
                            }
                        }
                    }
                }

                // Set the type on the source reader to use PCM16
                using (var partialType = new MediaType())
                {
                    partialType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
                    partialType.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Pcm);
                    partialType.Set(MediaTypeAttributeKeys.AudioBitsPerSample, 16);
                    sourceReader.SetCurrentMediaType(SourceReaderIndex.FirstAudioStream, partialType);
                }

                // Retrieve back the real media type
                using (var realMediaType = sourceReader.GetCurrentMediaType(SourceReaderIndex.FirstAudioStream))
                {
                    int sizeRef;
                    WaveFormat wf = realMediaType.ExtracttWaveFormat(out sizeRef);
                    if (wf.Channels > 2)
                    {
                        _logger.Log("MF Audio decoder does not support more than 2 channels currently", LogLevel.Wrn);
                        return AudioInitializationResult.Failure_BadFormat;
                    }

                    OutputFormat = new AudioSampleFormat(wf.SampleRate, wf.Channels, wf.Channels == 1 ? MultiChannelMapping.Monaural : MultiChannelMapping.Stereo_L_R);
                }

                _bytesReadFromCurrentSample = 0;
            }
            catch (Exception e)
            {
                _logger.Log(e);
                if (sourceReader != null)
                {
                    sourceReader.Dispose();
                    sourceReader = null;
                }

                return AudioInitializationResult.Failure_Unspecified;
            }

            IsInitialized = true;
            return AudioInitializationResult.Success;
        }

        /// <inheritdoc/>
        public override bool PlaybackFinished => _endOfStream;

        /// <inheritdoc/>
        public override string CodecDescription => $"MediaFoundation generic decoder: {Codec}";

        /// <inheritdoc/>
        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_endOfStream)
            {
                await DurandalTaskExtensions.NoOpTask;
                return -1;
            }

            // Get a valid sample
            int samplesPerChannelProcessed = 0;
            while (currentSample == null || _bytesReadFromCurrentSample >= currentSample.TotalLength)
            {
                int streamIndex;
                SourceReaderFlags flags;
                long time;
                currentSample = sourceReader.ReadSample(SourceReaderIndex.FirstAudioStream, SourceReaderControlFlags.None, out streamIndex, out flags, out time);
                if ((flags & SourceReaderFlags.Endofstream) != 0)
                {
                    _endOfStream = true;
                    return samplesPerChannelProcessed;
                }

                _bytesReadFromCurrentSample = 0;
            }

            // And read as much as we can from it
            using (MediaBuffer currentBuffer = currentSample.ConvertToContiguousBuffer())
            {
                int bufferMaxLength;
                int bufferCurrentLength;

                var ptr = currentBuffer.Lock(out bufferMaxLength, out bufferCurrentLength);

                DataPointer rawData = new DataPointer(ptr, bufferCurrentLength);
                int samplesPerChannelCanReadThisTime = Math.Min(numSamplesPerChannel, (rawData.Size - _bytesReadFromCurrentSample) / sizeof(short) / OutputFormat.NumChannels);
                int bytesCanReadThisTime = Math.Min(BufferPool<byte>.DEFAULT_BUFFER_SIZE, samplesPerChannelCanReadThisTime * OutputFormat.NumChannels * sizeof(short));
                using (PooledBuffer<byte> managedScratch = BufferPool<byte>.Rent(bytesCanReadThisTime))
                {
                    rawData.CopyTo(managedScratch.Buffer, 0, bytesCanReadThisTime);
                    _bytesReadFromCurrentSample += bytesCanReadThisTime;
                    AudioMath.ConvertSamples_2BytesIntLittleEndianToFloat(
                        managedScratch.Buffer,
                        0,
                        buffer,
                        bufferOffset + (samplesPerChannelProcessed * OutputFormat.NumChannels),
                        samplesPerChannelCanReadThisTime * OutputFormat.NumChannels);
                    samplesPerChannelProcessed += samplesPerChannelCanReadThisTime;
                }

                currentBuffer.Unlock();

                return samplesPerChannelProcessed;
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                if (_mediaFoundationInitialized)
                {
                    MediaManager.Shutdown();
                }

                if (disposing)
                {
                    currentSample?.Dispose();
                    sourceReader?.Dispose();
                    if (_ownsStream)
                    {
                        _managedInputStream?.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Log(e);
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
