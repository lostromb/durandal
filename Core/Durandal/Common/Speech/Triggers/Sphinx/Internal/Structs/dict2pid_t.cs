using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class dict2pid_t
    {
        public bin_mdef_t mdef;
        public dict_t dict;
        public Pointer<Pointer<Pointer<ushort>>> ldiph_lc;
        public Pointer<Pointer<xwdssid_t>> rssid;
        public Pointer<Pointer<Pointer<ushort>>> lrdiph_rc;
        public Pointer<Pointer<xwdssid_t>> lrssid;
        public SphinxLogger logger;
    }
}
