using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.File
{
    public class FileStat
    {
        /// <summary>
        /// st_mtime - Retrieves the last modification time of this file according to the underlying file system
        /// </summary>
        public DateTimeOffset LastWriteTime { get; set; }

        public DateTimeOffset CreationTime { get; set; }

        public DateTimeOffset LastAccessTime { get; set; }

        /// <summary>
        /// st_size - The total file size in bytes
        /// </summary>
        public long Size { get; set; }
    }
}
