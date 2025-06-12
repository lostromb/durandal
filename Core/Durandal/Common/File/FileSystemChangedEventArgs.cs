using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.File
{
    /// <summary>
    /// Event args used for filesystem change events (such as watching for when file or directory contents change).
    /// </summary>
    public class FileSystemChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Defines the type of change being made.
        /// </summary>
        public FileSystemChangeType ChangeType;

        /// <summary>
        /// For create, delete, and change events, this is the path of the thing that changed.
        /// For rename events, this is the original path.
        /// </summary>
        public VirtualPath AffectedPath;

        /// <summary>
        /// For rename events only, this is the new path of the file/directory after renaming.
        /// </summary>
        public VirtualPath RenamedPath;
    }
}
