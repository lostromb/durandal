
namespace Durandal.Extensions.NativeAudio.Codecs
{
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Audio.Codecs.Opus.Enums;
    using Durandal.Common.Audio.Codecs.Opus.Structs;
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
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Audio codec which decodes a self-contained flac stream
    /// </summary>
    internal class NativeFlacDecoder : AudioDecoder
    {
        /// <summary>
        /// 8196 is default internal buffer size used by libflac, but we might need more than one frame, so increase it a bit
        /// </summary>
        private static readonly int INPUT_BUFFER_SIZE = 32768;

        /// <summary>
        /// Handle to native flac implementation
        /// </summary>
        private readonly INativeFlacCodecProvider _flacLibrary;

        /// <summary>
        /// A trace logger.
        /// </summary>
        private readonly ILogger _traceLogger;

        /// <summary>
        /// Persistent handle to callback function when flac wants to read encoded data
        /// </summary>
        private readonly FlacInterop.FLAC__StreamDecoderReadCallback _decoderReadCallback;

        /// <summary>
        /// Persistent handle to callback function when flac wants to write decoded data
        /// </summary>
        private readonly FlacInterop.FLAC__StreamDecoderWriteCallback _decoderWriteCallback;

        /// <summary>
        /// Persistent handle to callback function when flac decoder throws an error.
        /// </summary>
        private readonly FlacInterop.FLAC__StreamDecoderErrorCallback _decoderErrorCallback;

        /// <summary>
        /// Scratch buffer for encoded audio data, which we read from input stream
        /// </summary>
        private readonly PooledBuffer<byte> _decoderInputBuffer;

        /// <summary>
        /// Scratch buffer for decoded output audio samples, in interleaved format, which we will copy directly to output stream
        /// </summary>
        private PooledBuffer<float> _decoderOutputBuffer;

        private readonly AudioSampleFormat _sampleFormatFromCodecParams;

        /// <summary>
        /// Indicates that the input stream is exhausted (there could still be data in input buffer through)
        /// </summary>
        private bool _endOfStreamOnInput;

        /// <summary>
        /// Indicates that the decoder has processed all incoming data and reached eof internally.
        /// </summary>
        private bool _endOfStreamOnDecoder;

        /// <summary>
        /// Samples per channel of interleaved audio stored in output buffer
        /// </summary>
        private int _samplesPerChannelInDecoderOutputBuffer;

        /// <summary>
        /// Bytes of encoded data stored in input buffer
        /// </summary>
        private int _bytesInDecoderInputBuffer;

        /// <summary>
        /// Handle to native decoder.
        /// </summary>
        private SafeHandle _decoder = null;

        private int _disposed = 0;

        public override bool PlaybackFinished => _endOfStreamOnDecoder && _samplesPerChannelInDecoderOutputBuffer == 0;

        public override string CodecDescription => "Flac audio codec (via libFLAC)";

        /// <summary>
        /// Constructs a new <see cref="NativeFlacDecoder"/> 
        /// </summary>
        /// <param name="nativeFlac">The native (usually P/Invoke) implementation of Flac library.</param>
        /// <param name="graph">The audio graph that this component is part of</param>
        /// <param name="codecParams">Codec parameters from the encode</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="traceLogger">A logger</param>
        /// <returns>A newly created <see cref="NativeFlacDecoder"/></returns>
        public unsafe NativeFlacDecoder(
            INativeFlacCodecProvider nativeFlac,
            WeakPointer<IAudioGraph> graph,
            string codecParams,
            string nodeCustomName,
            ILogger traceLogger)
            : base(NativeFlacCodecFactory.CODEC_NAME, graph, nameof(NativeFlacDecoder), nodeCustomName)
        {
            _flacLibrary = nativeFlac;
            _traceLogger = traceLogger;
            _decoderReadCallback = ReadCallbackInternal;
            _decoderWriteCallback = WriteCallbackInternal;
            _decoderErrorCallback = ErrorCallbackInternal;
            _samplesPerChannelInDecoderOutputBuffer = 0;
            _bytesInDecoderInputBuffer = 0;
            _endOfStreamOnDecoder = false;
            _endOfStreamOnInput = false;

            if (!string.IsNullOrEmpty(codecParams))
            {
                if (!CommonCodecParamHelper.TryParseCodecParams(codecParams, out _sampleFormatFromCodecParams))
                {
                    throw new FormatException("Failed to parse codec parameter string for flac audio: " + codecParams);
                }
            }

            _decoderInputBuffer = BufferPool<byte>.Rent(INPUT_BUFFER_SIZE); 
        }

        /// <summary>
        /// Necessary destructor because we hold an IntPtr reference to a native decoder struct
        /// </summary>
        ~NativeFlacDecoder()
        {
            Dispose(false);
        }

