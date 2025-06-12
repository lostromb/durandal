namespace Durandal.Common.File
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.IO;
    using Durandal.Common.Logger;
    using System.Threading.Tasks;
    using System.Threading;
    using File;
    using Durandal.Common.Utils;
    using Durandal.Common.IO;

    /// <summary>
    /// Represents the actual hard-disk file system on the current computer. The default root directory is the current program's working directory.
    /// </summary>
    public class RealFileSystem : AbstractFileSystem
    {
        private const int READ_BUFFER_SIZE = 32768; // based on Win32 benchmarking this seems to be a sweet spot
        private const int WRITE_BUFFER_SIZE = 32768;
        private readonly string _workingDir; // Full base directory path minus trailing slash
        private readonly bool _isReadOnly; // Whether to allow modifying the underlying FS
        private readonly ILogger _logger;

        /// <summary>
        /// Constructs a new <see cref="RealFileSystem"/>.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public RealFileSystem(ILogger logger) : this(logger, null, false)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="RealFileSystem"/>.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="workingDirectory">The working directory to use as the root of the file system.</param>
        public RealFileSystem(ILogger logger, string workingDirectory) : this(logger, workingDirectory, false)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="RealFileSystem"/>.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="workingDirectory">The working directory to use as the root of the file system.</param>
        /// <param name="isReadOnly">Whether to disallow modifications to the underlying file system.</param>
        public RealFileSystem(ILogger logger, string workingDirectory, bool isReadOnly)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _workingDir = string.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory;
            _isReadOnly = isReadOnly;

            DirectoryInfo rootDirectory = new DirectoryInfo(_workingDir);
            if (!rootDirectory.Exists)
            {
                _logger.Log("Could not create filesystem linked to directory \"" + _workingDir + "\" because the directory does not exist!", LogLevel.Err);
                throw new ArgumentException("Cannot create a filesystem for non-existent directory " + _workingDir);
            }

            _workingDir = rootDirectory.FullName.TrimEnd('\\');
            _logger.Log("Real filesystem is initialized - working directory is \"" + _workingDir + "\"");
        }

        /// <summary>
        /// Gets the root directory of this file system on the physical hard disk.
        /// </summary>
        public string RootDirectory => _workingDir;

        /// <summary>
        /// Method specific only to this (non-virtual) filesystem.
        /// Given a virtual path, return the actual path on the system that this virtual path refers to.
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
        public string MapVirtualPathToFileName(VirtualPath resource)
        {
            if (resource.IsRoot)
            {
                return _workingDir;
            }

            return _workingDir + resource.FullName;
        }

        /// <inheritdoc />
        public override NonRealTimeStream OpenStream(
            VirtualPath resourceName,
            FileOpenMode openMode,
            FileAccessMode accessMode,
            int? bufferSizeHint = null)
        {
            return OpenStream(
                new FileStreamParams(
                    resourceName,
                    openMode,
                    accessMode,
                    bufferSizeHint));
        }

        /// <inheritdoc />
        public override NonRealTimeStream OpenStream(FileStreamParams fileParams)
        {
            if (fileParams.AccessMode == FileAccessMode.Read)
            {
                if (File.Exists(MapVirtualPathToFileName(fileParams.Path)))
                {
                    int retries = 0;
                    while (true)
                    {
                        try
                        {
                            return new NonRealTimeStreamWrapper(
                                new FileStream(
                                    MapVirtualPathToFileName(fileParams.Path),
                                    Convert(fileParams.OpenMode),
                                    Convert(fileParams.AccessMode),
                                    Convert(fileParams.ShareMode),
                                    bufferSize: fileParams.BufferSizeHint ?? READ_BUFFER_SIZE,
                                    useAsync: fileParams.UseAsyncHint ?? false),
                                true);
                        }
                        catch (IOException)
                        {
                            // Hack to catch files that are currently in use. Wait a bit and see if they free up. If not, propagate the exception
                            if (retries++ > 5)
                            {
                                throw;
                            }

                            Thread.Sleep(200);
                        }
                    }
                }
                else
                {
                    throw new FileNotFoundException("File not found: " + fileParams.Path.FullName, MapVirtualPathToFileName(fileParams.Path));
                }
            }
            else if (fileParams.AccessMode == FileAccessMode.Write)
            {
                if (_isReadOnly)
                {
                    throw new InvalidOperationException("Read-only file system");
                }

                FileInfo targetFile = new FileInfo(MapVirtualPathToFileName(fileParams.Path));
                if (!targetFile.Directory.Exists)
                {
                    // Do we need to create subdirectories for this file?
                    Directory.CreateDirectory(targetFile.Directory.FullName);
                }

                return new NonRealTimeStreamWrapper(
                    new FileStream(
                        targetFile.FullName,
                        Convert(fileParams.OpenMode),
                        Convert(fileParams.AccessMode),
                        Convert(fileParams.ShareMode),
                        bufferSize: fileParams.BufferSizeHint ?? WRITE_BUFFER_SIZE,
                        useAsync: fileParams.UseAsyncHint ?? false),
                    true);
            }
            else if (fileParams.AccessMode == FileAccessMode.ReadWrite)
            {
                if (_isReadOnly)
                {
                    throw new InvalidOperationException("Read-only file system");
                }

                FileInfo targetFile = new FileInfo(MapVirtualPathToFileName(fileParams.Path));
                if (!targetFile.Directory.Exists)
                {
                    // Do we need to create subdirectories for this file?
                    Directory.CreateDirectory(targetFile.Directory.FullName);
                }

                return new NonRealTimeStreamWrapper(
                    new FileStream(
                        targetFile.FullName,
                        Convert(fileParams.OpenMode),
                        Convert(fileParams.AccessMode),
                        Convert(fileParams.ShareMode),
                        bufferSize: fileParams.BufferSizeHint ?? WRITE_BUFFER_SIZE,
                        useAsync: fileParams.UseAsyncHint ?? false),
                    true);
            }

            return null;
        }

        /// <inheritdoc />
        public override void WriteLines(VirtualPath resourceName, IEnumerable<string> data)
        {
            if (_isReadOnly)
            {
                throw new InvalidOperationException("Read-only file system");
            }

            try
            {
                FileInfo targetFile = new FileInfo(MapVirtualPathToFileName(resourceName));
                if (!targetFile.Directory.Exists)
                {
                    // Do we need to create subdirectories for this file?
                    Directory.CreateDirectory(targetFile.Directory.FullName);
                }

                File.WriteAllLines(MapVirtualPathToFileName(resourceName), data);
            }
            catch (IOException e)
            {
                _logger.Log(e, LogLevel.Err);
            }
        }

        /// <inheritdoc />
        public override IReadOnlyCollection<string> ReadLines(VirtualPath resourceName)
        {
            if (File.Exists(MapVirtualPathToFileName(resourceName)))
            {
                return File.ReadAllLines(MapVirtualPathToFileName(resourceName));
            }

            return null;
        }

