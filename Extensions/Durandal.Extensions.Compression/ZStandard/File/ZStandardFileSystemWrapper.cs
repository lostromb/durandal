using Durandal.Common.Collections;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Durandal.Extensions.Compression.ZStandard.File
{
    // TODO implement compression based on file extension as well - use regex allow list matcher class whenever that exists
    // FIXME Make caller awareness of .zst abstraction consistent. All accesses should only be for uncompressed files.
    // E.g. if caller deletes "2021.log" and it is backed by "2021.log.zst", internally we delete the zst file.
    /// <summary>
    /// Wraps a standard <see cref="IFileSystem"/>, and intercepts write stream requests for certain subdirectories,
    /// causing any file writes under those directories to be compressed on the fly to "{original file name}.{original extension}.zst".
    /// Additionally, opening a read stream for any file with a ".zst" extension will be decompressed on read automatically.
    /// This is intended for applications like a file logger where such compression would be desirable.
    /// </summary>
    public class ZStandardFileSystemWrapper : IFileSystem
    {
        private static readonly string ZST_EXTENSION = ".zst";
        private readonly IFileSystem _inner;
        private readonly FastConcurrentHashSet<VirtualPath> _basePathsToCompress;
        private readonly ILogger _logger;
        private readonly int _compressionLevel;
        private readonly int _bufferSize;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="innerFileSystem">The inner filesystem to wrap</param>
        /// <param name="logger">A logger</param>
        /// <param name="compressionLevel">Zstandard compression level between 1 and 22</param>
        /// <param name="bufferSize">The size of input buffers to be sent to the compressor</param>
        public ZStandardFileSystemWrapper(IFileSystem innerFileSystem, ILogger logger, int compressionLevel = 1, int bufferSize = 4096)
        {
            _inner = innerFileSystem.AssertNonNull(nameof(innerFileSystem));
            _logger = logger.AssertNonNull(nameof(logger));
            _basePathsToCompress = new FastConcurrentHashSet<VirtualPath>();
            _bufferSize = bufferSize.AssertNonNegative(nameof(bufferSize));
            _compressionLevel = Math.Max(Math.Min(compressionLevel, ZstdSharp.Compressor.MaxCompressionLevel), ZstdSharp.Compressor.MinCompressionLevel);
        }

        /// <summary>
        /// Gets the set of paths which will be compressed if any file is written to any subdirectory of one of them.
        /// For example, if "/logs" was in the set, then "/logs/main.log" will compress to "/logs/main.log.zst", and
        /// likewise for "/logs/debug/output.log", but not "/output.log".
        /// On file read, any file with a ".zst" extension will be decompressed automatically.
        /// </summary>
        public Durandal.Common.Collections.IReadOnlySet<VirtualPath> BasePathsToCompress => _basePathsToCompress;

        /// <summary>
        /// Adds a new path to the <see cref="BasePathsToCompress"/> set.
        /// </summary>
        /// <param name="path">The base path to add</param>
        public void AddBasePathToCompress(VirtualPath path)
        {
            _basePathsToCompress.Add(path);
        }

        public void ClearBasePathsToCompress()
        {
            _basePathsToCompress.Clear();
        }

        /// <inheritdoc/>
        public void CreateDirectory(VirtualPath directory)
        {
            _inner.CreateDirectory(directory);
        }

        /// <inheritdoc/>
        public Task CreateDirectoryAsync(VirtualPath directory)
        {
            return _inner.CreateDirectoryAsync(directory);
        }

        /// <inheritdoc/>
        public Task<IFileSystemWatcher> CreateDirectoryWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories)
        {
            return _inner.CreateDirectoryWatcher(watchPath, filter, recurseSubdirectories);
        }

        /// <inheritdoc/>
        public bool Delete(VirtualPath path)
        {
            return _inner.Delete(path);
        }

        /// <inheritdoc/>
        public Task<bool> DeleteAsync(VirtualPath path)
        {
            return _inner.DeleteAsync(path);
        }

        /// <inheritdoc/>
        public bool Exists(VirtualPath path)
        {
            return _inner.Exists(path);
        }

        /// <inheritdoc/>
        public Task<bool> ExistsAsync(VirtualPath path)
        {
            return _inner.ExistsAsync(path);
        }

        /// <inheritdoc/>
        public IEnumerable<VirtualPath> ListDirectories(VirtualPath directoryName)
        {
            return _inner.ListDirectories(directoryName);
        }

        /// <inheritdoc/>
        public Task<IEnumerable<VirtualPath>> ListDirectoriesAsync(VirtualPath directoryName)
        {
            return _inner.ListDirectoriesAsync(directoryName);
        }

        /// <inheritdoc/>
        public IEnumerable<VirtualPath> ListFiles(VirtualPath directoryName)
        {
            return _inner.ListFiles(directoryName);
        }

        /// <inheritdoc/>
        public Task<IEnumerable<VirtualPath>> ListFilesAsync(VirtualPath directoryName)
        {
            return _inner.ListFilesAsync(directoryName);
        }

        /// <inheritdoc/>
        public void Move(VirtualPath source, VirtualPath target)
        {
            _inner.Move(source, target);
        }

        /// <inheritdoc/>
        public Task MoveAsync(VirtualPath source, VirtualPath target)
        {
            return _inner.MoveAsync(source, target);
        }

        /// <inheritdoc/>
        public NonRealTimeStream OpenStream(VirtualPath file, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            return OpenStream(new FileStreamParams(file, openMode, accessMode, bufferSizeHint));
        }

        /// <inheritdoc/>
        public NonRealTimeStream OpenStream(FileStreamParams fileParams)
        {
            if (ZstdEnabledForFile(fileParams.Path))
            {
                VirtualPath zstFile = fileParams.Path.Extension.Equals(ZST_EXTENSION, StringComparison.OrdinalIgnoreCase)
                    ? fileParams.Path : new VirtualPath(fileParams.Path.FullName + ZST_EXTENSION);

                if (fileParams.AccessMode == FileAccessMode.Write)
                {
                    Stream innerStream = _inner.OpenStream(fileParams.WithFile(zstFile));
                    return new NonRealTimeStreamWrapper(
                        ZStandardCodec.CreateCompressorStream(innerStream, _logger, _compressionLevel, _bufferSize, isolateInnerStream: false),
                        true);
                }
                // Only decompress on read if there is a matching .zst file that corresponds to the requested path
                // (i.e. the caller requests Program.log, but we see there is a Program.log.zst; use that instead
                // Note that that can cause weird behavior if both the intended and .zst file exist side-by-side).
                // This function also has to handle if the caller passed a .zst file path directly.
                else if (fileParams.AccessMode == FileAccessMode.Read && Exists(zstFile))
                {
                    Stream innerStream = _inner.OpenStream(fileParams.WithFile(zstFile));
                    return new NonRealTimeStreamWrapper(
                        ZStandardCodec.CreateDecompressorStream(innerStream, _logger, _bufferSize, isolateInnerStream: false),
                        true);
                }
            }

            return _inner.OpenStream(fileParams);
        }

        /// <inheritdoc/>
        public Task<NonRealTimeStream> OpenStreamAsync(VirtualPath file, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            return OpenStreamAsync(new FileStreamParams(file, openMode, accessMode, bufferSizeHint));
        }

        /// <inheritdoc/>
        public async Task<NonRealTimeStream> OpenStreamAsync(FileStreamParams fileParams)
        {
            if (ZstdEnabledForFile(fileParams.Path))
            {
                VirtualPath zstFile = fileParams.Path.Extension.Equals(ZST_EXTENSION, StringComparison.OrdinalIgnoreCase)
                    ? fileParams.Path : new VirtualPath(fileParams.Path.FullName + ZST_EXTENSION);

                if (fileParams.AccessMode == FileAccessMode.Write)
                {
                    Stream innerStream = await _inner.OpenStreamAsync(fileParams.WithFile(zstFile)).ConfigureAwait(false);
                    return new NonRealTimeStreamWrapper(
                        ZStandardCodec.CreateCompressorStream(innerStream, DebugLogger.Default, _compressionLevel, _bufferSize, isolateInnerStream: false),
                        true);
                }
                // Only decompress on read if there is a matching .zst file that corresponds to the requested path
                else if (fileParams.AccessMode == FileAccessMode.Read && await ExistsAsync(zstFile))
                {
                    Stream innerStream = await _inner.OpenStreamAsync(fileParams.WithFile(zstFile)).ConfigureAwait(false);
                    return new NonRealTimeStreamWrapper(
                        ZStandardCodec.CreateDecompressorStream(innerStream, _logger, _bufferSize, isolateInnerStream: false),
                        true);
                }
            }

            return await _inner.OpenStreamAsync(fileParams).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public IReadOnlyCollection<string> ReadLines(VirtualPath sourceFile)
        {
            return _inner.ReadLines(sourceFile);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> ReadLinesAsync(VirtualPath sourceFile)
        {
            return _inner.ReadLinesAsync(sourceFile);
        }

        /// <inheritdoc/>
        public FileStat Stat(VirtualPath fileName)
        {
            return _inner.Stat(fileName);
        }

        /// <inheritdoc/>
        public Task<FileStat> StatAsync(VirtualPath fileName)
        {
            return _inner.StatAsync(fileName);
        }

        /// <inheritdoc/>
        public void WriteStat(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime)
        {
            _inner.WriteStat(fileName, newCreationTime, newModificationTime);
        }

        /// <inheritdoc/>
        public Task WriteStatAsync(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime)
        {
            return _inner.WriteStatAsync(fileName, newCreationTime, newModificationTime);
        }

        /// <inheritdoc/>
        public ResourceType WhatIs(VirtualPath resourceName)
        {
            return _inner.WhatIs(resourceName);
        }

        /// <inheritdoc/>
        public Task<ResourceType> WhatIsAsync(VirtualPath resourceName)
        {
            return _inner.WhatIsAsync(resourceName);
        }

        /// <inheritdoc/>
        public void WriteLines(VirtualPath targetFile, IEnumerable<string> data)
        {
            _inner.WriteLines(targetFile, data);
        }

        /// <inheritdoc/>
        public Task WriteLinesAsync(VirtualPath targetFile, IEnumerable<string> data)
        {
            return _inner.WriteLinesAsync(targetFile, data);
        }

        private bool ZstdEnabledForFile(VirtualPath path)
        {
            foreach (VirtualPath basePath in _basePathsToCompress)
            {
                if (path.IsSubsetOf(basePath))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
