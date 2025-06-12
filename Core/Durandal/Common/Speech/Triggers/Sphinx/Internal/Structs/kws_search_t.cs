﻿using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class kws_search_t : ps_search_t
    {
        public hmm_context_t hmmctx;        /*< HMM context. */

        public List<kws_keyphrase_t> keyphrases;          /*< Keyphrases to spot */

        public kws_detections_t detections; /*< Keyword spotting history */
        public int frame;            /*< Frame index */

        public int beam;

        public int plp;                    /*< Phone loop probability */
        public int bestscore;              /*< For beam pruning */
        public int def_threshold;          /*< default threshold for p(hyp)/p(altern) ratio */
        public int delay;                  /*< Delay to wait for best detection score */

        public int n_pl;                   /*< Number of CI phones */
        public hmm_t[] pl_hmms;               /*< Phone loop hmms - hmms of CI phones */

        public ptmr_t perf; /*< Performance counter */
        public int n_tot_frame;

        public kws_search_t()
        {
            perf = new ptmr_t();
        }
    }
}
