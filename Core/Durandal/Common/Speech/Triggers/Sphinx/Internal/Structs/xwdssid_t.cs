using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class xwdssid_t
    {
        public Pointer<ushort> ssid;
        public Pointer<short> cimap;
        public int n_ssid;
    }
}
