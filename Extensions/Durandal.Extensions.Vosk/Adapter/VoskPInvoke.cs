﻿using Durandal.Common.Utils.NativePlatform;

namespace Durandal.Extensions.Vosk.Adapter
{

    internal static class VoskPInvoke
    {
        private const string LIB_NAME = "vosk";


        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_model_new")]
        public static extern global::System.IntPtr new_Model(string jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_model_free")]
        public static extern void delete_Model(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_model_find_word")]
        public static extern int Model_vosk_model_find_word(global::System.Runtime.InteropServices.HandleRef jarg1, string jarg2);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_spk_model_new")]
        public static extern global::System.IntPtr new_SpkModel(string jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_spk_model_free")]
        public static extern void delete_SpkModel(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_new")]
        public static extern global::System.IntPtr new_VoskRecognizer(global::System.Runtime.InteropServices.HandleRef jarg1, float jarg2);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_new_spk")]
        public static extern global::System.IntPtr new_VoskRecognizerSpk(global::System.Runtime.InteropServices.HandleRef jarg1, float jarg2, global::System.Runtime.InteropServices.HandleRef jarg3);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_new_grm")]
        public static extern global::System.IntPtr new_VoskRecognizerGrm(global::System.Runtime.InteropServices.HandleRef jarg1, float jarg2, string jarg3);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_free")]
        public static extern void delete_VoskRecognizer(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_set_max_alternatives")]
        public static extern void VoskRecognizer_SetMaxAlternatives(global::System.Runtime.InteropServices.HandleRef jarg1, int jarg2);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_set_words")]
        public static extern void VoskRecognizer_SetWords(global::System.Runtime.InteropServices.HandleRef jarg1, int jarg2);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_set_partial_words")]
        public static extern void VoskRecognizer_SetPartialWords(global::System.Runtime.InteropServices.HandleRef jarg1, int jarg2);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_set_spk_model")]
        public static extern void VoskRecognizer_SetSpkModel(global::System.Runtime.InteropServices.HandleRef jarg1, global::System.Runtime.InteropServices.HandleRef jarg2);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_accept_waveform")]
        public static extern bool VoskRecognizer_AcceptWaveform(global::System.Runtime.InteropServices.HandleRef jarg1, [global::System.Runtime.InteropServices.In, global::System.Runtime.InteropServices.MarshalAs(global::System.Runtime.InteropServices.UnmanagedType.LPArray)] byte[] jarg2, int jarg3);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_accept_waveform_s")]
        public static extern bool VoskRecognizer_AcceptWaveformShort(global::System.Runtime.InteropServices.HandleRef jarg1, [global::System.Runtime.InteropServices.In, global::System.Runtime.InteropServices.MarshalAs(global::System.Runtime.InteropServices.UnmanagedType.LPArray)] short[] jarg2, int jarg3);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_accept_waveform_f")]
        public static extern bool VoskRecognizer_AcceptWaveformFloat(global::System.Runtime.InteropServices.HandleRef jarg1, [global::System.Runtime.InteropServices.In, global::System.Runtime.InteropServices.MarshalAs(global::System.Runtime.InteropServices.UnmanagedType.LPArray)] float[] jarg2, int jarg3);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_result")]
        public static extern global::System.IntPtr VoskRecognizer_Result(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_partial_result")]
        public static extern global::System.IntPtr VoskRecognizer_PartialResult(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_final_result")]
        public static extern global::System.IntPtr VoskRecognizer_FinalResult(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_recognizer_reset")]
        public static extern void VoskRecognizer_Reset(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_set_log_level")]
        public static extern void SetLogLevel(int jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_gpu_init")]
        public static extern void GpuInit();

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_gpu_thread_init")]
        public static extern void GpuThreadInit();

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_batch_model_new")]
        public static extern global::System.IntPtr new_BatchModel(string jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_batch_model_free")]
        public static extern void delete_BatchModel(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_batch_model_wait")]
        public static extern void wait_BatchModel(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_batch_recognizer_new")]
        public static extern global::System.IntPtr new_VoskBatchRecognizer(global::System.Runtime.InteropServices.HandleRef jarg1, float jarg2);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_batch_recognizer_free")]
        public static extern void delete_VoskBatchRecognizer(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_batch_recognizer_accept_waveform")]
        public static extern bool VoskBatchRecognizer_AcceptWaveform(global::System.Runtime.InteropServices.HandleRef jarg1, [global::System.Runtime.InteropServices.In, global::System.Runtime.InteropServices.MarshalAs(global::System.Runtime.InteropServices.UnmanagedType.LPArray)] byte[] jarg2, int jarg3);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_batch_recognizer_set_nlsml")]
        public static extern void VoskBatchRecognizer_SetNLSML(global::System.Runtime.InteropServices.HandleRef jarg1, int jarg2);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_batch_recognizer_finish_stream")]
        public static extern void VoskBatchRecognizer_FinishStream(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_batch_recognizer_front_result")]
        public static extern global::System.IntPtr VoskBatchRecognizer_FrontResult(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_batch_recognizer_pop")]
        public static extern void VoskBatchRecognizer_Pop(global::System.Runtime.InteropServices.HandleRef jarg1);

        [global::System.Runtime.InteropServices.DllImport(LIB_NAME, EntryPoint = "vosk_batch_recognizer_get_pending_chunks")]
        public static extern int VoskBatchRecognizer_GetPendingChunks(global::System.Runtime.InteropServices.HandleRef jarg1);


    }

}