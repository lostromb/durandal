using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.File
{
    /// <summary>
    /// Determines how we want to access a specific
    /// file or file stream.
    /// Corresponds to System.IO.FileAccess.
    /// </summary>
    [Flags]
    public enum FileAccessMode
    {
        Read = 0x1,
        Write = 0x2,
        ReadWrite = Read | Write
    }
}
