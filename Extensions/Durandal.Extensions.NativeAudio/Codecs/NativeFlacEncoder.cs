using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Extensions.NativeAudio.Codecs
{
    internal unsafe class NativeFlacEncoder : AudioEncoder
    {
        private readonly INativeFlacCodecProvider _flacLibrary;
        private readonly ILogger _logger;
        private readonly uint _complexity;
        private readonly FlacInterop.FLAC__StreamEncoderWriteCallback _writeCallbackRef; // need to keep the delegate pinned in memory so it doesn't get collected by GC
        private SafeHandle _encoder = null;
        private IRealTimeProvider _jankyCachedRealTime; // used to reconcile the write operation from the flac callback with the original WriteAsync operation
        private CancellationToken _jankyCachedCancelToken;
        private int _disposed = 0;

        /// <summary>
        /// Creates a new Flac encoder backed by a native library implementation.
        /// </summary>
        /// <param name="flacLibrary">Handle to native library implementation</param>
        /// <param name="graph">The audio graph to add this encoder to</param>
        /// <param name="format">The format of input audio</param>
        /// <param name="nodeCustomName">A custom name for this node in the audio graph</param>
        /// <param name="logger">A logger</param>
        /// <param name="complexity">Encoder complexity from 0 to 8</param>
        public NativeFlacEncoder(
            INativeFlacCodecProvider flacLibrary,
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            ILogger logger = null,
            int complexity = 0)
            : base(graph, format, nameof(NativeFlacEncoder), nodeCustomName)
        {
            _logger = logger ?? NullLogger.Singleton;
            _flacLibrary = flacLibrary.AssertNonNull(nameof(flacLibrary));

            // Assert that the input format conforms to what Flac allows
            if (format.NumChannels > 8)
            {
                throw new ArgumentOutOfRangeException("Flac codec cannot handle more than 8 channels");
            }

            if (complexity < 0 || complexity > 8)
            {
                throw new ArgumentOutOfRangeException("Flac complexity ranges from 0 to 8");
            }

            _complexity = (uint)complexity;
            _writeCallbackRef = WriteEncodedDataToOutputStreamCallback;
        }

        /// <summary>
        /// Necessary destructor because we hold an IntPtr reference to a native encoder struct
        /// </summary>
        ~NativeFlacEncoder()
        {
            Dispose(false);
        }

        /// <inheritdoc/>
        public override string Codec => NativeFlacCodecFactory.CODEC_NAME;

        /// <inheritdoc/>
        public override string CodecParams => CreateCodecParams(InputFormat);

        /// <inheritdoc/>
        public override Task<AudioInitializationResult> Initialize(
            NonRealTimeStream outputStream,
            bool ownsStream,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(NativeFlacEncoder));
            }

            if (IsInitialized)
            {
                return Task.FromResult(AudioInitializationResult.Already_Initialized);
            }

            OutputStream = outputStream.AssertNonNull(nameof(outputStream));
            OwnsStream = ownsStream;

            _logger.Log("Initializing Flac compression stream with samplerate=" + InputFormat.SampleRateHz + ", complexity=" + _complexity);
            _encoder = _flacLibrary.CreateEncoder();
            _flacLibrary.SetEncoderCompressionLevel(_encoder, (uint)_complexity);
            _flacLibrary.SetEncoderBitsPerSample(_encoder, 16);
            _flacLibrary.SetEncoderChannelCount(_encoder, (uint)InputFormat.NumChannels);
            _flacLibrary.SetEncoderSampleRate(_encoder, (uint)InputFormat.SampleRateHz);
            _flacLibrary.InitializeEncoderStream(_encoder, _writeCallbackRef);
            IsInitialized = true;
            return Task.FromResult(AudioInitializationResult.Success);
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(NativeFlacEncoder));
            }

            _jankyCachedRealTime = realTime;
            _jankyCachedCancelToken = cancelToken;
            // Convert floating point interleaved to integer interleaved
            // With 16 bits per sample the encoder range is += 32767
            using (PooledBuffer<int> scratchBuf = BufferPool<int>.Rent(count * InputFormat.NumChannels))
            {
                AudioMath.ConvertSamples_FloatToInt32_16bit(buffer, offset, scratchBuf.Buffer, 0, count * InputFormat.NumChannels, clamp: true);
                _flacLibrary.Encode(_encoder, scratchBuf.Buffer, 0, (uint)count);
            }

            return new ValueTask();
        }

        protected override ValueTask FinishInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(NativeFlacEncoder));
            }

            _jankyCachedRealTime = realTime;
            _jankyCachedCancelToken = cancelToken;
            _flacLibrary.FinishEncoderStream(_encoder);
            return new ValueTask();
        }

#if NET5_0_OR_GREATER
        [UnmanagedCallersOnly]
#endif
        private unsafe FlacInterop.FLAC__StreamEncoderWriteStatus WriteEncodedDataToOutputStreamCallback(IntPtr encoder, byte* buffer, uint bytes, uint samples, uint current_frame, IntPtr client_data)
        {
            try
            {
                using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent((int)bytes))
                {
                    // Span copy is one of the fastest possible methods to move data, and works well with unmanaged pointers, so use that
                    new Span<byte>(buffer, (int)bytes).CopyTo(scratch.Buffer);
                    OutputStream.Write(scratch.Buffer, 0, (int)bytes, _jankyCachedCancelToken, _jankyCachedRealTime);
                    return FlacInterop.FLAC__StreamEncoderWriteStatus.FLAC__STREAM_ENCODER_WRITE_STATUS_OK;
                }
            }
            catch (Exception e) // prevent exceptions from propagating to native code, who knows what will happen
            {
                try { _logger.Log(e, LogLevel.Err); } catch (Exception) { }
                return FlacInterop.FLAC__StreamEncoderWriteStatus.FLAC__STREAM_ENCODER_WRITE_STATUS_FATAL_ERROR;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                _flacLibrary.DestroyEncoder(_encoder);
            }
            catch (Exception e)
            {
                // don't let exceptions in the native layer kill the finalizer thread
                _logger.Log(e);
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private string CreateCodecParams(AudioSampleFormat format)
        {
            // This should conform to the common codec params used elsewhere, except with the added "Q" parameter (see CommonCodecParamHelper)
            return string.Format("samplerate={0} q={1} channels={2} layout={3}", format.SampleRateHz, _complexity, format.NumChannels, (int)format.ChannelMapping);
        }
    }
}
