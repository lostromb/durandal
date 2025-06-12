using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal struct vq_feature_t
    {
        public int score; /* score or distance */
        public int codeword; /* codeword (vector index) */
    }
}
