using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Compression.Zip;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using System.Threading;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.IO;

namespace Durandal.Common.Compression
{
    /// <summary>
    /// Implements a filesystem that exists within a compressed .zip archive. This implementation can be used to
    /// browse or extract files from a zip archive, or to create new archives by initializing empty filesystems.
    /// Note that the performance will not be ideal because the semantics of a random-access filesystem do not
    /// quite match 1:1 with ideal zip file operations. Notably, read+write streams are not supported. Also note 
    /// that this file system implements IDisposable because the zip file needs to be flushed after writing.
    /// </summary>
    public class ZipFileFileSystem : AbstractFileSystem, IDisposable
    {
        private readonly ILogger _logger;
        private readonly ZipFile _archive;
        private bool _touched = false;
        private int _disposed = 0;

        /// <summary>
        /// Creates a file system for an existing or newly created zip file
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="realFileSystem"></param>
        /// <param name="zipFile"></param>
        public ZipFileFileSystem(ILogger logger, IFileSystem realFileSystem, VirtualPath zipFile)
        {
            _logger = logger;
            _archive = new ZipFile(zipFile, logger, realFileSystem);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ZipFileFileSystem()
        {
            Dispose(false);
        }
#endif

        public override NonRealTimeStream OpenStream(VirtualPath path, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            return OpenStream(new FileStreamParams(path, openMode, accessMode, bufferSizeHint));
        }

        public override NonRealTimeStream OpenStream(FileStreamParams fileParams)
        {
            ZipEntry entry = ResolveZipEntry(fileParams.Path);
            if (fileParams.AccessMode == FileAccessMode.Read)
            {
                if (fileParams.OpenMode == FileOpenMode.Create || fileParams.OpenMode == FileOpenMode.CreateNew)
                {
                    throw new ArgumentException("Cannot open a new file as read-only");
                }

                if (entry != null)
                {
                    if (_touched)
                    {
                        _archive.Save();
                        _touched = false;
                    }

                    return new NonRealTimeStreamWrapper(entry.OpenReader(), true);
                }
                else
                {
                    throw new FileNotFoundException("Entry not found in zip archive", fileParams.Path.FullName);
                }
            }
            else if (fileParams.AccessMode == FileAccessMode.Write)
            {
                if (entry != null)
                {
                    // Overwrite existing entry if needed
                    if (fileParams.OpenMode == FileOpenMode.CreateNew)
                    {
                        throw new IOException("Cannot open write stream; file already exists: " + fileParams.Path.FullName);
                    }

                    _archive.RemoveEntry(entry);
                }

                _touched = true;
                return new NonRealTimeStreamWrapper(
                    new ZipEntryWriteStream(ConvertVirtualPathToArchivePath(fileParams.Path), new WeakPointer<ZipFile>(_archive)),
                    true);
            }
            else if (fileParams.AccessMode == FileAccessMode.ReadWrite)
            {
                throw new NotImplementedException("Cannot open R/W streams inside of compressed zip archive entries");
            }

            throw new ArgumentException("Invalid stream parameters");
        }

        public override Task CreateDirectoryAsync(VirtualPath path)
        {
            // no explicit concept of directories inside of a zip archive
            return DurandalTaskExtensions.NoOpTask;
        }

        public override bool Delete(VirtualPath resourceName)
        {
            if (resourceName.IsRoot)
            {
                return false;
            }

            string archivePath = ConvertVirtualPathToArchivePath(resourceName);
            ResourceType type = WhatIs(resourceName);

            bool deleteHappened = false;
            if (type == ResourceType.File)
            {
                _archive.RemoveEntry(archivePath);
                _touched = true;
                deleteHappened = true;
            }
            else if (type == ResourceType.Directory)
            {
                // If we are deleting a directory, do rm -rf on all nested files
                List<string> filesToDelete = new List<string>();

                foreach (string archiveFile in _archive.EntryFileNames)
                {
                    //_logger.Log("Inspecting " + archiveFile);

                    if (archiveFile.StartsWith(archivePath, StringComparison.OrdinalIgnoreCase) &&
                        !archiveFile.EndsWith("/", StringComparison.Ordinal))
                    {
                        filesToDelete.Add(archiveFile);
                    }
                }

                foreach (string fileToDelete in filesToDelete)
                {
                    _archive.RemoveEntries(filesToDelete);
                    _touched = true;
                    deleteHappened = true;
                }
            }

            return deleteHappened;
        }

        public override bool Exists(VirtualPath resourceName)
        {
            ResourceType type = WhatIs(resourceName);
            return type != ResourceType.Unknown;
        }

        public override FileStat Stat(VirtualPath fileName)
        {
            ZipEntry entry = ResolveZipEntry(fileName);
            if (entry != null)
            {
                return new FileStat()
                {
                    Size = entry.UncompressedSize,
                    CreationTime = new DateTimeOffset(entry.CreationTime),
                    LastAccessTime = new DateTimeOffset(entry.LastModified),
                    LastWriteTime = new DateTimeOffset(entry.LastModified)
                };
            }

            return null;
        }

        public override void WriteStat(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime)
        {
            ZipEntry entry = ResolveZipEntry(fileName);
            if (entry != null)
            {
                if (newCreationTime.HasValue)
                {
                    entry.CreationTime = newCreationTime.Value.UtcDateTime;
                }
                if (newModificationTime.HasValue)
                {
                    entry.ModifiedTime = newModificationTime.Value.UtcDateTime;
                }
            }
        }

        public override ResourceType WhatIs(VirtualPath resourceName)
        {
            if (resourceName.IsRoot)
            {
                return ResourceType.Directory;
            }

            string zipBasePath = ConvertVirtualPathToArchivePath(resourceName);
            foreach (string archiveFile in _archive.EntryFileNames)
            {
                //_logger.Log("Inspecting " + archiveFile);
                if (string.Equals(archiveFile, zipBasePath, StringComparison.OrdinalIgnoreCase))
                {
                    return ResourceType.File;
                }
                else if (archiveFile.StartsWith(zipBasePath, StringComparison.OrdinalIgnoreCase) &&
                    //archiveFile.Length > zipBasePath.Length &&
                    archiveFile.IndexOf('/', zipBasePath.Length) > 0)
                {
                    return ResourceType.Directory;
                }
            }

            return ResourceType.Unknown;
        }

        public override void Move(VirtualPath source, VirtualPath target)
        {
            ZipEntry entry = ResolveZipEntry(source);
            if (entry != null)
            {
                entry.FileName = ConvertVirtualPathToArchivePath(target); // No idea how this works
                _touched = true;
            }
        }

        public override IEnumerable<VirtualPath> ListDirectories(VirtualPath resourceContainerName)
        {
            HashSet<VirtualPath> returnVal = new HashSet<VirtualPath>();
            string zipBasePath = ConvertVirtualPathToArchivePath(resourceContainerName);
            foreach (string archiveFile in _archive.EntryFileNames)
            {
                //_logger.Log("Inspecting " + archiveFile);

                if (archiveFile.StartsWith(zipBasePath, StringComparison.OrdinalIgnoreCase) &&
                    archiveFile.Length > zipBasePath.Length)
                {
                    int nextSlashIdx = archiveFile.IndexOf('/', zipBasePath.Length + 1);
                    if (nextSlashIdx > 0)
                    {
                        VirtualPath subDirName = ConvertArchivePathToVirtualPath(archiveFile.Substring(0, nextSlashIdx));
                        if (!returnVal.Contains(subDirName))
                        {
                            returnVal.Add(subDirName);
                            //_logger.Log("Found directory " + subDirName);
                        }
                    }
                }
            }

            return returnVal;
        }

        public override IEnumerable<VirtualPath> ListFiles(VirtualPath resourceContainerName)
        {
            List<VirtualPath> returnVal = new List<VirtualPath>();
            string zipBasePath = ConvertVirtualPathToArchivePath(resourceContainerName);
            foreach (string archiveFile in _archive.EntryFileNames)
            {
                //_logger.Log("Inspecting " + archiveFile);

                if (!archiveFile.EndsWith("/") &&
                    archiveFile.StartsWith(zipBasePath, StringComparison.OrdinalIgnoreCase) &&
                    archiveFile.Length > zipBasePath.Length &&
                    archiveFile.IndexOf('/', zipBasePath.Length + 1) < 0)
                {
                    returnVal.Add(ConvertArchivePathToVirtualPath(archiveFile));
                }
            }

            return returnVal;
        }

        public override Task<IFileSystemWatcher> CreateDirectoryWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories)
        {
            return Task.FromResult(NullFileSystemWatcher.Singleton);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing && _touched)
            {
                _archive.Save();
                _archive.Dispose();
            }
        }

