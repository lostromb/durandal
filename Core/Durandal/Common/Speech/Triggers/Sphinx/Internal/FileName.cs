using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class FileName
    {
        internal static Pointer<byte> path2basename(Pointer<byte> path)
        {
            Pointer<byte> result;
            result = cstring.strrchr(path, (byte)'\\');
            return (result.IsNull ? path : result + 1);
        }
    }
}
