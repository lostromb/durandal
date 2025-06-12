using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using System.Threading;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using Durandal.Common.File;

namespace Durandal.Common.Speech.Triggers.Sphinx
{
    /// <summary>
    /// Contains platform-specific implementations of the Pocketsphinx native bridge
    /// </summary>
    public static class NativePocketSphinxAdapter
    {
        /// <summary>
        /// Attempts to get an instance of the PocketSphinx decoder for whatever platform you are currently running on
        /// </summary>
        public static IPocketSphinx GetPInvokeAdapterForPlatform(IFileSystem modelFileSystem, ILogger logger)
        {
            try
            {
                OSAndArchitecture platform = NativePlatformUtils.GetCurrentPlatform(logger);
                if (platform.OS == PlatformOperatingSystem.Windows ||
                    platform.OS == PlatformOperatingSystem.Linux)
                {
                    NativePlatformUtils.PrepareNativeLibrary("psphinx_trigger", logger);
                    return new NativePocketSphinx();
                }
                else
                {
                    return new PortablePocketSphinx(modelFileSystem, logger);
                }
            }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Err);
                return new PortablePocketSphinx(modelFileSystem, logger);
            }
        }

        private class NativePocketSphinx : IPocketSphinx
        {
            /// <summary>
            /// The approximate size of a loaded trigger model in-memory
            /// </summary>
            private const long MEMORY_PRESSURE = 37000 * 1024;

            private IntPtr _hDecoder = IntPtr.Zero;
            private int _disposed = 0;

            ~NativePocketSphinx()
            {
                Dispose(false);
            }

            [DllImport("psphinx_trigger", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private extern static IntPtr trigger_create(
                [MarshalAs(UnmanagedType.LPStr)] string modelDir,
                [MarshalAs(UnmanagedType.LPStr)] string dictionaryFile,
                bool verboseLogging);

            [DllImport("psphinx_trigger", CallingConvention = CallingConvention.Cdecl)]
            private extern static int trigger_reconfigure(IntPtr decoder,
                [MarshalAs(UnmanagedType.LPStr)]string keywordFile);

            [DllImport("psphinx_trigger", CallingConvention = CallingConvention.Cdecl)]
            private extern static int trigger_start_processing(IntPtr decoder);

            [DllImport("psphinx_trigger", CallingConvention = CallingConvention.Cdecl)]
            private extern static int trigger_stop_processing(IntPtr decoder);

            [DllImport("psphinx_trigger", CallingConvention = CallingConvention.Cdecl)]
            private extern static bool trigger_process_samples(IntPtr decoder, short[] samples, int numSamples);

            [DllImport("psphinx_trigger", CallingConvention = CallingConvention.Cdecl)]
            private extern static void trigger_get_last_hyp(IntPtr decoder, byte[] buffer);

            [DllImport("psphinx_trigger", CallingConvention = CallingConvention.Cdecl)]
            private extern static bool trigger_get_in_speech(IntPtr decoder);

            [DllImport("psphinx_trigger", CallingConvention = CallingConvention.Cdecl)]
            private extern static int trigger_free(IntPtr decoder);

            public bool Create(string modelDir, string dictionaryFile, bool verboseLogging)
            {
                _hDecoder = trigger_create(modelDir, dictionaryFile, verboseLogging);
                return _hDecoder != IntPtr.Zero;
            }

            public bool Reconfigure(KeywordSpottingConfiguration keywordConfig)
            {
                return trigger_reconfigure(_hDecoder, SphinxHelpers.CreateKeywordFile(keywordConfig, NullLogger.Singleton)) == 0;
            }

            public bool Start()
            {
                bool returnVal = trigger_start_processing(_hDecoder) == 0;
                if (returnVal)
                {
                    // Inform the GC about the memory that we just allocated. The models take about 37 megabytes
                    GC.AddMemoryPressure(MEMORY_PRESSURE);
                }

                return returnVal;
            }

            public bool Stop()
            {
                return trigger_stop_processing(_hDecoder) == 0;
            }

            public void ProcessForVad(short[] samples, int numSamples)
            {
                Process(samples, numSamples);
            }

            public string ProcessForKws(short[] samples, int numSamples)
            {
                return Process(samples, numSamples);
            }

            public string Process(short[] samples, int numSamples)
            {
                bool triggered = trigger_process_samples(_hDecoder, samples, numSamples);

                if (triggered)
                {
                    byte[] charBuf = new byte[512];
                    trigger_get_last_hyp(_hDecoder, charBuf);
                    int terminator = 0;
                    while (terminator < charBuf.Length && charBuf[terminator] != 0)
                        terminator++;
                    string str = Encoding.UTF8.GetString(charBuf, 0, terminator).Trim();
                    return str;
                }

                return null;
            }

            public bool IsSpeechDetected()
            {
                return trigger_get_in_speech(_hDecoder);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                if (_hDecoder != IntPtr.Zero)
                {
                    int hResult = trigger_free(_hDecoder);
                    GC.RemoveMemoryPressure(MEMORY_PRESSURE);
                }
            }
        }
    }
}