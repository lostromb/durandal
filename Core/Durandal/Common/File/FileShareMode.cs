using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.File
{
    /// <summary>
    /// Determines how other processes can access a file
    /// while this one has a handle open to it.
    /// Corresponds to System.IO.FileShare.
    /// </summary>
    [Flags]
    public enum FileShareMode
    {
        None = 0x0,
        Read = 0x1,
        Write = 0x2,
        ReadWrite = Read | Write,
        Delete = 0x4,
    }
}
