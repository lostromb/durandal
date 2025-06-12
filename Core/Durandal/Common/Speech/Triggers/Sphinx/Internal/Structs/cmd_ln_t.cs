using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class cmd_ln_t
    {
        public hash_table_t ht;
        public Pointer<Pointer<byte>> f_argv;
        public uint f_argc;
        public SphinxLogger logger;
    }
}
