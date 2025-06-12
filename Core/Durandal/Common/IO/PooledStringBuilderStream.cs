using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.IO
{
    /// <summary>
    /// A copy of <see cref="StringStream" /> which operates on <see cref="PooledStringBuilder"/> objects.
    /// This lets you write the contents of a string builder directly to a stream without ever calling ToString() and making large allocations.
    /// </summary>
    public class PooledStringBuilderStream : StringBuilderReadStream
    {
        private readonly PooledStringBuilder _sourcePooledStringBuilder;
        private readonly bool _disposeInner;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new stream which reads bytes from a string in the given encoding.
        /// </summary>
        /// <param name="input">The string to read from</param>
        /// <param name="encoding">The encoding to use</param>
        /// <param name="leaveOpen">If true, don't dispose of the pooled string builder afterwards.</param>
        public PooledStringBuilderStream(PooledStringBuilder input, Encoding encoding, bool leaveOpen = false)
            : base(input.AssertNonNull(nameof(input)).Builder, 0, (input?.Builder.Length).GetValueOrDefault(0), encoding)
        {
            _sourcePooledStringBuilder = input;
            _disposeInner = !leaveOpen;
        }

        /// <summary>
        /// Constructs a new stream which reads bytes from a string in the given encoding.
        /// </summary>
        /// <param name="input">The string to read from</param>
        /// <param name="charOffset">The initial char offset to use when reading the string</param>
        /// <param name="charCount">The total number of chars to read from the string</param>
        /// <param name="encoding">The encoding to use</param>
        /// <param name="leaveOpen">If true, don't dispose of the pooled string builder afterwards.</param>
        public PooledStringBuilderStream(PooledStringBuilder input, int charOffset, int charCount, Encoding encoding, bool leaveOpen = false)
            : base (input.AssertNonNull(nameof(input)).Builder, charOffset, charCount, encoding)
        {
            _sourcePooledStringBuilder = input;
            _disposeInner = !leaveOpen;
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PooledStringBuilderStream()
        {
            Dispose(false);
        }
#endif

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                if (disposing && _disposeInner)
                {
                    _sourcePooledStringBuilder?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
