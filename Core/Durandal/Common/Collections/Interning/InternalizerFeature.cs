using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Collections.Interning
{
    public enum InternalizerFeature
    {
        /// <summary>
        /// No special internalizer features are requested
        /// </summary>
        None = 0x0,

        /// <summary>
        /// Use case insensitive comparison (for string internalizers only)
        /// </summary>
        CaseInsensitive = 0x1,

        /// <summary>
        /// Indicates that the internalizer will only ever be used on inputs within
        /// its known input set. In other words, it should never be given unexpected input,
        /// and doing so will result in undefined behavior.
        /// This can be used as a performance optimization internally.
        /// </summary>
        OnlyMatchesWithinSet = 0x2,

        /// <summary>
        /// Indicates that we can take an ordinal key and look up the original unique
        /// value for that ordinal, which is the inverse of the normal lookup.
        /// Not possible if the input set contains distinct values with the same key.
        /// </summary>
        CanDoReverseLookup = 0x4,

        /// <summary>
        /// Hints to an internalizer to avoid large object
        /// heap allocations for its internal tables.
        /// </summary>
        //AvoidLOHAllocations = 0x8,
    }
}
