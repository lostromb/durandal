using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class GenericList
    {
        // FIXME how / where is this used?
        internal static object gnode_ptr(Pointer<gnode_t> g)
        {
            return g.Deref.data;
        }

        internal static int gnode_int32(Pointer<gnode_t> g)
        {
            return (int)g.Deref.data;
        }

        internal static uint gnode_uint32(Pointer<gnode_t> g)
        {
            return (uint)g.Deref.data;
        }

        internal static float gnode_float32(Pointer<gnode_t> g)
        {
            return (float)g.Deref.data;
        }

        internal static double gnode_float64(Pointer<gnode_t> g)
        {
            return (double)g.Deref.data;
        }

        internal static Pointer<gnode_t> gnode_next(Pointer<gnode_t> g)
        {
            return g.Deref.next;
        }

        internal static Pointer<gnode_t> glist_add_ptr(Pointer<gnode_t> g, object ptr)
        {
            Pointer<gnode_t> gn;

            gn = CKDAlloc.ckd_calloc_struct<gnode_t>(1);
            gn.Deref.data = ptr;
            gn.Deref.next = g;
            return gn;      /* Return the new head of the list */
        }
        
        internal static Pointer<gnode_t> glist_add_int(Pointer<gnode_t> g, object val)
        {
            Pointer<gnode_t> gn;

            gn = CKDAlloc.ckd_calloc_struct<gnode_t>(1);
            gn.Deref.data = val;
            gn.Deref.next = g;
            return gn;      /* Return the new head of the list */
        }

        internal static int glist_count(Pointer<gnode_t> g)
        {
            Pointer<gnode_t> gn;
            int n;

            for (gn = g, n = 0; gn.IsNonNull; gn = gn.Deref.next, n++) ;
            return n;
        }
    }
}
