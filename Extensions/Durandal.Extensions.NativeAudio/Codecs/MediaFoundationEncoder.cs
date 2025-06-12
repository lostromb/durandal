using Durandal.Common.Audio;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using SharpDX;
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
    // Copied largely from https://github.com/markheath/NAudio.SharpMediaFoundation/blob/master/NAudio.SharpMediaFoundation/SharpMediaFoundationEncoder.cs

    /// <summary>
    /// An abstract media encoder backed by Windows MediaFoundation libraries. Can encode a variety of common audio formats
    /// using OS-provided codecs, depending on the version of Windows that is running.
    /// </summary>
    public abstract class MediaFoundationEncoder : AudioEncoder
    {
        private bool _mediaFoundationInitialized = false;
        //private const int MF_E_NOT_FOUND = unchecked((int)0xC00D36D5);

        private readonly ILogger _logger;
        private readonly Guid _containerTypeGuid;
        private MediaType _inputMediaType;
        private int _streamIndex;
        private SinkWriter _writer;
        private long _positionNanoseconds = 0;
        private IByteStream _nativeOutputStream;
        private NonRealTimeStream _managedOutputStream;
        private bool _ownsStream;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="MediaFoundationEncoder"/>
        /// </summary>
        /// <param name="logger">A logger</param>
        /// <param name="containerTypeGuid">A <see cref="TranscodeContainerTypeGuids">GUID</see> specifying the output container format</param>
        /// <param name="graph">The audio graph this is a part of.</param>
        /// <param name="inputFormat">The input audio format.</param>
        /// <param name="implementingTypeName">The type name of the subclass, i.e. the actual non-abstract encoder</param>
        /// <param name="nodeCustomName">An optional custom name for this graph node, for debugging.</param>
        public MediaFoundationEncoder(
            ILogger logger,
            Guid containerTypeGuid,
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat inputFormat,
            string implementingTypeName,
            string nodeCustomName = null)
            : base(graph, inputFormat, implementingTypeName, nodeCustomName)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _containerTypeGuid = containerTypeGuid;
        }

        /// <summary>
        /// Gets the bitrate of the encoded media, in bits per second
        /// </summary>
        public int BitrateBitsPerSecond { get; protected set; }

        /// <summary>
        /// Gets the MediaFoundation media type for the encoded audio, which specifies all internal codec parameters.
        /// </summary>
        protected MediaType EncodedMediaType { get; set; }

        /// <inheritdoc/>
        public override async Task<AudioInitializationResult> Initialize(NonRealTimeStream outputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (EncodedMediaType == null)
            {
                _logger.LogFormat(LogLevel.Err, DataPrivacyClassification.SystemMetadata,
                    "Could not find a MediaFoundation encoder able to encode \"{0}\", format {1}, bitrate {2}",
                    Codec,
                    InputFormat,
                    BitrateBitsPerSecond);
                return AudioInitializationResult.Failure_Unspecified;
            }

            if (_writer != null)
            {
                await DurandalTaskExtensions.NoOpTask;
                return AudioInitializationResult.Already_Initialized;
            }

            _managedOutputStream = outputStream.AssertNonNull(nameof(outputStream));
            _ownsStream = ownsStream;
            if (!outputStream.CanSeek)
            {
                throw new ArgumentException("The target stream for a MediaFoundation encoder must be seekable.");
            }

            MediaManager.Startup();
            _mediaFoundationInitialized = true;

            var sharpWf = WaveFormat.CreateCustomFormat(
                WaveFormatEncoding.Pcm,
                InputFormat.SampleRateHz,
                InputFormat.NumChannels,
                InputFormat.SampleRateHz * InputFormat.NumChannels * sizeof(short),
                InputFormat.NumChannels * sizeof(short),
                16);

            _inputMediaType = new MediaType();
            var size = 18 + sharpWf.ExtraSize;

            MediaFactory.InitMediaTypeFromWaveFormatEx(_inputMediaType, new[] { sharpWf }, size);
            _nativeOutputStream = new ByteStream(_managedOutputStream);

            using (var attributes = new MediaAttributes())
            {
                MediaFactory.CreateAttributes(attributes, 2);
                attributes.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms.Guid, (uint)1);
                attributes.Set(TranscodeAttributeKeys.TranscodeContainertype, _containerTypeGuid);

                try
                {
                    _writer = MediaFactory.CreateSinkWriterFromURL(null, _nativeOutputStream, attributes);
                    _writer.AddStream(EncodedMediaType, out _streamIndex);

                    // n.b. can get 0xC00D36B4 - MF_E_INVALIDMEDIATYPE here
                    _writer.SetInputMediaType(_streamIndex, _inputMediaType, null);
                    _writer.BeginWriting();
                }
                catch (Exception e)
                {
                    _logger.Log(e);
                    if (_writer != null)
                    {
                        _writer.Dispose();
                        _writer = null;
                    }

                    return AudioInitializationResult.Failure_Unspecified;
                }
            }

            IsInitialized = true;
            return AudioInitializationResult.Success;
        }

        /// <inheritdoc/>
        protected override async ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await DurandalTaskExtensions.NoOpTask;
            int samplesPerChannelProcessed = 0;
            using (var managedInt16Buffer = BufferPool<byte>.Rent())
            {
                int maxSamplesPerChannelPerBuffer = managedInt16Buffer.Length / InputFormat.NumChannels / sizeof(short);
                int maxBlockLength;
                int currentLength;

                while (samplesPerChannelProcessed < numSamplesPerChannel)
                {
                    int samplesPerChannelThisBuffer = Math.Min(maxSamplesPerChannelPerBuffer, numSamplesPerChannel - samplesPerChannelProcessed);
                    int bytesThisBuffer = samplesPerChannelThisBuffer * InputFormat.NumChannels * sizeof(short);

                    using (var nativeBuffer = MediaFactory.CreateMemoryBuffer(bytesThisBuffer))
                    using (var sample = MediaFactory.CreateSample())
                    {
                        sample.AddBuffer(nativeBuffer);

                        AudioMath.ConvertSamples_FloatTo2BytesIntLittleEndian(
                            buffer,
                            bufferOffset + (samplesPerChannelProcessed * InputFormat.NumChannels),
                            managedInt16Buffer.Buffer,
                            0,
                            samplesPerChannelThisBuffer * InputFormat.NumChannels);

                        long durationConverted = (10000000L * samplesPerChannelThisBuffer) / InputFormat.SampleRateHz;
                        var ptr = nativeBuffer.Lock(out maxBlockLength, out currentLength);
                        Marshal.Copy(managedInt16Buffer.Buffer, 0, ptr, bytesThisBuffer);
                        nativeBuffer.CurrentLength = bytesThisBuffer;
                        nativeBuffer.Unlock();
                        sample.SampleTime = _positionNanoseconds;
                        sample.SampleDuration = durationConverted;
                        _writer.WriteSample(_streamIndex, sample);
                        _positionNanoseconds += durationConverted;
                        samplesPerChannelProcessed += samplesPerChannelThisBuffer;
                    }
                }
            }
        }

        /// <inheritdoc/>
        protected override async ValueTask FlushAsyncInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await DurandalTaskExtensions.NoOpTask;
            //writer.Flush(streamIndex);
        }

        /// <inheritdoc/>
        protected override async ValueTask FinishInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await DurandalTaskExtensions.NoOpTask;
            _writer.Finalize();
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
                    _inputMediaType?.Dispose();
                    _writer?.Dispose();
                    _nativeOutputStream?.Dispose();
                    if (_ownsStream)
                    {
                        _managedOutputStream?.Dispose();
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

        /// <summary>
        /// Queries the available bitrates for a given encoding output type, sample rate and number of channels
        /// </summary>
        /// <param name="audioSubtype">Audio subtype - a value from the AudioSubtypes class</param>
        /// <param name="sampleRate">The sample rate of the PCM to encode</param>
        /// <param name="channels">The number of channels of the PCM to encode</param>
        /// <returns>An array of available bitrates in average bits per second</returns>
        protected static int[] GetEncodeBitrates(Guid audioSubtype, int sampleRate, int channels)
        {
            return GetOutputMediaTypes(audioSubtype)
                .Where(mt => mt.Get(MediaTypeAttributeKeys.AudioSamplesPerSecond) == sampleRate &&
                    mt.Get(MediaTypeAttributeKeys.AudioNumChannels) == channels)
                .Select(mt => mt.Get(MediaTypeAttributeKeys.AudioAvgBytesPerSecond) * 8)
                .Distinct()
                .OrderBy(br => br)
                .ToArray();
        }

        /// <summary>
        /// Gets all the available media types for a particular audio subtype
        /// </summary>
        /// <param name="audioSubtype">Audio subtype - a value from the AudioSubtypes class</param>
        /// <returns>An array of available media types that can be encoded with this subtype</returns>
        protected static IReadOnlyCollection<MediaType> GetOutputMediaTypes(Guid audioSubtype)
        {
            Collection availableTypes = null;
            try
            {
                availableTypes = MediaFactory.TranscodeGetAudioOutputAvailableTypes(audioSubtype, TransformEnumFlag.All, null);
                int count = availableTypes.ElementCount;
                var mediaTypes = new List<MediaType>(count);
                for (int n = 0; n < count; n++)
                {
                    var mediaTypeObject = (ComObject)availableTypes.GetElement(n);
                    mediaTypes.Add(new MediaType(mediaTypeObject.NativePointer));
                }

                return mediaTypes;
            }
            catch (SharpDXException c)
            {
                if (c.ResultCode.Code == ResultCode.NotFound.Code)
                {
                    // Don't worry if we didn't find any - just means no encoder available for this type
                    return new MediaType[0];
                }

                throw;
            }
            finally
            {
                availableTypes?.Dispose();
            }
        }

        /// <summary>
        /// Tries to find the encoding media type with the closest bitrate to that specified
        /// </summary>
        /// <param name="audioSubtype">Audio subtype, a value from AudioSubtypes</param>
        /// <param name="inputFormat">Your encoder input format (used to check sample rate and channel count)</param>
        /// <param name="desiredBitRate">Your desired bitrate</param>
        /// <returns>The closest media type, or null if none available</returns>
        protected static MediaType SelectMediaType(Guid audioSubtype, AudioSampleFormat inputFormat, int desiredBitRate)
        {
            return GetOutputMediaTypes(audioSubtype)
                .Where(mt => mt.Get(MediaTypeAttributeKeys.AudioSamplesPerSecond) == inputFormat.SampleRateHz &&
                    mt.Get(MediaTypeAttributeKeys.AudioNumChannels) == inputFormat.NumChannels)
                .Select(mt => new { MediaType = mt, Delta = Math.Abs(desiredBitRate - mt.Get(MediaTypeAttributeKeys.AudioAvgBytesPerSecond) * 8) })
                .OrderBy(mt => mt.Delta)
                .Select(mt => mt.MediaType)
                .FirstOrDefault();
        }
    }
}
