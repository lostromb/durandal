using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.File
{
#pragma warning disable CS1574 // System.IO.WatcherChangeTypes is not defined in all target runtimes
    /// <summary>
    /// Defines a type of change that can be made to a file or directory.
    /// Analogue to <see cref="System.IO.WatcherChangeTypes"/>.
    /// </summary>
#pragma warning restore CS1574
    public enum FileSystemChangeType
    {
        FileChanged,
        FileDeleted,
        FileCreated,
        FileRenamed,
        // Not yet supported
        //DirectoryDeleted,
        //DirectoryCreated,
        //DirectoryRenamed
    }
}