#if NETCOREAPP
        /// <inheritdoc />
        public override async Task WriteLinesAsync(VirtualPath resourceName, IEnumerable<string> data)
        {
            if (_isReadOnly)
            {
                throw new InvalidOperationException("Read-only file system");
            }

            try
            {
                FileInfo targetFile = new FileInfo(MapVirtualPathToFileName(resourceName));
                if (!targetFile.Directory.Exists)
                {
                    // Do we need to create subdirectories for this file?
                    Directory.CreateDirectory(targetFile.Directory.FullName);
                }

                await File.WriteAllLinesAsync(MapVirtualPathToFileName(resourceName), data).ConfigureAwait(false);
            }
            catch (IOException e)
            {
                _logger.Log(e, LogLevel.Err);
            }
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyCollection<string>> ReadLinesAsync(VirtualPath resourceName)
        {
            if (File.Exists(MapVirtualPathToFileName(resourceName)))
            {
                return await File.ReadAllLinesAsync(MapVirtualPathToFileName(resourceName)).ConfigureAwait(false);
            }

            return null;
        }
#endif

        /// <inheritdoc />
        public override bool Delete(VirtualPath resourceName)
        {
            if (_isReadOnly)
            {
                throw new InvalidOperationException("Read-only file system");
            }

            try
            {
                string path = MapVirtualPathToFileName(resourceName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    return true;
                }
            }
            catch (IOException e)
            {
                _logger.Log(e, LogLevel.Err);
            }

            return false;
        }

        /// <inheritdoc />
        public override bool Exists(VirtualPath resourceName)
        {
            string path = MapVirtualPathToFileName(resourceName);
            return new FileInfo(path).Exists || new DirectoryInfo(path).Exists;
        }

        /// <inheritdoc />
        public override ResourceType WhatIs(VirtualPath resourceName)
        {
            string nativePath = MapVirtualPathToFileName(resourceName);
            if (new DirectoryInfo(nativePath).Exists)
            {
                return ResourceType.Directory;
            }
            else if (new FileInfo(nativePath).Exists)
            {
                return ResourceType.File;
            }
            else
            {
                return ResourceType.Unknown;
            }
        }

        /// <inheritdoc />
        public override void CreateDirectory(VirtualPath path)
        {
            if (_isReadOnly)
            {
                throw new InvalidOperationException("Read-only file system");
            }

            Directory.CreateDirectory(MapVirtualPathToFileName(path));
        }

        /// <inheritdoc />
        public override IEnumerable<VirtualPath> ListFiles(VirtualPath resourceContainerName)
        {
            string nativePath = MapVirtualPathToFileName(resourceContainerName);

            DirectoryInfo dir = new DirectoryInfo(nativePath);
            if (dir.Exists)
            {
                FileInfo[] fileInfos = dir.GetFiles();
                VirtualPath[] fileNames = new VirtualPath[fileInfos.Length];
                for (int c = 0; c < fileInfos.Length; c++)
                {
                    // FIXME: HACKISH CONVERSION
                    fileNames[c] = new VirtualPath(fileInfos[c].FullName.Remove(0, _workingDir.Length + 1)); // +1 to catch the slash as well
                }
                return fileNames;
            }

            return Enumerable.Empty<VirtualPath>();
        }

        /// <inheritdoc />
        public override IEnumerable<VirtualPath> ListDirectories(VirtualPath resourceContainerName)
        {
            string nativePath = MapVirtualPathToFileName(resourceContainerName);

            DirectoryInfo dir = new DirectoryInfo(nativePath);
            if (dir.Exists)
            {
                DirectoryInfo[] dirInfos = dir.GetDirectories();
                VirtualPath[] containerNames = new VirtualPath[dirInfos.Length];
                for (int c = 0; c < dirInfos.Length; c++)
                {
                    // FIXME: HACKISH CONVERSION
                    containerNames[c] = new VirtualPath(dirInfos[c].FullName.Remove(0, _workingDir.Length + 1)); // +1 to catch the slash as well
                }
                return containerNames;
            }

            return Enumerable.Empty<VirtualPath>();
        }

        /// <inheritdoc />
        public override FileStat Stat(VirtualPath resourceName)
        {
            string path = MapVirtualPathToFileName(resourceName);
            if (File.Exists(path))
            {
                FileInfo fileInfo = new FileInfo(path);
                return new FileStat()
                {
                    CreationTime = fileInfo.CreationTimeUtc,
                    LastAccessTime = fileInfo.LastAccessTimeUtc,
                    LastWriteTime = fileInfo.LastWriteTimeUtc,
                    Size = fileInfo.Length
                };
            }

            return null;
        }

        public override void WriteStat(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime)
        {
            string path = MapVirtualPathToFileName(fileName);
            if (File.Exists(path))
            {
                FileInfo fileInfo = new FileInfo(path);
                if (newCreationTime.HasValue)
                {
                    fileInfo.CreationTimeUtc = newCreationTime.Value.UtcDateTime;
                }
                if (newModificationTime.HasValue)
                {
                    fileInfo.LastWriteTimeUtc = newModificationTime.Value.UtcDateTime;
                }
            }
        }

        /// <inheritdoc />
        public override void Move(VirtualPath source, VirtualPath target)
        {
            if (_isReadOnly)
            {
                throw new InvalidOperationException("Read-only file system");
            }

            string sourceFile = MapVirtualPathToFileName(source);
            if (!File.Exists(sourceFile))
            {
                throw new FileNotFoundException("Cannot move non-existent file", sourceFile);
            }

            string targetFile = MapVirtualPathToFileName(target);
            if (File.Exists(targetFile))
            {
                throw new IOException("Cannot rename \"" + sourceFile + "\" to \"" + targetFile + "\" as a file with that name already exists");
            }
            if (Directory.Exists(targetFile))
            {
                throw new IOException("Cannot rename \"" + sourceFile + "\" to \"" + targetFile + "\" as a directory with that name already exists");
            }

            File.Move(sourceFile, targetFile);
        }

        /// <inheritdoc />
        public override Task<IFileSystemWatcher> CreateDirectoryWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories)
        {
            return Task.FromResult<IFileSystemWatcher>(new RealFileSystemWatcher(_workingDir, MapVirtualPathToFileName(watchPath), filter, recurseSubdirectories, _logger));
        }

        private static FileMode Convert(FileOpenMode mode)
        {
            switch (mode)
            {
                case FileOpenMode.Open:
                    return FileMode.Open;
                case FileOpenMode.OpenOrCreate:
                    return FileMode.OpenOrCreate;
                case FileOpenMode.Create:
                    return FileMode.Create;
                case FileOpenMode.CreateNew:
                    return FileMode.CreateNew;
                default:
                    throw new ArgumentException("Unimplemented FileOpenMode: " + mode);
            }
        }

        private static FileAccess Convert(FileAccessMode mode)
        {
            switch (mode)
            {
                case FileAccessMode.Read:
                    return FileAccess.Read;
                case FileAccessMode.Write:
                    return FileAccess.Write;
                case FileAccessMode.ReadWrite:
                    return FileAccess.ReadWrite;
                default:
                    throw new ArgumentException("Unimplemented FileAccessMode: " + mode);
            }
        }

        private static FileShare Convert(FileShareMode mode)
        {
            FileShare returnVal = FileShare.None;
            if ((mode & FileShareMode.Read) != 0)
            {
                returnVal |= FileShare.Read;
            }

            if ((mode & FileShareMode.Write) != 0)
            {
                returnVal |= FileShare.Write;
            }

            if ((mode & FileShareMode.Delete) != 0)
            {
                returnVal |= FileShare.Delete;
            }

            return returnVal;
        }
    }
}