        private ZipEntry ResolveZipEntry(VirtualPath virtualPath)
        {
            string archivePath = ConvertVirtualPathToArchivePath(virtualPath);
            foreach (ZipEntry zipEntry in _archive.Entries) // OPT I wish there were a faster way to fetch the desired entry
            {
                if (string.Equals(zipEntry.FileName, archivePath, StringComparison.OrdinalIgnoreCase))
                {
                    return zipEntry;
                }
            }

            return null;
        }
        
        private static string ConvertVirtualPathToArchivePath(VirtualPath virtualPath)
        {
            return virtualPath.FullName.Replace('\\', '/').Trim('/');
        }

        private static VirtualPath ConvertArchivePathToVirtualPath(string archivePath)
        {
            return new VirtualPath('/' + archivePath);
        }

        public class ZipEntryWriteStream : Stream
        {
            private readonly string _entryName;
            private readonly WeakPointer<ZipFile> _targetFile;
#pragma warning disable CA2213 // "_base" is not disposed
            private MemoryStream _base;
#pragma warning restore CA2213
            private int _disposed = 0;

            public ZipEntryWriteStream(string targetArchivePath, WeakPointer<ZipFile> targetFile)
            {
                _entryName = targetArchivePath;
                _targetFile = targetFile;
                _base = new MemoryStream();
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

            public override bool CanRead
            {
                get
                {
                    return false;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return true;
                }
            }

            public override long Length
            {
                get
                {
                    return _base.Length;
                }
            }

            public override long Position
            {
                get
                {
                    return _base.Position;
                }

                set
                {
                    _base.Position = value;
                }
            }

            public override void Flush()
            {
                _base.Flush();
            }

            protected override void Dispose(bool disposing)
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                try
                {
                    DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                    if (disposing)
                    {
                        // flush the entry
                        _base.Position = 0;
                        _targetFile.Value.AddEntry(_entryName, _base);

                        // we explicitly don't dispose of _base here because we transfer ownership to the zip archive
                        // However, we still do this to appease the code analyzer
                        _base = null;
                        _base?.Dispose();
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }

            /*public new void Close()
            {
                Flush();
                base.Close();
            }*/

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _base.Write(buffer, offset, count);
            }
        }
    }
}
