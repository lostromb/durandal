using Durandal.Common.IO;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.NativeAudio.Codecs
{
    /// <summary>
    /// P/Invoke adapter for flac
    /// </summary>
    internal unsafe class NativeFlacCodecProvider : INativeFlacCodecProvider
    {
        private const string FLAC_LIBRARY_NAME = "FLAC";

        public SafeHandle CreateDecoder()
        {
            FlacDecoderHandle returnVal = FLAC__stream_decoder_new();
            bool ok = FLAC__stream_decoder_set_md5_checking(returnVal, false);
            ok = ok && FLAC__stream_decoder_set_metadata_ignore_all(returnVal);
            if (!ok)
            {
                throw new Exception("Failed to create flac decoder");
            }

            return returnVal;
        }

        public uint GetDecoderBitsPerSample(SafeHandle decoder)
        {
            if (!(decoder is FlacDecoderHandle decoderHandle))
            {
                throw new InvalidCastException("FLAC decoder handle is invalid");
            }

            return FLAC__stream_decoder_get_bits_per_sample(decoderHandle);
        }

        public uint GetDecoderSampleRate(SafeHandle decoder)
        {
            if (!(decoder is FlacDecoderHandle decoderHandle))
            {
                throw new InvalidCastException("FLAC decoder handle is invalid");
            }

            return FLAC__stream_decoder_get_sample_rate(decoderHandle);
        }

        public uint GetDecoderChannelCount(SafeHandle decoder)
        {
            if (!(decoder is FlacDecoderHandle decoderHandle))
            {
                throw new InvalidCastException("FLAC decoder handle is invalid");
            }

            return FLAC__stream_decoder_get_channels(decoderHandle);
        }

        public FlacInterop.FLAC__StreamDecoderState GetDecoderState(SafeHandle decoder)
        {
            if (!(decoder is FlacDecoderHandle decoderHandle))
            {
                throw new InvalidCastException("FLAC decoder handle is invalid");
            }

            return FLAC__stream_decoder_get_state(decoderHandle);
        }

        public void InitializeDecoderStream(
            SafeHandle decoder,
            FlacInterop.FLAC__StreamDecoderReadCallback readCallback,
            FlacInterop.FLAC__StreamDecoderWriteCallback writeCallback,
            FlacInterop.FLAC__StreamDecoderErrorCallback errorCallback)
        {
            if (!(decoder is FlacDecoderHandle decoderHandle))
            {
                throw new InvalidCastException("FLAC decoder handle is invalid");
            }

            FlacInterop.FLAC__StreamDecoderInitStatus initCode = FLAC__stream_decoder_init_stream(
                decoderHandle,
                read_callback: readCallback, // non-null
                seek_callback: IntPtr.Zero, // seeking not supported
                tell_callback: IntPtr.Zero, // seeking not supported
                length_callback: IntPtr.Zero, // length not supported
                eof_callback: IntPtr.Zero, // we can signal this during read so not needed
                write_callback: writeCallback,
                metadata_callback: IntPtr.Zero,
                error_callback: errorCallback,
                client_data: IntPtr.Zero);
            if (initCode != FlacInterop.FLAC__StreamDecoderInitStatus.FLAC__STREAM_DECODER_INIT_STATUS_OK)
            {
                throw new Exception("Flac library returned error status code " + initCode.ToString());
            }
        }

        public void DecodeSingle(SafeHandle decoder)
        {
            if (!(decoder is FlacDecoderHandle decoderHandle))
            {
                throw new InvalidCastException("FLAC decoder handle is invalid");
            }

            if (!FLAC__stream_decoder_process_single(decoderHandle))
            {
                throw new Exception("Flac library returned error status code");
            }
        }

        public void FinishDecoderStream(SafeHandle decoder)
        {
            if (!(decoder is FlacDecoderHandle decoderHandle))
            {
                throw new InvalidCastException("FLAC decoder handle is invalid");
            }

            if (!FLAC__stream_decoder_finish(decoderHandle))
            {
                throw new Exception("Flac library returned error status code");
            }
        }

        public void DestroyDecoder(SafeHandle decoder)
        {
            if (!(decoder is FlacDecoderHandle decoderHandle))
            {
                throw new InvalidCastException("FLAC decoder handle is invalid");
            }

            decoderHandle.Dispose();
        }

        public SafeHandle CreateEncoder()
        {
            FlacEncoderHandle returnVal = FLAC__stream_encoder_new();
            bool ok = FLAC__stream_encoder_set_verify(returnVal, false);
            if (!ok)
            {
                throw new Exception("Failed to create flac encoder");
            }

            return returnVal;
        }

        public void SetEncoderCompressionLevel(SafeHandle encoder, uint compressionLevel)
        {
            if (!(encoder is FlacEncoderHandle encoderHandle))
            {
                throw new InvalidCastException("FLAC encoder handle is invalid");
            }

            if (!FLAC__stream_encoder_set_compression_level(encoderHandle, compressionLevel))
            {
                throw new Exception("Flac library returned error status code");
            }
        }

        public void SetEncoderBitsPerSample(SafeHandle encoder, uint bitDepth)
        {
            if (!(encoder is FlacEncoderHandle encoderHandle))
            {
                throw new InvalidCastException("FLAC encoder handle is invalid");
            }

            if (!FLAC__stream_encoder_set_bits_per_sample(encoderHandle, bitDepth))
            {
                throw new Exception("Flac library returned error status code");
            }
        }

        public void SetEncoderSampleRate(SafeHandle encoder, uint sampleRate)
        {
            if (!(encoder is FlacEncoderHandle encoderHandle))
            {
                throw new InvalidCastException("FLAC encoder handle is invalid");
            }

            if (!FLAC__stream_encoder_set_sample_rate(encoderHandle, sampleRate))
            {
                throw new Exception("Flac library returned error status code");
            }
        }

        public void SetEncoderChannelCount(SafeHandle encoder, uint channelCount)
        {
            if (!(encoder is FlacEncoderHandle encoderHandle))
            {
                throw new InvalidCastException("FLAC encoder handle is invalid");
            }

            if (!FLAC__stream_encoder_set_channels(encoderHandle, channelCount))
            {
                throw new Exception("Flac library returned error status code");
            }
        }

        public void InitializeEncoderStream(SafeHandle encoder, FlacInterop.FLAC__StreamEncoderWriteCallback writeToStreamCallback)
        {
            if (!(encoder is FlacEncoderHandle encoderHandle))
            {
                throw new InvalidCastException("FLAC encoder handle is invalid");
            }

            int initCode = FLAC__stream_encoder_init_stream(encoderHandle, writeToStreamCallback, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (initCode != 0)
            {
                throw new Exception("Flac library returned error status code " + initCode);
            }
        }

        public void Encode(SafeHandle encoder, int[] in_pcm, int in_pcm_offset, uint num_samples_per_channel)
        {
            if (!(encoder is FlacEncoderHandle encoderHandle))
            {
                throw new InvalidCastException("FLAC encoder handle is invalid");
            }

            fixed (int* inPtr = &in_pcm[in_pcm_offset])
            {
                if (!FLAC__stream_encoder_process_interleaved(encoderHandle, inPtr, num_samples_per_channel))
                {
                    throw new Exception("Flac library returned error status code");
                }
            }
        }

        public void FinishEncoderStream(SafeHandle encoder)
        {
            if (!(encoder is FlacEncoderHandle encoderHandle))
            {
                throw new InvalidCastException("FLAC encoder handle is invalid");
            }

            if (!FLAC__stream_encoder_finish(encoderHandle))
            {
                throw new Exception("Flac library returned error status code");
            }
        }

        public void DestroyEncoder(SafeHandle encoder)
        {
            encoder.Dispose();
        }

        public class FlacDecoderHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public FlacDecoderHandle() : base(ownsHandle: true)
            {
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            protected override bool ReleaseHandle()
            {
                FLAC__stream_decoder_delete(handle);
                return true;
            }
        }

        public class FlacEncoderHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public FlacEncoderHandle() : base(ownsHandle: true)
            {
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            protected override bool ReleaseHandle()
            {
                FLAC__stream_encoder_delete(handle);
                return true;
            }
        }

        #region Decoder imports

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern FlacDecoderHandle FLAC__stream_decoder_new();

        //[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        //public static extern FlacInterop.FLAC__StreamDecoderInitStatus FLAC__stream_decoder_init_file(
        //    IntPtr decoder,
        //    [MarshalAs(UnmanagedType.LPStr)] string filename,
        //    FlacInterop.FLAC__StreamDecoderWriteCallback write_callback,
        //    IntPtr metadata_callback,
        //    FlacInterop.FLAC__StreamDecoderErrorCallback error_callback,
        //    IntPtr client_data);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern FlacInterop.FLAC__StreamDecoderInitStatus FLAC__stream_decoder_init_stream(
            FlacDecoderHandle decoder,
            FlacInterop.FLAC__StreamDecoderReadCallback read_callback,
            IntPtr seek_callback,
            IntPtr tell_callback,
            IntPtr length_callback,
            IntPtr eof_callback,
            FlacInterop.FLAC__StreamDecoderWriteCallback write_callback,
            IntPtr metadata_callback,
            FlacInterop.FLAC__StreamDecoderErrorCallback error_callback,
            IntPtr client_data);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLAC__stream_decoder_delete(IntPtr decoder);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool FLAC__stream_decoder_set_metadata_ignore_all(FlacDecoderHandle decoder);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool FLAC__stream_decoder_set_md5_checking(FlacDecoderHandle decoder, bool value);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLAC__stream_decoder_get_channels(FlacDecoderHandle decoder);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLAC__stream_decoder_get_bits_per_sample(FlacDecoderHandle decoder);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLAC__stream_decoder_get_sample_rate(FlacDecoderHandle decoder);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern FlacInterop.FLAC__StreamDecoderState FLAC__stream_decoder_get_state(FlacDecoderHandle decoder);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool FLAC__stream_decoder_process_single(FlacDecoderHandle decoder);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool FLAC__stream_decoder_finish(FlacDecoderHandle decoder);

        #endregion

        #region Encoder imports

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern FlacEncoderHandle FLAC__stream_encoder_new();

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLAC__stream_encoder_delete(IntPtr encoder);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool FLAC__stream_encoder_set_verify(FlacEncoderHandle encoder, bool value);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool FLAC__stream_encoder_set_channels(FlacEncoderHandle encoder, uint value);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool FLAC__stream_encoder_set_bits_per_sample(FlacEncoderHandle encoder, uint value);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool FLAC__stream_encoder_set_sample_rate(FlacEncoderHandle encoder, uint value);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool FLAC__stream_encoder_set_compression_level(FlacEncoderHandle encoder, uint value);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FLAC__stream_encoder_init_stream(
            FlacEncoderHandle encoder,
            FlacInterop.FLAC__StreamEncoderWriteCallback write_callback,
            IntPtr seek_callback,
            IntPtr tell_callback,
            IntPtr metadata_callback,
            IntPtr client_data);

        //[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        //public static extern int FLAC__stream_encoder_init_file(IntPtr encoder, [MarshalAs(UnmanagedType.LPStr)]string filename, FlacInterop.FLAC__StreamEncoderProgressCallback progress_callback, IntPtr client_data);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool FLAC__stream_encoder_process_interleaved(FlacEncoderHandle encoder, int* buffer, uint samples);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool FLAC__stream_encoder_finish(FlacEncoderHandle encoder);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FLAC__stream_encoder_get_state(FlacEncoderHandle encoder);

        [DllImport(FLAC_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FLAC__stream_encoder_get_verify_decoder_state(FlacEncoderHandle encoder);

        #endregion
    }
}
