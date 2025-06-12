using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.NativeAudio.Codecs
{
    internal interface INativeFlacCodecProvider
    {
        #region Decoder methods

        // https://www.xiph.org/flac/api/group__flac__stream__decoder.html

        /// <summary>
        /// Creates a decoder but does not initialize it.
        /// </summary>
        /// <returns></returns>
        SafeHandle CreateDecoder();

        /// <summary>
        /// Gets the bits per sample in the most recently decoded frame
        /// </summary>
        /// <param name="decoder"></param>
        /// <returns></returns>
        uint GetDecoderBitsPerSample(SafeHandle decoder);

        /// <summary>
        /// Gets the sample rate in the most recently decoded frame
        /// </summary>
        /// <param name="decoder"></param>
        /// <returns></returns>
        uint GetDecoderSampleRate(SafeHandle decoder);

        /// <summary>
        /// Gets the channel count in the most recently decoded frame
        /// </summary>
        /// <param name="decoder"></param>
        /// <returns></returns>
        uint GetDecoderChannelCount(SafeHandle decoder);

        /// <summary>
        /// Gets the current decoder state
        /// </summary>
        /// <param name="decoder"></param>
        /// <returns></returns>
        FlacInterop.FLAC__StreamDecoderState GetDecoderState(SafeHandle decoder);

        /// <summary>
        /// Initializes a decoder using callbacks to handle all I/O
        /// </summary>
        /// <param name="decoder">The decoder to configure</param>
        /// <param name="readCallback">The callback used when reading encoded data</param>
        /// <param name="writeCallback">The callback used when writing decoded data</param>
        /// <param name="errorCallback">The callback used when an error happens.</param>
        void InitializeDecoderStream(
            SafeHandle decoder,
            FlacInterop.FLAC__StreamDecoderReadCallback readCallback,
            FlacInterop.FLAC__StreamDecoderWriteCallback writeCallback,
            FlacInterop.FLAC__StreamDecoderErrorCallback errorCallback);

        /// <summary>
        /// Decode one metadata block or audio frame. This version instructs the decoder to decode a either a single metadata block or a single frame and stop,
        /// unless the callbacks return a fatal error or the read callback returns FLAC__STREAM_DECODER_READ_STATUS_END_OF_STREAM.
        /// As the decoder needs more input it will call the read callback.Depending on what was decoded, the metadata or write callback will be called with the decoded metadata block or audio frame.
        /// Unless there is a fatal read error or end of stream, this function will return once one whole frame is decoded.
        /// In other words, if the stream is not synchronized or points to a corrupt frame header, the decoder will continue to
        /// try and resync until it gets to a valid frame, then decode one frame, then return. If the decoder points to a frame whose
        /// frame CRC in the frame footer does not match the computed frame CRC, this function will issue a FLAC__STREAM_DECODER_ERROR_STATUS_FRAME_CRC_MISMATCH
        /// error to the error callback, and return, having decoded one complete, although corrupt, frame. (Such corrupted frames are sent as silence of the correct length to the write callback.)
        /// </summary>
        /// <param name="decoder"></param>
        void DecodeSingle(SafeHandle decoder);

        /// <summary>
        /// Finish the decoding process. Flushes the decoding buffer, releases resources, resets the decoder settings to their defaults, and returns the decoder state to FLAC__STREAM_DECODER_UNINITIALIZED.
        /// </summary>
        /// <param name="decoder"></param>
        void FinishDecoderStream(SafeHandle decoder);

        /// <summary>
        /// Deletes a native decoder object
        /// </summary>
        /// <param name="decoder"></param>
        void DestroyDecoder(SafeHandle decoder);

        #endregion

        #region Encoder methods

        // https://www.xiph.org/flac/api/group__flac__stream__encoder.html

        /// <summary>
        /// Creates an encoder but does not initialize it
        /// </summary>
        /// <returns></returns>
        SafeHandle CreateEncoder();

        /// <summary>
        /// Sets the compression level on an encoder from 0 to 8
        /// </summary>
        /// <param name="encoder"></param>
        /// <param name="compressionLevel"></param>
        void SetEncoderCompressionLevel(SafeHandle encoder, uint compressionLevel);

        /// <summary>
        /// Sets the bit depth of an encoder, either 16 or 24
        /// </summary>
        /// <param name="encoder"></param>
        /// <param name="bitDepth"></param>
        void SetEncoderBitsPerSample(SafeHandle encoder, uint bitDepth);

        /// <summary>
        /// Sets the sample rate on an encoder
        /// </summary>
        /// <param name="encoder"></param>
        /// <param name="sampleRate"></param>
        void SetEncoderSampleRate(SafeHandle encoder, uint sampleRate);

        /// <summary>
        /// Sets the channel count on an encoder
        /// </summary>
        /// <param name="encoder"></param>
        /// <param name="channelCount"></param>
        void SetEncoderChannelCount(SafeHandle encoder, uint channelCount);

        /// <summary>
        /// Initializes an encoder stream using a callback that will be invoked whenever encoded data is produced
        /// </summary>
        /// <param name="encoder"></param>
        /// <param name="writeToStreamCallback"></param>
        void InitializeEncoderStream(SafeHandle encoder, FlacInterop.FLAC__StreamEncoderWriteCallback writeToStreamCallback);

        /// <summary>
        /// Encodes a set of interleaved input audio. The audio range should be determined by the bit depth of the encoder, but usually +-32767.
        /// Encoded data will be output separately to the callback provided by the initializer.
        /// </summary>
        /// <param name="encoder">An encoder</param>
        /// <param name="in_pcm">Buffer with input audio samples</param>
        /// <param name="in_pcm_offset">The initial offset of in_pcm</param>
        /// <param name="num_samples_per_channel">Number of samples per channel in the interleaved pcm</param>
        void Encode(SafeHandle encoder, int[] in_pcm, int in_pcm_offset, uint num_samples_per_channel);

        /// <summary>
        /// Finishes audio encoding and writes the last page of data to the output stream.
        /// </summary>
        /// <param name="encoder"></param>
        void FinishEncoderStream(SafeHandle encoder);

        /// <summary>
        /// Deletes a native encoder object.
        /// </summary>
        /// <param name="encoder"></param>
        void DestroyEncoder(SafeHandle encoder);

        #endregion
    }
}
