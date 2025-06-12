using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class hash_entry_t
    {
        public Pointer<byte> key;        /* Key string, null if this is an empty slot.
					    NOTE that the key must not be changed once the entry
					    has been made. */
        public uint len;         /* Key-length; the key string does not have to be a C-style null
					    terminated string; it can have arbitrary binary bytes */
        public object val;          /* Value associated with above key */
        public hash_entry_t next;   /* For collision resolution */
    };
}
