using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Collections
{
    /// <summary>
    /// Performance hacks applied to common language runtime collections.
    /// </summary>
    internal static class ListHacks
    {
        private static readonly string NameOfInternalItemsInSystemList = "_items";
        private static readonly Lazy<FieldInfo> List_Double_InnerArrayAccessor = new Lazy<FieldInfo>(GenerateListAccessor_Double, LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Given a common runtime collection, try and extract the underlying storage
        /// array using reflection. <b>THIS IS A HUGE HACK!</b> At least lock the list for concurrency
        /// as long as you are holding a reference to the returned array.
        /// </summary>
        /// <param name="list">The collection to try and pry open.</param>
        /// <param name="segment">The returned raw array segment.</param>
        /// <returns>True if we were able to access the list internals.</returns>
        public static bool TryGetUnderlyingArraySegment(IEnumerable<double> list, out ArraySegment<double> segment)
        {
            // In modern dotnet we could use CollectionsMarshal.AsSpan for something similar
            segment = default;

            if (list is double[] array)
            {
                segment = new ArraySegment<double>(array, 0, array.Length);
                return true;
            }
            else if (list is List<double> castList)
            {
                if (List_Double_InnerArrayAccessor.Value == null)
                {
                    return false;
                }

                object rawRef = List_Double_InnerArrayAccessor.Value.GetValue(list);
                if (rawRef == null || !(rawRef is double[]))
                {
                    return false;
                }

                segment = new ArraySegment<double>((double[])rawRef, 0, castList.Count);
                return true;
            }
            else
            {
                return false;
            }
        }

        private static FieldInfo GenerateListAccessor_Double()
        {
            try
            {
                // Probe to see if we can pry into the underlying double[] array beneath the List<double>,
                // and if so, cache the reflection accessor that makes that possible
                FieldInfo returnVal = typeof(List<double>).GetRuntimeFields()
                    .Where((s) => string.Equals(NameOfInternalItemsInSystemList, s.Name, StringComparison.Ordinal)).FirstOrDefault();
                if (returnVal == null ||
                    returnVal.FieldType != typeof(double[]))
                {
                    return null;
                }

                return returnVal;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
