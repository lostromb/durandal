using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs.Opus.Common;
using Durandal.Common.Logger;
using Durandal.Common.Utils.NativePlatform;
using Microsoft.Win32.SafeHandles;
using System;
using System.Numerics;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

namespace Durandal.Extensions.NativeAudio
{
    internal class NativeSpeexResampler : SafeHandleZeroOrMinusOneIsInvalid, IResampler
    {
        private const string LIBRARY_NAME = "speexdsp";

        private static readonly int[] FILTER_BASE_LENGTHS = new int[11]
        {
            8, 16, 32, 48, 64, 80, 96, 128, 160, 192, 256
        };

        internal TimeSpan _outputLatency;

        /// <summary>
        /// Globally initialize the speexdsp library for use.
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static bool Initialize(ILogger logger)
        {
            return NativePlatformUtils.PrepareNativeLibrary(LIBRARY_NAME, logger) == NativeLibraryStatus.Available;
        }

        internal NativeSpeexResampler() : base(ownsHandle: true)
        {
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            speex_resampler_destroy(this.handle);
            return true;
        }

        public unsafe void ProcessInterleaved(float[] input, int input_ptr, ref int in_len, float[] output, int output_ptr, ref int out_len)
        {
            fixed (float* inPtr = &input[input_ptr])
            fixed (float* outPtr = &output[output_ptr])
            {
                uint inLenUint = (uint)in_len;
                uint outLenUint = (uint)out_len;
                speex_resampler_process_interleaved_float(this, inPtr, ref inLenUint, outPtr, ref outLenUint);
                in_len = (int)inLenUint;
                out_len = (int)outLenUint;
            }
        }

        public TimeSpan OutputLatency
        {
            get
            {
                return _outputLatency;
                // For whatever infuriating reason, when I compile the libspeex dll, it includes all the exported functions EXCEPT for this one. Wtf?
                //return speex_resampler_get_output_latency(this);
            }
        }

        /// <summary>
        /// Creates a new Speex resampler, prioritizing a native implementation but falling back to managed code
        /// if something fails.
        /// </summary>
        /// <param name="numChannels">The number of channels to be processed</param>
        /// <param name="inRate">Input sampling rate, in hertz</param>
        /// <param name="outRate">Output sampling rate, in hertz</param>
        /// <param name="quality">Resampling quality</param>
        /// <param name="logger">A logger</param>
        /// <returns></returns>
        internal static IResampler Create(int numChannels, int inRate, int outRate, AudioProcessingQuality quality, ILogger logger)
        {
            int integerQuality = SpeexResampler.ConvertEnumQualityToInteger(quality);

            // In high quality mode on modern processors, the C# code actually performs better because it can
            // take advantage of higher vector widths than the native library which can only use SSE
            if (Vector.IsHardwareAccelerated &&
                Vector<float>.Count >= 8 &&
                integerQuality > 7)
            {
                return new SpeexResampler(numChannels, inRate, outRate, integerQuality);
            }

            try
            {
                NativeSpeexResampler returnVal;
                OSAndArchitecture currentPlatform = NativePlatformUtils.GetCurrentPlatform(logger);

                // x86 builds of libspeexdsp will usually assume SSE support
                // without runtime CPU detection. In the rare case that we're
                // running such a configuration but don't have SSE, fallback to managed
                bool failsX86SSERequirement =
                    (currentPlatform.Architecture == PlatformArchitecture.X64 ||
                    currentPlatform.Architecture == PlatformArchitecture.I386) &&
                    !Vector.IsHardwareAccelerated;
                if (!failsX86SSERequirement)
                {
                    int err;
                    returnVal = speex_resampler_init((uint)numChannels, (uint)inRate, (uint)outRate, integerQuality, out err);

                    if (err != 0)
                    {
                        logger.Log($"Could not create native Speex resampler: error {err}. Falling back to managed implementation", LogLevel.Wrn);
                        return new SpeexResampler(numChannels, inRate, outRate, integerQuality);
                    }
                    else
                    {
                        // we have to calculate the output latency ourself for dumb reasons
                        int filt_len = FILTER_BASE_LENGTHS[integerQuality];
                        filt_len = ((filt_len - 1) & (~0x7)) + 8;

                        if (inRate > outRate)
                        {
                            filt_len = filt_len * inRate / outRate;
                            filt_len = ((filt_len - 1) & (~0x7)) + 8;
                        }

                        returnVal._outputLatency = AudioMath.ConvertSamplesPerChannelToTimeSpan(outRate, ((filt_len / 2) * outRate + (inRate >> 1)) / inRate);
                        return returnVal;
                    }
                }
                else
                {
                    return new SpeexResampler(numChannels, inRate, outRate, integerQuality);
                }
            }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Wrn);
                logger.Log("Falling back to managed implementation of resampler", LogLevel.Wrn);
                return new SpeexResampler(numChannels, inRate, outRate, integerQuality);
            }
        }

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeSpeexResampler speex_resampler_init(uint nb_channels, uint in_rate, uint out_rate, int quality, out int err);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int speex_resampler_process_interleaved_float(NativeSpeexResampler st, float* @in, ref uint in_len, float* @out, ref uint out_len);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void speex_resampler_destroy(IntPtr st);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int speex_resampler_get_output_latency(NativeSpeexResampler st);
    }
}
