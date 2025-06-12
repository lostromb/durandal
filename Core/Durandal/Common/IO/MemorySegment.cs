using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Basic implementation of <see cref="ReadOnlySequenceSegment{T}"/>, backed by <see cref="ReadOnlyMemory{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of memory data contained in this segment.</typeparam>
    public class MemorySegment<T> : ReadOnlySequenceSegment<T>
    {
        public MemorySegment(ReadOnlyMemory<T> memory)
        {
            Memory = memory;
        }

        public MemorySegment<T> Append(ReadOnlyMemory<T> memory)
        {
            var segment = new MemorySegment<T>(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };

            Next = segment;

            return segment;
        }
    }
}
