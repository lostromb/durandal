#pragma warning disable CS0649

using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class ps_mllr_t
    {
        public int n_class;    /*< Number of MLLR classes. */
        public int n_feat;     /*< Number of feature streams. */
        public Pointer<int> veclen;    /*< Length of input vectors for each stream. */
        public Pointer<Pointer<Pointer<Pointer<float>>>> A;  /*< Rotation part of mean transformations. */
        public Pointer<Pointer<Pointer<float>>> b;   /*< Bias part of mean transformations. */
        public Pointer<Pointer<Pointer<float>>> h;   /*< Diagonal transformation of variances. */
        public Pointer<int> cb2mllr; /*< Mapping from codebooks to transformations. */
    }
}

#pragma warning restore CS0649