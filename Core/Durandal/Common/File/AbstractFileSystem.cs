using Durandal.Common.IO;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.File
{
    /// <summary>
    /// Provides default implementations of various filesystem operations, and automatically fills in async or synchronous stubs for unimplemented methods.
    /// WARNING! Out of each pair of methods (for example, Move and MoveAsync), at least one of them has to be implemented by a subclass, otherwise you will
    /// get an infinite recursion when you try and call one.
    /// </summary>
    public abstract class AbstractFileSystem : IFileSystem
    {
        /// <inheritdoc />
        public virtual void CreateDirectory(VirtualPath path)
        {
            CreateDirectoryAsync(path).Await();
        }

        /// <inheritdoc />
        public virtual Task CreateDirectoryAsync(VirtualPath path)
        {
            CreateDirectory(path);
            return DurandalTaskExtensions.NoOpTask;
        }

        /// <inheritdoc />
        public virtual bool Delete(VirtualPath resourceName)
        {
            return DeleteAsync(resourceName).Await();
        }

        /// <inheritdoc />
        public virtual Task<bool> DeleteAsync(VirtualPath resourceName)
        {
            return Task.FromResult(Delete(resourceName));
        }

        /// <inheritdoc />
        public virtual bool Exists(VirtualPath resourceName)
        {
            return ExistsAsync(resourceName).Await();
        }

        /// <inheritdoc />
        public virtual Task<bool> ExistsAsync(VirtualPath resourceName)
        {
            return Task.FromResult(Exists(resourceName));
        }

        /// <inheritdoc />
        public virtual ResourceType WhatIs(VirtualPath resourceName)
        {
            return WhatIsAsync(resourceName).Await();
        }

        /// <inheritdoc />
        public virtual Task<ResourceType> WhatIsAsync(VirtualPath resourceName)
        {
            return Task.FromResult(WhatIs(resourceName));
        }

        /// <inheritdoc />
        public virtual IEnumerable<VirtualPath> ListDirectories(VirtualPath resourceContainerName)
        {
            return ListDirectoriesAsync(resourceContainerName).Await();
        }

        /// <inheritdoc />
        public virtual Task<IEnumerable<VirtualPath>> ListDirectoriesAsync(VirtualPath resourceContainerName)
        {
            return Task.FromResult(ListDirectories(resourceContainerName));
        }

        /// <inheritdoc />
        public virtual IEnumerable<VirtualPath> ListFiles(VirtualPath resourceContainerName)
        {
            return ListFilesAsync(resourceContainerName).Await();
        }

        /// <inheritdoc />
        public virtual Task<IEnumerable<VirtualPath>> ListFilesAsync(VirtualPath resourceContainerName)
        {
            return Task.FromResult(ListFiles(resourceContainerName));
        }

        // TODO provide default implementation of Move
        /// <inheritdoc />
        public virtual void Move(VirtualPath source, VirtualPath target)
        {
            MoveAsync(source, target).Await();
        }

        /// <inheritdoc />
        public virtual Task MoveAsync(VirtualPath source, VirtualPath target)
        {
            Move(source, target);
            return DurandalTaskExtensions.NoOpTask;
        }

        /// <inheritdoc />
        public virtual NonRealTimeStream OpenStream(VirtualPath path, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            return OpenStreamAsync(path, openMode, accessMode, bufferSizeHint).Await();
        }

        /// <inheritdoc />
        public virtual NonRealTimeStream OpenStream(FileStreamParams fileParameters)
        {
            return OpenStreamAsync(fileParameters).Await();
        }

        /// <inheritdoc />
        public virtual Task<NonRealTimeStream> OpenStreamAsync(VirtualPath path, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            return Task.FromResult(OpenStream(path, openMode, accessMode, bufferSizeHint));
        }

        /// <inheritdoc />
        public virtual Task<NonRealTimeStream> OpenStreamAsync(FileStreamParams fileParameters)
        {
            return Task.FromResult(OpenStream(fileParameters));
        }

        /// <inheritdoc />
        public virtual IReadOnlyCollection<string> ReadLines(VirtualPath resourceName)
        {
            List<string> returnVal = new List<string>();
            using (Stream fileStream = OpenStream(resourceName, FileOpenMode.Open, FileAccessMode.Read))
            {
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    while (!reader.EndOfStream)
                    {
                        returnVal.Add(reader.ReadLine());
                    }
                }
            }

            return returnVal;
        }

        /// <inheritdoc />
        public virtual async Task<IReadOnlyCollection<string>> ReadLinesAsync(VirtualPath resourceName)
        {
            List<string> returnVal = new List<string>();
            using (Stream fileStream = await OpenStreamAsync(resourceName, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
            {
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    while (!reader.EndOfStream)
                    {
                        returnVal.Add(await reader.ReadLineAsync().ConfigureAwait(false));
                    }
                }
            }

            return returnVal;
        }

        /// <inheritdoc />
        public virtual FileStat Stat(VirtualPath resourceName)
        {
            return StatAsync(resourceName).Await();
        }

        /// <inheritdoc />
        public virtual Task<FileStat> StatAsync(VirtualPath resourceName)
        {
            return Task.FromResult(Stat(resourceName));
        }

        /// <inheritdoc />
        public virtual void WriteStat(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime)
        {
            WriteStatAsync(fileName, newCreationTime, newModificationTime).Await();
        }

        /// <inheritdoc />
        public virtual Task WriteStatAsync(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime)
        {
            WriteStat(fileName, newCreationTime, newModificationTime);
            return DurandalTaskExtensions.NoOpTask;
        }

        /// <inheritdoc />
        public virtual void WriteLines(VirtualPath resourceName, IEnumerable<string> data)
        {
            using (Stream fileStream = OpenStream(resourceName, FileOpenMode.Create, FileAccessMode.Write))
            {
                using (StreamWriter writer = new StreamWriter(fileStream))
                {
                    foreach (string line in data)
                    {
                        writer.WriteLine(line);
                    }
                }
            }
        }

        /// <inheritdoc />
        public virtual async Task WriteLinesAsync(VirtualPath resourceName, IEnumerable<string> data)
        {
            using (Stream fileStream = await OpenStreamAsync(resourceName, FileOpenMode.Create, FileAccessMode.Write).ConfigureAwait(false))
            {
                using (StreamWriter writer = new StreamWriter(fileStream))
                {
                    foreach (string line in data)
                    {
                        await writer.WriteLineAsync(line).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <inheritdoc />
        public abstract Task<IFileSystemWatcher> CreateDirectoryWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories);
    }
}
