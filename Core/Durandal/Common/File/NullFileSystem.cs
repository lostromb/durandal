using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.File
{
    using Durandal.Common.IO;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// A no-op FileSystem for testing, etc.
    /// All write attempts are blackholed and queries for files always return empty.
    /// </summary>
    public class NullFileSystem : AbstractFileSystem
    {
        public static readonly NullFileSystem Singleton = new NullFileSystem();

        private NullFileSystem() { }

        public override void CreateDirectory(VirtualPath path)
        {
        }

        public override bool Delete(VirtualPath resourceName)
        {
            return false;
        }

        public override bool Exists(VirtualPath resourceName)
        {
            return false;
        }

        public override ResourceType WhatIs(VirtualPath resourceName)
        {
            return ResourceType.Unknown;
        }

        public override IEnumerable<VirtualPath> ListDirectories(VirtualPath resourceContainerName)
        {
            return new VirtualPath[0];
        }

        public override IEnumerable<VirtualPath> ListFiles(VirtualPath resourceContainerName)
        {
            return new VirtualPath[0];
        }

        public override void Move(VirtualPath source, VirtualPath target)
        {
        }

        public override NonRealTimeStream OpenStream(VirtualPath path, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            return new NonRealTimeStreamWrapper(Stream.Null, false);
        }

        public override NonRealTimeStream OpenStream(FileStreamParams fileParams)
        {
            return new NonRealTimeStreamWrapper(Stream.Null, false);
        }

        public override FileStat Stat(VirtualPath resourceName)
        {
            return null;
        }
        public override void WriteStat(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime)
        {
        }

        public override Task<IFileSystemWatcher> CreateDirectoryWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories)
        {
            return Task.FromResult(NullFileSystemWatcher.Singleton);
        }
    }
}
