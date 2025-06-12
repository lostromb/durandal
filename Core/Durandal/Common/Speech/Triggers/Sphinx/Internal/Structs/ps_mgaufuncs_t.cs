using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class ps_mgaufuncs_t
    {
        public Pointer<byte> name;
        public frame_eval_func frame_eval;
        public transform_func transform;

        public delegate int frame_eval_func(
            ps_mgau_t mgau,
            short[] senscr,
            Pointer<byte> senone_active,
            int n_senone_active,
            Pointer<Pointer<float>> feats,
            int frame,
            int compallsen,
            SphinxLogger logger);

        public delegate int transform_func(ps_mgau_t mgau, ps_mllr_t mllr, FileAdapter fileAdapter);
    }
}
