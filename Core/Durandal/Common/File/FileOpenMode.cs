using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.File
{
    /// <summary>
    /// Generic analogue to System.IO.FileMode
    /// </summary>
    public enum FileOpenMode
    {
        /// <summary>
        /// Specifies that the file system should create a new file, and throw an exception if it already exists
        /// </summary>
        CreateNew = 1,

        /// <summary>
        /// Specifies that the file system should create a new file, overwriting the previous one if present
        /// </summary>
        Create = 2,
        
        /// <summary>
        /// Specifies that the file system should open an existing file, and throw an exception if none exists
        /// </summary>
        Open = 3,

        /// <summary>
        /// Specifies that the fily system should open an existing file if present, or create a new one if not present
        /// </summary>
        OpenOrCreate = 4,
    }
}
