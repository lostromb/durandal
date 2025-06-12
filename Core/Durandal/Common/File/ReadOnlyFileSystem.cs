using Durandal.Common.IO;
using Durandal.Common.Speech.Triggers.Sphinx.Internal;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.File
{
    /// <summary>
    /// Wraps around an existing IFileSystem and blocks all attempts to do anything except open, read, list, and stat
    /// </summary>
    public class ReadOnlyFileSystem : IFileSystem
    {
        private readonly IFileSystem _inner;

        /// <inheritdoc />
        public ReadOnlyFileSystem(IFileSystem innerFileSystem)
        {
            _inner = innerFileSystem.AssertNonNull(nameof(innerFileSystem));
        }

        /// <inheritdoc />
        public void CreateDirectory(VirtualPath directory)
        {
            throw new IOException("Cannot create directory " + directory.FullName + ": Read-only filesystem");
        }

        /// <inheritdoc />
        public Task CreateDirectoryAsync(VirtualPath directory)
        {
            throw new IOException("Cannot create directory " + directory.FullName + ": Read-only filesystem");
        }

        /// <inheritdoc />
        public bool Delete(VirtualPath path)
        {
            throw new IOException("Cannot delete " + path.FullName + ": Read-only filesystem");
        }

        /// <inheritdoc />
        public Task<bool> DeleteAsync(VirtualPath path)
        {
            throw new IOException("Cannot delete " + path.FullName + ": Read-only filesystem");
        }

        /// <inheritdoc />
        public bool Exists(VirtualPath path)
        {
            return _inner.Exists(path);
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(VirtualPath path)
        {
            return _inner.ExistsAsync(path);
        }

        /// <inheritdoc />
        public IEnumerable<VirtualPath> ListDirectories(VirtualPath directoryName)
        {
            return _inner.ListDirectories(directoryName);
        }

        /// <inheritdoc />
        public Task<IEnumerable<VirtualPath>> ListDirectoriesAsync(VirtualPath directoryName)
        {
            return _inner.ListDirectoriesAsync(directoryName);
        }

        /// <inheritdoc />
        public IEnumerable<VirtualPath> ListFiles(VirtualPath directoryName)
        {
            return _inner.ListFiles(directoryName);
        }

        /// <inheritdoc />
        public Task<IEnumerable<VirtualPath>> ListFilesAsync(VirtualPath directoryName)
        {
            return _inner.ListFilesAsync(directoryName);
        }

        /// <inheritdoc />
        public void Move(VirtualPath source, VirtualPath target)
        {
            throw new IOException("Cannot move " + source.FullName + ": Read-only filesystem");
        }

        /// <inheritdoc />
        public Task MoveAsync(VirtualPath source, VirtualPath target)
        {
            throw new IOException("Cannot move " + source.FullName + ": Read-only filesystem");
        }

        /// <inheritdoc />
        public NonRealTimeStream OpenStream(VirtualPath file, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            if (openMode != FileOpenMode.Open ||
                accessMode != FileAccessMode.Read)
            {
                throw new IOException("Cannot open " + file.FullName + ": Read-only filesystem");
            }

            return _inner.OpenStream(file, openMode, accessMode, bufferSizeHint);
        }

        /// <inheritdoc />
        public NonRealTimeStream OpenStream(FileStreamParams fileParams)
        {
            if (fileParams.OpenMode != FileOpenMode.Open ||
                fileParams.AccessMode != FileAccessMode.Read)
            {
                throw new IOException("Cannot open " + fileParams.Path.FullName + ": Read-only filesystem");
            }

            return _inner.OpenStream(fileParams);
        }

        /// <inheritdoc />
        public Task<NonRealTimeStream> OpenStreamAsync(VirtualPath file, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            if (openMode != FileOpenMode.Open ||
                accessMode != FileAccessMode.Read)
            {
                throw new IOException("Cannot open " + file.FullName + ": Read-only filesystem");
            }

            return _inner.OpenStreamAsync(file, openMode, accessMode, bufferSizeHint);
        }

        /// <inheritdoc />
        public Task<NonRealTimeStream> OpenStreamAsync(FileStreamParams fileParams)
        {
            if (fileParams.OpenMode != FileOpenMode.Open ||
                fileParams.AccessMode != FileAccessMode.Read)
            {
                throw new IOException("Cannot open " + fileParams.Path.FullName + ": Read-only filesystem");
            }

            return _inner.OpenStreamAsync(fileParams);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<string> ReadLines(VirtualPath sourceFile)
        {
            return _inner.ReadLines(sourceFile);
        }

        /// <inheritdoc />
        public Task<IReadOnlyCollection<string>> ReadLinesAsync(VirtualPath sourceFile)
        {
            return _inner.ReadLinesAsync(sourceFile);
        }

        /// <inheritdoc />
        public FileStat Stat(VirtualPath fileName)
        {
            return _inner.Stat(fileName);
        }

        /// <inheritdoc />
        public Task<FileStat> StatAsync(VirtualPath fileName)
        {
            return _inner.StatAsync(fileName);
        }

        /// <inheritdoc />
        public virtual void WriteStat(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime)
        {
            throw new IOException("Cannot modify " + fileName.FullName + ": Read-only filesystem");
        }

        /// <inheritdoc />
        public virtual Task WriteStatAsync(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime)
        {
            throw new IOException("Cannot modify " + fileName.FullName + ": Read-only filesystem");
        }

        /// <inheritdoc />
        public ResourceType WhatIs(VirtualPath resourceName)
        {
            return _inner.WhatIs(resourceName);
        }

        /// <inheritdoc />
        public Task<ResourceType> WhatIsAsync(VirtualPath resourceName)
        {
            return _inner.WhatIsAsync(resourceName);
        }

        /// <inheritdoc />
        public void WriteLines(VirtualPath targetFile, IEnumerable<string> data)
        {
            throw new IOException("Cannot write to " + targetFile.FullName + ": Read-only filesystem");
        }

        /// <inheritdoc />
        public Task WriteLinesAsync(VirtualPath targetFile, IEnumerable<string> data)
        {
            throw new IOException("Cannot write to " + targetFile.FullName + ": Read-only filesystem");
        }

        /// <inheritdoc />
        public Task<IFileSystemWatcher> CreateDirectoryWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories)
        {
            return _inner.CreateDirectoryWatcher(watchPath, filter, recurseSubdirectories);
        }
    }
}