        public override async Task<AudioInitializationResult> Initialize(NonRealTimeStream inputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(NativeFlacDecoder));
            }

            if (IsInitialized)
            {
                return AudioInitializationResult.Already_Initialized;
            }

            InputStream = inputStream.AssertNonNull(nameof(inputStream));
            OwnsStream = ownsStream;

            // Determine bitness, sample rate, channel count, etc. from the input stream
            _decoderOutputBuffer = BufferPool<float>.Rent(BufferPool<float>.MAX_BUFFER_SIZE);
            _decoder = _flacLibrary.CreateDecoder();
            _flacLibrary.InitializeDecoderStream(_decoder, _decoderReadCallback, _decoderWriteCallback, _decoderErrorCallback);

            int decodedSampleRate = 0;
            while (decodedSampleRate == 0)
            {
                // Try and read one frame to ensure the stream is in a valid state and get the headers we need
                await TryFillInternalReadBuffer(cancelToken, realTime).ConfigureAwait(false);
                _flacLibrary.DecodeSingle(_decoder);
                FlacInterop.FLAC__StreamDecoderState state = _flacLibrary.GetDecoderState(_decoder);
                if (state == FlacInterop.FLAC__StreamDecoderState.FLAC__STREAM_DECODER_END_OF_STREAM ||
                    state == FlacInterop.FLAC__StreamDecoderState.FLAC__STREAM_DECODER_ABORTED)
                {
                    _endOfStreamOnDecoder = true;
                    return AudioInitializationResult.Failure_StreamEnded;
                }

                decodedSampleRate = (int)_flacLibrary.GetDecoderSampleRate(_decoder);
            }

            int decoderChannelCount = (int)_flacLibrary.GetDecoderChannelCount(_decoder);
            MultiChannelMapping channelMapping = MultiChannelMapping.Unknown;
            // If it's multichannel audio, we need to have the channel mapping that was specified in the extra codec params
            if (_sampleFormatFromCodecParams != null)
            {
                channelMapping = _sampleFormatFromCodecParams.ChannelMapping;
            }
            else
            {
                // Otherwise (mono or stereo) we can maybe make a guess if channel mapping was not given
                if (decoderChannelCount > 2)
                {
                    throw new InvalidDataException("Could not determine channel mapping for input FLAC stream (Codec params string was not given). Channel count is " + decoderChannelCount);
                }

                channelMapping = decoderChannelCount == 1 ? MultiChannelMapping.Monaural : MultiChannelMapping.Stereo_L_R;
                _traceLogger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "Guessing channel layout for input FLAC stream: {0}", channelMapping);
            }

            // Swap from our temporary large buffer to a potentially smaller one now that we have more information about the stream
            OutputFormat = new AudioSampleFormat(decodedSampleRate, decoderChannelCount, channelMapping);
            // 65536 is the max block size, multiplied by the number of channels reported in the codec params, x2 just for safety
            PooledBuffer<float> actualOutputBuf = BufferPool<float>.Rent(65536 * decoderChannelCount * 2);
            ArrayExtensions.MemCopy(_decoderOutputBuffer.Buffer, 0, actualOutputBuf.Buffer, 0, _samplesPerChannelInDecoderOutputBuffer * decoderChannelCount);
            _decoderOutputBuffer.Dispose();
            _decoderOutputBuffer = actualOutputBuf;

            IsInitialized = true;
            return AudioInitializationResult.Success;
        }

        /// <summary>
        /// Attempts to read as much as possible from input stream into the input buffer, to try and satisfy the next read that libflac may try and make in its callback.
        /// We have to make sure there's enough in the buffer in advance so it doesn't have to trigger a read during the actual decoder callback. 8196 seems to be about
        /// the minimum number of bytes required.
        /// </summary>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private async ValueTask TryFillInternalReadBuffer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            while (_bytesInDecoderInputBuffer < INPUT_BUFFER_SIZE)
            {
                if (_endOfStreamOnInput)
                {
                    return;
                }

                int bytesRead = await InputStream.ReadAsync(
                    _decoderInputBuffer.Buffer,
                    _bytesInDecoderInputBuffer,
                    _decoderInputBuffer.Buffer.Length - _bytesInDecoderInputBuffer,
                    cancelToken,
                    realTime).ConfigureAwait(false);

                if (bytesRead <= 0)
                {
                    _endOfStreamOnInput = true;
                }
                else
                {
                    _bytesInDecoderInputBuffer += bytesRead;
                }
            }
        }

        /// <summary>
        /// Called by libflac when it wants to read more encoded data, which we supply from our prebuffered input data.
        /// </summary>
        /// <param name="decoder"></param>
        /// <param name="buffer"></param>
        /// <param name="bytes"></param>
        /// <param name="client_data"></param>
        /// <returns></returns>
