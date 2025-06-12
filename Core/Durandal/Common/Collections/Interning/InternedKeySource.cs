using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Durandal.Common.Collections.Interning
{
    /// <summary>
    /// Basic atomic interned key source.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class InternedKeySource<T> : IInternedKeySource<T>
    {
        private int _next = -1;

        /// <inheritdoc />
        public InternedKey<T> GenerateNewUniqueValue()
        {
            return new InternedKey<T>(Interlocked.Increment(ref _next));
        }
    }
}
