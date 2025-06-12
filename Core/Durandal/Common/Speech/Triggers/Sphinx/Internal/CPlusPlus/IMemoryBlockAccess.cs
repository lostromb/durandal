using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus
{
    internal interface IMemoryBlockAccess<T>
    {
        T this[uint index] { get; set; }
        T this[int index] { get; set; }

        void Free();
        void Realloc(int newSize);
    }
}
