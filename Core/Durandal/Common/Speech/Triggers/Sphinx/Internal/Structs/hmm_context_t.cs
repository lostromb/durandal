#pragma warning disable CS0649

using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class hmm_context_t
    {
        public int n_emit_state;     /*< Number of emitting states in this set of HMMs. */
        public Pointer<Pointer<Pointer<byte>>> tp;     /*< State transition scores tp[id][from][to] (logs3 values). */
        public short[] senscore;  /*< State emission scores senscore[senid]
                               (negated scaled logs3 values). */
        public Pointer<Pointer<ushort>> sseq;   /*< Senone sequence mapping. */
        public Pointer<int> st_sen_scr;      /*< Temporary array of senone scores (for some topologies). */
        public object udata;            /*< Whatever you feel like, gosh. */
    }
}

#pragma warning restore CS0649