using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class ByteOrder
    {
        // TODO TEST THESE

        internal static void SWAP_INT16(Pointer<short> x)
        {
            x.Deref = SWAP_INT16(+x);
        }

        internal static short SWAP_INT16(short x)
        {
            return (short)SWAP_INT16((ushort)x);
        }

        internal static void SWAP_INT16(Pointer<ushort> x)
        {
            x.Deref = SWAP_INT16(+x);
        }

        internal static ushort SWAP_INT16(ushort x)
        {
            return (ushort)((0x00ffU & (x >> 8)) | (0xff00U & (x << 8)));
        }

        internal static void SWAP_INT32(Pointer<int> x)
        {
            x.Deref = SWAP_INT32(+x);
        }

        internal static int SWAP_INT32(int x)
        {
            return (int)SWAP_INT32((uint)x);
        }

        internal static void SWAP_INT32(Pointer<uint> x)
        {
            x.Deref = SWAP_INT32(+x);
        }

        internal static uint SWAP_INT32(uint x)
        {
            return ((0x000000ffU & (x >> 24)) |
                (0x0000ff00U & (x >> 8)) |
                (0x00ff0000U & (x << 8)) |
                (0xff000000U & (x << 24)));
        }

        internal static void SWAP_FLOAT32(Pointer<float> x)
        {
            byte[] bytes = BitConverter.GetBytes(x.Deref);
            byte s;
            s = bytes[0];
            bytes[0] = bytes[3];
            bytes[3] = s;
            s = bytes[1];
            bytes[1] = bytes[2];
            bytes[2] = s;
            x.Deref = BitConverter.ToSingle(bytes, 0);
        }
    }
}
