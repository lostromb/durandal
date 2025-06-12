using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.File
{
    /// <summary>
    /// Represents the set of parameters passed when opening a file stream.
    /// </summary>
    public struct FileStreamParams
    {
        /// <summary>
        /// The path to the file being accessed.
        /// </summary>
        public VirtualPath Path { get; private set; }

        /// <summary>
        /// The mode to use when opening the file (create a new file? open existing?)
        /// </summary>
        public FileOpenMode OpenMode { get; private set; }

        /// <summary>
        /// The access pattern this process intends to do with the opened file.
        /// </summary>
        public FileAccessMode AccessMode { get; private set; }

        /// <summary>
        /// What other processes besides this one are allowed to do with the file while we have it opened.
        /// </summary>
        public FileShareMode ShareMode { get; private set; }

        /// <summary>
        /// An optional hint to the underlying file system about what buffer size we want for the returned stream.
        /// </summary>
        public int? BufferSizeHint { get; private set; }

        /// <summary>
        /// An optional hint to the underlying file system about whether to operate in async mode.
        /// Generally faster for large bulk operations, but slower for small random access.
        /// </summary>
        public bool? UseAsyncHint { get; private set; }

        /// <summary>
        /// Constructs new <see cref="FileStreamParams"/> making basic assumptions about what we want
        /// to do with the file based on the openMode.
        /// </summary>
        /// <param name="path">The file path to access.</param>
        /// <param name="openMode">The file open mode (open existing, create new, etc.)</param>
        public FileStreamParams(VirtualPath path, FileOpenMode openMode)
        {
            Path = path.AssertNonNull(nameof(path));
            OpenMode = openMode;
            BufferSizeHint = null;
            UseAsyncHint = null;

            switch (openMode)
            {
                case FileOpenMode.Open:
                    AccessMode = FileAccessMode.Read;
                    ShareMode = FileShareMode.Read;
                    break;
                default:
                    AccessMode = FileAccessMode.ReadWrite;
                    ShareMode = FileShareMode.None;
                    break;
            }
        }

        /// <summary>
        /// Constructs new <see cref="FileStreamParams"/> with basic open mode and access mode.
        /// </summary>
        /// <param name="path">The file path to access.</param>
        /// <param name="openMode">The file open mode (open existing, create new, etc.)</param>
        /// <param name="accessMode">The file access mode.</param>
        public FileStreamParams(VirtualPath path, FileOpenMode openMode, FileAccessMode accessMode)
            : this (path, openMode, accessMode, bufferSizeHint: null)
        {
        }

        /// <summary>
        /// Constructs new <see cref="FileStreamParams"/> with basic open mode and access mode.
        /// </summary>
        /// <param name="path">The file path to access.</param>
        /// <param name="openMode">The file open mode (open existing, create new, etc.)</param>
        /// <param name="accessMode">The file access mode.</param>
        /// <param name="bufferSizeHint">The buffer size hint.</param>
        public FileStreamParams(VirtualPath path, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint)
        {
            Path = path.AssertNonNull(nameof(path));
            OpenMode = openMode;
            AccessMode = accessMode;
            BufferSizeHint = bufferSizeHint;
            UseAsyncHint = null;

            switch (openMode)
            {
                case FileOpenMode.Open:
                    ShareMode = FileShareMode.Read;
                    break;
                default:
                    ShareMode = FileShareMode.None;
                    break;
            }
        }

        /// <summary>
        /// Constructs new <see cref="FileStreamParams"/> with all file modes specified.
        /// </summary>
        /// <param name="path">The file path to access.</param>
        /// <param name="openMode">The file open mode (open existing, create new, etc.)</param>
        /// <param name="accessMode">The file access mode.</param>
        /// <param name="shareMode">The file share mode (which controls what other processes are allowed to do with this file)</param>
        public FileStreamParams(VirtualPath path,
            FileOpenMode openMode,
            FileAccessMode accessMode,
            FileShareMode shareMode)
        {
            Path = path.AssertNonNull(nameof(path));
            OpenMode = openMode;
            AccessMode = accessMode;
            ShareMode = shareMode;
            BufferSizeHint = null;
            UseAsyncHint = null;
        }

        /// <summary>
        /// Constructs new <see cref="FileStreamParams"/>.
        /// </summary>
        /// <param name="path">The file path to access.</param>
        /// <param name="openMode">The file open mode (open existing, create new, etc.)</param>
        /// <param name="accessMode">The file access mode.</param>
        /// <param name="shareMode">The file share mode (which controls what other processes are allowed to do with this file)</param>
        /// <param name="bufferSizeHint">The suggested size of the file buffer in bytes.</param>
        /// <param name="useAsyncHint">Whether to tell the underlying file system to optimize this stream for async access
        /// (typically better for large throughput at the cost of latency)</param>
        public FileStreamParams(VirtualPath path,
            FileOpenMode openMode,
            FileAccessMode accessMode,
            FileShareMode shareMode,
            int? bufferSizeHint,
            bool? useAsyncHint)
        {
            Path = path.AssertNonNull(nameof(path));
            OpenMode = openMode;
            AccessMode = accessMode;
            ShareMode = shareMode;
            BufferSizeHint = bufferSizeHint;
            UseAsyncHint = useAsyncHint;
        }

        /// <summary>
        /// Returns a copy of these file parameters but applied to a new file path.
        /// </summary>
        /// <param name="newFile">The new file path.</param>
        /// <returns>A new copy of these stream parameters.</returns>
        public FileStreamParams WithFile(VirtualPath newFile)
        {
            return new FileStreamParams(
                newFile.AssertNonNull(nameof(newFile)),
                OpenMode,
                AccessMode,
                ShareMode,
                BufferSizeHint,
                UseAsyncHint);
        }
    }
}