#if NET5_0_OR_GREATER
        [UnmanagedCallersOnly]
#endif
        internal unsafe FlacInterop.FLAC__StreamDecoderReadStatus ReadCallbackInternal(IntPtr decoder, byte* buffer, ref uint bytes, IntPtr client_data)
        {
            try
            {
                if (_bytesInDecoderInputBuffer == 0 && !_endOfStreamOnInput)
                {
                    // We should ideally never hit this case because it forces us to do a blocking async read during an unsafe native callback, which is 100 different kinds of jank.
                    // But it's better than just aborting the stream mid-decoder....
                    _traceLogger.Log("Flac stream is forced to do a sync stream read because of inadequate prebuffering", LogLevel.Wrn);
                    TryFillInternalReadBuffer(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                }
                
                if (_bytesInDecoderInputBuffer == 0)
                {
                    bytes = 0;
                    return FlacInterop.FLAC__StreamDecoderReadStatus.FLAC__STREAM_DECODER_READ_STATUS_END_OF_STREAM;
                }

                int bytesCanReadFromInputBuffer = FastMath.Min(_bytesInDecoderInputBuffer, (int)bytes);
                new Span<byte>(_decoderInputBuffer.Buffer, 0, bytesCanReadFromInputBuffer).CopyTo(new Span<byte>(buffer, (int)bytes));

                // Shift input buffer left if necessary
                if (_bytesInDecoderInputBuffer > bytesCanReadFromInputBuffer)
                {
                    ArrayExtensions.MemMove(
                        _decoderInputBuffer.Buffer,
                        bytesCanReadFromInputBuffer,
                        0,
                        _bytesInDecoderInputBuffer - bytesCanReadFromInputBuffer);
                }

                _bytesInDecoderInputBuffer = _bytesInDecoderInputBuffer - bytesCanReadFromInputBuffer;
                bytes = (uint)bytesCanReadFromInputBuffer;
                return FlacInterop.FLAC__StreamDecoderReadStatus.FLAC__STREAM_DECODER_READ_STATUS_CONTINUE;
            }
            catch (Exception e)
            {
                try { _traceLogger.Log(e, LogLevel.Err); } catch (Exception) { }
                return FlacInterop.FLAC__StreamDecoderReadStatus.FLAC__STREAM_DECODER_READ_STATUS_ABORT;
            }
        }

        /// <summary>
        /// Called by libflac when it has produced decoded data for us. Parse the data header, copy the integer samples to interleaved floating point,
        /// and stash them in our output buffer.
        /// </summary>
        /// <param name="decoder"></param>
        /// <param name="frame"></param>
        /// <param name="buffer"></param>
        /// <param name="client_data"></param>
        /// <returns></returns>
#if NET5_0_OR_GREATER
        [UnmanagedCallersOnly]
#endif
        private unsafe FlacInterop.FLAC__StreamDecoderWriteStatus WriteCallbackInternal(IntPtr decoder, FlacInterop.FLAC__Frame* frame, int** buffer, IntPtr client_data)
        {
            // Interpret incoming data from the decoder and put it into our scratch buffer
            try
            {
                uint blockSize = (*frame).header.blocksize;
                uint channels = (*frame).header.channels;
                uint bits_per_sample = (*frame).header.bits_per_sample;
                float[] outBuf = _decoderOutputBuffer.Buffer;
                if (bits_per_sample == 16)
                {
                    for (uint chan = 0; chan < channels; chan++)
                    {
                        uint outIdx = ((uint)_samplesPerChannelInDecoderOutputBuffer * channels) + chan;
                        int* sourceArray = buffer[chan];
                        for (int sample = 0; sample < blockSize; sample++)
                        {
                            outBuf[outIdx] = (float)sourceArray[sample] / 32767.0f;
                            outIdx += channels;
                        }
                    }
                }
                else if (bits_per_sample == 24)
                {
                    for (uint chan = 0; chan < channels; chan++)
                    {
                        uint outIdx = ((uint)_samplesPerChannelInDecoderOutputBuffer * channels) + chan;
                        int* sourceArray = buffer[chan];
                        for (int sample = 0; sample < blockSize; sample++)
                        {
                            outBuf[outIdx] = (float)sourceArray[sample] / 3288607.0f;
                            outIdx += channels;
                        }
                    }
                }

                _samplesPerChannelInDecoderOutputBuffer += (int)blockSize;
                return FlacInterop.FLAC__StreamDecoderWriteStatus.FLAC__STREAM_DECODER_WRITE_STATUS_CONTINUE;
            }
            catch (Exception e)
            {
                try { _traceLogger.Log(e, LogLevel.Err); } catch (Exception) { }
                return FlacInterop.FLAC__StreamDecoderWriteStatus.FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
            }
        }

        /// <summary>
        /// Indicates an error being raised by decoder (corrupted stream or something). Set the flag to halt decoding after this.
        /// </summary>
        /// <param name="decoder"></param>
        /// <param name="status"></param>
        /// <param name="client_data"></param>
#if NET5_0_OR_GREATER
        [UnmanagedCallersOnly]
#endif
        private void ErrorCallbackInternal(IntPtr decoder, FlacInterop.FLAC__StreamDecoderErrorStatus status, IntPtr client_data)
        {
            try
            {
                _traceLogger.Log("Error raised by flac decoder: " + status.ToString(), LogLevel.Err);
                _endOfStreamOnDecoder = true;
            }
            catch (Exception) { }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(NativeFlacDecoder));
            }

            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (bufferOffset < 0) throw new ArgumentOutOfRangeException(nameof(bufferOffset));
            if (numSamplesPerChannel <= 0) throw new ArgumentOutOfRangeException(nameof(numSamplesPerChannel));
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            int samplesPerChannelReturnedToCaller = 0;
            
            while (samplesPerChannelReturnedToCaller < numSamplesPerChannel && !this.PlaybackFinished)
            {
                // Copy what we can from our scratch buffer first
                int samplesPerChannelCanCopyFromDecoderBuffer = FastMath.Min(numSamplesPerChannel - samplesPerChannelReturnedToCaller, _samplesPerChannelInDecoderOutputBuffer);
                if (samplesPerChannelCanCopyFromDecoderBuffer > 0)
                {
                    // Copy from scratch to output
                    ArrayExtensions.MemCopy(
                        _decoderOutputBuffer.Buffer,
                        0,
                        buffer,
                        (bufferOffset + (samplesPerChannelReturnedToCaller * OutputFormat.NumChannels)),
                        samplesPerChannelCanCopyFromDecoderBuffer * OutputFormat.NumChannels);

                    // Shift scratch to the left if needed
                    int samplesPerChannelRemainingInBuffer = _samplesPerChannelInDecoderOutputBuffer - samplesPerChannelCanCopyFromDecoderBuffer;
                    if (samplesPerChannelRemainingInBuffer > 0)
                    {
                        ArrayExtensions.MemMove(
                            _decoderOutputBuffer.Buffer,
                            samplesPerChannelCanCopyFromDecoderBuffer * OutputFormat.NumChannels,
                            0,
                            samplesPerChannelRemainingInBuffer * OutputFormat.NumChannels);
                    }

                    samplesPerChannelReturnedToCaller += samplesPerChannelCanCopyFromDecoderBuffer;
                    _samplesPerChannelInDecoderOutputBuffer = samplesPerChannelRemainingInBuffer;
                }
                else if (!_endOfStreamOnDecoder)
                {
                    // Attempt to fill the input buffer
                    await TryFillInternalReadBuffer(cancelToken, realTime).ConfigureAwait(false);

                    // Attempt to decode one frame. Do this even if the input buffer is empty, because the callbacks will correctly report eof to the flac decoder
                    _flacLibrary.DecodeSingle(_decoder);

                    FlacInterop.FLAC__StreamDecoderState decoderState = _flacLibrary.GetDecoderState(_decoder);
                    if (decoderState == FlacInterop.FLAC__StreamDecoderState.FLAC__STREAM_DECODER_END_OF_STREAM ||
                        decoderState == FlacInterop.FLAC__StreamDecoderState.FLAC__STREAM_DECODER_OGG_ERROR ||
                        decoderState == FlacInterop.FLAC__StreamDecoderState.FLAC__STREAM_DECODER_SEEK_ERROR ||
                        decoderState == FlacInterop.FLAC__StreamDecoderState.FLAC__STREAM_DECODER_ABORTED ||
                        decoderState == FlacInterop.FLAC__StreamDecoderState.FLAC__STREAM_DECODER_MEMORY_ALLOCATION_ERROR)
                    {
                        _flacLibrary.FinishDecoderStream(_decoder);
                        _endOfStreamOnDecoder = true;
                    }
                }
            }

            if (samplesPerChannelReturnedToCaller == 0 && PlaybackFinished)
            {
                return -1;
            }

            return samplesPerChannelReturnedToCaller;
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            if (disposing)
            {
                _decoderOutputBuffer?.Dispose();
                _decoderInputBuffer?.Dispose();
            }

            try
            {
                _flacLibrary.DestroyDecoder(_decoder);
            }
            catch (Exception e)
            {
                // don't let exceptions in the native layer kill the finalizer thread
                _traceLogger.Log(e);
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
