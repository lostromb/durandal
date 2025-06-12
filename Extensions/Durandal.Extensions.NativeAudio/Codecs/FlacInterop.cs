using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.NativeAudio.Codecs
{
    /// <summary>
    /// Delegates and enumerations used by native flac library.
    /// </summary>
    internal static class FlacInterop
    {
        public enum FLAC__ChannelAssignment
        {
            FLAC__CHANNEL_ASSIGNMENT_INDEPENDENT = 0,
            FLAC__CHANNEL_ASSIGNMENT_LEFT_SIDE = 1,
            FLAC__CHANNEL_ASSIGNMENT_RIGHT_SIDE = 2,
            FLAC__CHANNEL_ASSIGNMENT_MID_SIDE = 3,
        };

        public enum FLAC__FrameNumberType
        {
            FLAC__FRAME_NUMBER_TYPE_FRAME_NUMBER = 0,
            FLAC__FRAME_NUMBER_TYPE_SAMPLE_NUMBER = 1,
        };

        public enum FLAC__StreamEncoderWriteStatus
        {
            FLAC__STREAM_ENCODER_WRITE_STATUS_OK = 0,
            FLAC__STREAM_ENCODER_WRITE_STATUS_FATAL_ERROR = 1,
        }

        public enum FLAC__StreamDecoderWriteStatus
        {
            FLAC__STREAM_DECODER_WRITE_STATUS_CONTINUE = 0,
            FLAC__STREAM_DECODER_WRITE_STATUS_ABORT = 1,
        }

        public enum FLAC__StreamDecoderReadStatus
        {
            FLAC__STREAM_DECODER_READ_STATUS_CONTINUE = 0,
            FLAC__STREAM_DECODER_READ_STATUS_END_OF_STREAM = 1,
            FLAC__STREAM_DECODER_READ_STATUS_ABORT= 2,
        }

        public enum FLAC__StreamDecoderErrorStatus
        {
            FLAC__STREAM_DECODER_ERROR_STATUS_LOST_SYNC = 0,
            FLAC__STREAM_DECODER_ERROR_STATUS_BAD_HEADER = 1,
            FLAC__STREAM_DECODER_ERROR_STATUS_FRAME_CRC_MISMATCH = 2,
            FLAC__STREAM_DECODER_ERROR_STATUS_UNPARSEABLE_STREAM = 3,
        }

        public enum FLAC__StreamDecoderInitStatus
        {
            FLAC__STREAM_DECODER_INIT_STATUS_OK = 0,
            FLAC__STREAM_DECODER_INIT_STATUS_UNSUPPORTED_CONTAINER = 1,
            FLAC__STREAM_DECODER_INIT_STATUS_INVALID_CALLBACKS = 2,
            FLAC__STREAM_DECODER_INIT_STATUS_MEMORY_ALLOCATION_ERROR = 3,
            FLAC__STREAM_DECODER_INIT_STATUS_ERROR_OPENING_FILE = 4,
            FLAC__STREAM_DECODER_INIT_STATUS_ALREADY_INITIALIZED = 5
        }

        public enum FLAC__StreamDecoderState
        {
            FLAC__STREAM_DECODER_SEARCH_FOR_METADATA = 0,
            FLAC__STREAM_DECODER_READ_METADATA = 1,
            FLAC__STREAM_DECODER_SEARCH_FOR_FRAME_SYNC = 2,
            FLAC__STREAM_DECODER_READ_FRAME = 3,
            FLAC__STREAM_DECODER_END_OF_STREAM = 4,
            FLAC__STREAM_DECODER_OGG_ERROR = 5,
            FLAC__STREAM_DECODER_SEEK_ERROR = 6,
            FLAC__STREAM_DECODER_ABORTED = 7,
            FLAC__STREAM_DECODER_MEMORY_ALLOCATION_ERROR = 8,
            FLAC__STREAM_DECODER_UNINITIALIZED = 9
        }

        //public unsafe struct FLAC__Subframe_LPC
        //{
        //    int entropy_coding_method;
        //    uint order;
        //    uint qlp_coeff_precision;
        //    int quantization_level;
        //    int[32] qlp_coeff;
        //    int[32] warmup;
        //    IntPtr residual;
        //}

        // The largest subframe is an LPC frame
        // An LPC frame has size equal to 68 ints + one intptr
        // So we just allocate dummy space for all of that and use the intptr to determine the final size,
        // since we don't care about the actual content
        [StructLayout(LayoutKind.Explicit)]
        public struct FLAC__Subframe
        {
            [FieldOffset(0)]
            public int type;

            [FieldOffset(276)]
            public IntPtr dummy;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct FLAC__FrameHeader
        {
            [FieldOffset(0)]
            public uint blocksize;
            [FieldOffset(4)]
            public uint sample_rate;
            [FieldOffset(8)]
            public uint channels;
            [FieldOffset(12)]
            public FLAC__ChannelAssignment channel_assignment;
            [FieldOffset(16)]
            public uint bits_per_sample;
            [FieldOffset(20)]
            public FLAC__FrameNumberType number_type;
            [FieldOffset(24)]
            public uint frame_number; // frame number and sample number are unioned
            [FieldOffset(24)]
            public ulong sample_number;
            [FieldOffset(32)]
            public byte crc;
        }

        public struct FLAC__FrameFooter
        {
            public ushort crc;
        }

        public struct FLAC__Frame
        {
            public FLAC__FrameHeader header;
            public FLAC__Subframe subframe_0;
            public FLAC__Subframe subframe_1;
            public FLAC__Subframe subframe_2;
            public FLAC__Subframe subframe_3;
            public FLAC__Subframe subframe_4;
            public FLAC__Subframe subframe_5;
            public FLAC__Subframe subframe_6;
            public FLAC__Subframe subframe_7;
            public FLAC__FrameFooter footer;
        }

        /// <summary>
        /// Callback used when the decoder is fetching more data to read
        /// typedef FLAC__StreamDecoderReadStatus(* FLAC__StreamDecoderReadCallback)(const FLAC__StreamDecoder *decoder, FLAC__byte buffer[], size_t *bytes, void *client_data)
        /// </summary>
        /// <param name="decoder">The decoder instance calling the callback.</param>
        /// <param name="buffer">A pointer to a location for the callee to store data to be decoded.</param>
        /// <param name="bytes">A pointer to the size of the buffer. On entry to the callback, it contains the maximum number of bytes that may be stored in buffer. The callee must set it to the actual number of bytes stored (0 in case of error or end-of-stream) before returning.</param>
        /// <param name="client_data">The callee's client data set through FLAC__stream_decoder_init_*().</param>
        /// <returns>The callee's return status</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate FLAC__StreamDecoderReadStatus FLAC__StreamDecoderReadCallback(IntPtr decoder, byte* buffer, ref uint bytes, IntPtr client_data);

        /// <summary>
        /// Callback used when writing decoded audio data to an output stream.
        /// typedef FLAC__StreamDecoderWriteStatus(* FLAC__StreamDecoderWriteCallback)(const FLAC__StreamDecoder *decoder, const FLAC__Frame *frame, const FLAC__int32 *const buffer[], void *client_data)
        /// </summary>
        /// <param name="decoder">The decoder instance calling the callback.</param>
        /// <param name="frame">The description of the decoded frame.</param>
        /// <param name="buffer">An array of pointers to decoded channels of data. Each pointer will point to an array of signed samples of length frame->header.blocksize. Channels will be ordered according to the FLAC specification</param>
        /// <param name="client_data">The callee's client data set through FLAC__stream_decoder_init_*().</param>
        /// <returns>The callee's return status</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate FLAC__StreamDecoderWriteStatus FLAC__StreamDecoderWriteCallback(IntPtr decoder, FLAC__Frame* frame, int** buffer, IntPtr client_data);

        /// <summary>
        /// Callback used when a decoder encounters an error
        /// typedef void(* FLAC__StreamDecoderErrorCallback)(const FLAC__StreamDecoder *decoder, FLAC__StreamDecoderErrorStatus status, void *client_data)
        /// </summary>
        /// <param name="decoder">The decoder instance calling the callback.</param>
        /// <param name="status">The error encountered by the decoder.</param>
        /// <param name="client_data">The callee's client data set through FLAC__stream_decoder_init_*().</param>
        /// <returns>The callee's return status</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FLAC__StreamDecoderErrorCallback(IntPtr decoder, FLAC__StreamDecoderErrorStatus status, IntPtr client_data);

        /// <summary>
        /// Callback used when writing encoded data to an output stream.
        /// typedef FLAC__StreamEncoderWriteStatus(* FLAC__StreamEncoderWriteCallback)(const FLAC__StreamEncoder *encoder, const FLAC__byte buffer[], size_t bytes, unsigned samples, unsigned current_frame, void *client_data)
        /// </summary>
        /// <param name="encoder">The encoder instance calling the callback.</param>
        /// <param name="buffer">An array of encoded data of length bytes.</param>
        /// <param name="bytes">The byte length of buffer</param>
        /// <param name="samples">The number of samples encoded by buffer. 0 has a special meaning; see above.</param>
        /// <param name="current_frame">The number of the current frame being encoded.</param>
        /// <param name="client_data">The callee's client data set through FLAC__stream_encoder_init_*().</param>
        /// <returns>The callee's return status</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate FLAC__StreamEncoderWriteStatus FLAC__StreamEncoderWriteCallback(IntPtr encoder, byte* buffer, uint bytes, uint samples, uint current_frame, IntPtr client_data);

        /// <summary>
        /// Callback used when the encoder is reporting its progress.
        /// typedef void(* FLAC__StreamEncoderProgressCallback)(const FLAC__StreamEncoder *encoder, FLAC__uint64 bytes_written, FLAC__uint64 samples_written, unsigned frames_written, unsigned total_frames_estimate, void *client_data)
        /// </summary>
        /// <param name="encoder">The encoder instance calling the callback.</param>
        /// <param name="bytes_written"></param>
        /// <param name="samples_written"></param>
        /// <param name="frames_written"></param>
        /// <param name="total_frames_estimate"></param>
        /// <param name="client_data">The callee's client data set through FLAC__stream_encoder_init_*(). </param>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void FLAC__StreamEncoderProgressCallback(IntPtr encoder, ulong bytes_written, ulong samples_written, uint frames_written, uint total_frames_estimate, IntPtr client_data);
    }
}
