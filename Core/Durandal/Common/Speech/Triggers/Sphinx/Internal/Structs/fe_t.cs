using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    /* Structure for the front-end computation. */
    internal class fe_t
    {
        public cmd_ln_t config;

        public float sampling_rate;
        public short frame_rate;
        public short frame_shift;

        public float window_length;
        public short frame_size;
        public short fft_size;

        public byte fft_order;
        public byte feature_dimension;
        public byte num_cepstra;
        public byte remove_dc;
        public byte log_spec;
        public byte swap;
        public byte dither;
        public byte transform;
        public byte remove_noise;
        public byte remove_silence;

        public float pre_emphasis_alpha;
        public short pre_emphasis_prior;
        public int dither_seed;

        public short num_overflow_samps;
        public uint num_processed_samps;

        /* Twiddle factors for FFT. */
        public double[] ccc;
        public double[] sss;
        /* Mel filter parameters. */
        public melfb_t mel_fb;
        /* Half of a Hamming Window. */
        public Pointer<double> hamming_window;

        /* Noise removal  */
        public noise_stats_t noise_stats;

        /* VAD variables */
        public short pre_speech;
        public short post_speech;
        public short start_speech;
        public float vad_threshold;
        public vad_data_t vad_data;

        /* Temporary buffers for processing. */
        /* FIXME: too many of these. */
        public short[] spch;
        public double[] frame;
        public double[] spec;
        public double[] mfspec;
        public short[] overflow_samps;

        public SphinxLogger logger;
    };
}
