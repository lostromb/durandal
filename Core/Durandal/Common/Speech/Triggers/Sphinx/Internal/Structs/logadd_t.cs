using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class logadd_t
    {
        /* Table, in unsigned integers of (width) bytes. Only one is used at a time */
        public Pointer<byte> table_uint8;
        public Pointer<ushort> table_uint16;
        public Pointer<uint> table_uint32;
        /* Number of elements in (table).  This is never smaller than 256 (important!) */
        public uint table_size;
        /* Width of elements of (table). */
        public byte width;
        /* Right shift applied to elements in (table). */
        public sbyte shift;
    }
}
