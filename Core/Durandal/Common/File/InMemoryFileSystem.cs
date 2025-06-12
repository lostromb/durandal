using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Durandal.Common.Compression;
using Durandal.Common.Compression.LZ4;
using System.Text.RegularExpressions;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.Logger;
using Durandal.Common.IO;
using Durandal.Common.Events;
using Durandal.Common.ServiceMgmt;
using System.Buffers;
using Durandal.Common.Collections;

namespace Durandal.Common.File
{
    /// <summary>
    /// Implements a virtual filesystem (VFS) entirely in-memory.
    /// </summary>
    public class InMemoryFileSystem : AbstractFileSystem
    {
        private const int COMPRESSED_FLAG = 0x80;
        private const int SERIALIZED_VERSION_NUM = 2;
        private static readonly char[] PATH_SPLIT_CHARS = new char[] { '\\', '/' };
        private readonly List<InMemoryFileSystemWatcher> _changeWatchers = new List<InMemoryFileSystemWatcher>();

        private InMemoryFileTreeNode _root;

        public InMemoryFileSystem()
        {
            _root = new InMemoryFileTreeNode(VirtualPath.Root);
        }

        private InMemoryFileSystem(BinaryReader inStream, int serializedVersion)
        {
            _root = InMemoryFileTreeNode.Deserialize(inStream, serializedVersion);
        }

        private InMemoryFileTreeNode IterateToContainer(VirtualPath resource, bool createIfNotExist = false)
        {
            string[] path = resource.FullName.Split(PATH_SPLIT_CHARS, StringSplitOptions.RemoveEmptyEntries);
            InMemoryFileTreeNode cur = _root;
            foreach (string p in path)
            {
                if (string.IsNullOrEmpty(p))
                {
                    continue;
                }

                if (!cur.Containers.ContainsKey(p))
                {
                    if (createIfNotExist)
                    {
                        cur.Containers.Add(p, new InMemoryFileTreeNode(resource));
                    }
                    else
                    {
                        return null;
                    }
                }

                cur = cur.Containers[p];
            }

            return cur;
        }

        public void AddFile(VirtualPath fileName, byte[] data)
        {
            InMemoryFileTreeNode node = IterateToContainer(fileName.Container, true);
            if (node.Files.ContainsKey(fileName.Name))
            {
                node.Files.Remove(fileName.Name);
            }

            node.Files[fileName.Name] = new InMemoryFileReference(new ReadOnlySequence<byte>(data));
        }

        public ReadOnlySequence<byte> GetFile(VirtualPath fileName)
        {
            InMemoryFileTreeNode node = IterateToContainer(fileName.Container, false);
            if (node != null && node.Files.ContainsKey(fileName.Name))
            {
                return node.Files[fileName.Name].Data;
            }

            return ReadOnlySequence<byte>.Empty;
        }

        /// <summary>
        /// Serialized this entire virtual filesystem into a contiguous blob to the specified output stream, with
        /// optional LZ4 compression
        /// </summary>
        /// <param name="outStream">The stream to write to</param>
        /// <param name="compress">Whether to compress the output</param>
        /// <param name="leaveOpen">If true, leave the write stream open after serialization finishes.</param>
        public void Serialize(Stream outStream, bool compress = true, bool leaveOpen = true)
        {
            byte header = (byte)(SERIALIZED_VERSION_NUM | (compress ? COMPRESSED_FLAG : 0));
            outStream.WriteByte(header);

            Stream innerStream = outStream;

            if (compress)
            {
                innerStream = new LZ4Stream(outStream, LZ4StreamMode.Compress);
            }

            using (BinaryWriter writer = new BinaryWriter(innerStream, StringUtils.UTF8_WITHOUT_BOM, leaveOpen))
            {
                _root.Serialize(writer);
            }
        }

        /// <summary>
        /// Attempts to deserialize the in-memory file system from a previously serialized blob.
        /// If deserialization fails, (usually because of a version mismatch) this method will return null
        /// </summary>
        /// <param name="inStream">The stream to read the serialized input from.</param>
        /// <param name="leaveOpen">If true, leave the read stream open after deserialization finishes.</param>
        /// <returns>The deserialized virtual resource manager</returns>
        public static InMemoryFileSystem Deserialize(Stream inStream, bool leaveOpen = true)
        {
            int checkByte = inStream.ReadByte();
            bool isCompressed = (checkByte & COMPRESSED_FLAG) != 0;
            int versionNum = checkByte & (~COMPRESSED_FLAG);
            if (versionNum > SERIALIZED_VERSION_NUM)
            {
                return null;
            }

            Stream innerStream = inStream;

            if (isCompressed)
            {
                innerStream = new LZ4Stream(inStream, LZ4StreamMode.Decompress);
            }

            using (BinaryReader reader = new BinaryReader(innerStream, StringUtils.UTF8_WITHOUT_BOM, leaveOpen))
            {
                InMemoryFileSystem returnVal = new InMemoryFileSystem(reader, versionNum);
                return returnVal;
            }
        }

        /// <inheritdoc />
        public override NonRealTimeStream OpenStream(VirtualPath path, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            return OpenStreamInternal(new FileStreamParams(path, openMode, accessMode, bufferSizeHint), DefaultRealTimeProvider.Singleton);
        }

        /// <inheritdoc />
        public override NonRealTimeStream OpenStream(FileStreamParams fileParameters)
        {
            return OpenStreamInternal(fileParameters, DefaultRealTimeProvider.Singleton);
        }

        private NonRealTimeStream OpenStreamInternal(FileStreamParams fileParams, IRealTimeProvider realTime)
        {
            if (fileParams.OpenMode == FileOpenMode.CreateNew &&
                (fileParams.AccessMode & FileAccessMode.Write) == 0)
            {
                throw new IOException($"Cannot create a new file without write access: {fileParams.Path.FullName}");
            }

            InMemoryFileTreeNode node = IterateToContainer(fileParams.Path.Container, false);
            bool fileExists = node != null && node.Files.ContainsKey(fileParams.Path.Name);

            if (!fileExists && fileParams.OpenMode == FileOpenMode.Open)
            {
                throw new FileNotFoundException($"File not found: {fileParams.Path.FullName}", fileParams.Path.FullName);
            }

            if (fileExists && fileParams.OpenMode == FileOpenMode.CreateNew)
            {
                throw new IOException($"File already exists, cannot overwrite: {fileParams.Path.FullName}");
            }

            if (fileParams.AccessMode == FileAccessMode.Read)
            {
                return new ReadOnlySequenceStream(node.Files[fileParams.Path.Name].Data);
            }
            else // if opening in some form of Write mode --------
            {
                // Create containing directory if needed
                node = IterateToContainer(fileParams.Path.Container, true);

                if (fileExists)
                {
                    ReadOnlySequence<byte> existingFileData = ReadOnlySequence<byte>.Empty;
                    // Overwrite existing file contents if Create was explicitly set
                    if (fileParams.OpenMode == FileOpenMode.Create)
                    {
                        node.Files.Remove(fileParams.Path.Name);

                        // Put a dummy file in the proper place just so that FileExists() checks will succeed
                        node.Files.Add(fileParams.Path.Name, new InMemoryFileReference(existingFileData));

                        // This emulates how Windows file watchers can signal 2 separate events
                        // one for "file is starting to be modified / access time is changing" and another for "file contents are modified" (which happens when the stream closes)
                        NotifyWatchers(new FileSystemChangedEventArgs()
                        {
                            ChangeType = FileSystemChangeType.FileChanged,
                            AffectedPath = fileParams.Path
                        },
                        realTime);
                    }
                    else
                    {
                        // Otherwise open the existing file
                        existingFileData = node.Files[fileParams.Path.Name].Data;
                    }

                    VirtualFileStream returnVal = new VirtualFileStream(this, fileParams.Path, node, realTime, canRead: fileParams.AccessMode == FileAccessMode.ReadWrite);
                    if (existingFileData.Length > 0)
                    {
                        foreach (var segment in existingFileData)
                        {
                            returnVal.Write(segment.Span);
                        }

                        returnVal.Seek(0, SeekOrigin.Begin);
                    }

                    return returnVal;
                }
                else
                {
                    // File doesn't exist, create a new one
                    node.Files.Add(fileParams.Path.Name, new InMemoryFileReference(ReadOnlySequence<byte>.Empty));

                    NotifyWatchers(new FileSystemChangedEventArgs()
                    {
                        ChangeType = FileSystemChangeType.FileCreated,
                        AffectedPath = fileParams.Path
                    },
                    realTime);

                    return new VirtualFileStream(this, fileParams.Path, node, realTime, canRead: fileParams.AccessMode == FileAccessMode.ReadWrite);
                }
            }
        }

        public override Task<bool> DeleteAsync(VirtualPath path)
        {
            return DeleteAsync(path, DefaultRealTimeProvider.Singleton);
        }

        public async Task<bool> DeleteAsync(VirtualPath path, IRealTimeProvider realTime)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            ResourceType type = WhatIs(path);
            if (type == ResourceType.Directory)
            {
                if (path.IsRoot)
                {
                    // Rather than throwing an exception, just wipe this whole filesystem
                    _root = new InMemoryFileTreeNode(VirtualPath.Root);
                    return true;
                }

                InMemoryFileTreeNode node = IterateToContainer(path.Container);
                if (node.Containers.ContainsKey(path.Name))
                {
                    // rm -rf the entire container and its sub files
                    node.Containers.Remove(path.Name);

                    // TODO notify watchers of file deletions
                    return true;
                }
            }
            else if (type == ResourceType.File)
            {
                InMemoryFileTreeNode container = IterateToContainer(path.Container);
                if (container != null &&
                    container.Files.ContainsKey(path.Name))
                {
                    container.Files.Remove(path.Name);
                    NotifyWatchers(new FileSystemChangedEventArgs()
                    {
                        AffectedPath = path,
                        ChangeType = FileSystemChangeType.FileDeleted
                    },
                    realTime);

                    return true;
                }
            }

            return false;
        }

        public override bool Exists(VirtualPath path)
        {
            if (path.IsRoot || WhatIs(path) == ResourceType.Directory)
            {
                return true;
            }
            else
            {
                InMemoryFileTreeNode node = IterateToContainer(path.Container, false);
                return node != null && node.Files.ContainsKey(path.Name);
            }
        }

        public override ResourceType WhatIs(VirtualPath resourceName)
        {
            string[] path = resourceName.FullName.Split(PATH_SPLIT_CHARS, StringSplitOptions.RemoveEmptyEntries);
            InMemoryFileTreeNode cur = _root;
            foreach (string p in path)
            {
                if (cur.Files.ContainsKey(p))
                {
                    return ResourceType.File;
                }
                else if (!cur.Containers.ContainsKey(p))
                {
                    return ResourceType.Unknown;
                }

                cur = cur.Containers[p];
            }

            return ResourceType.Directory;
        }

        public override IEnumerable<VirtualPath> ListFiles(VirtualPath directoryName)
        {
            InMemoryFileTreeNode root = IterateToContainer(directoryName, false);
            if (root == null)
            {
                return Enumerable.Empty<VirtualPath>();
                //throw new FileNotFoundException("The container \"" + directoryName.Container + "\" does not exist");
            }

            IList<VirtualPath> returnVal = new List<VirtualPath>();
            foreach (string c in root.Files.Keys)
            {
                returnVal.Add(directoryName.Combine(c));
            }

            return returnVal;
        }

        public override IEnumerable<VirtualPath> ListDirectories(VirtualPath directoryName)
        {
            InMemoryFileTreeNode root = IterateToContainer(directoryName, false);
            if (root == null)
            {
                return Enumerable.Empty<VirtualPath>();
                //throw new FileNotFoundException("The directory \"" + directoryName.FullName + "\" does not exist");
            }

            IList<VirtualPath> returnVal = new List<VirtualPath>();
            foreach (string c in root.Containers.Keys)
            {
                returnVal.Add(directoryName.Combine(c));
            }

            return returnVal;
        }

        public override FileStat Stat(VirtualPath fileName)
        {
            InMemoryFileTreeNode node = IterateToContainer(fileName.Container, false);
            if (node == null || !node.Files.ContainsKey(fileName.Name))
            {
                return null;
            }

            InMemoryFileReference virtFile = node.Files[fileName.Name];
            return new FileStat()
            {
                CreationTime = virtFile.CreationTime,
                LastWriteTime = virtFile.LastWriteTime,
                Size = (long)virtFile.Data.Length
            };
        }

        public override void WriteStat(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime)
        {
            InMemoryFileTreeNode node = IterateToContainer(fileName.Container, false);
            if (node == null || !node.Files.ContainsKey(fileName.Name))
            {
                return;
            }

            InMemoryFileReference virtFile = node.Files[fileName.Name];
            if (newCreationTime.HasValue)
            {
                virtFile.CreationTime = newCreationTime.Value;
            }
            if (newModificationTime.HasValue)
            {
                virtFile.LastWriteTime = newModificationTime.Value;
            }
        }

        public override void CreateDirectory(VirtualPath path)
        {
        }

        public override Task<IFileSystemWatcher> CreateDirectoryWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories)
        {
            InMemoryFileSystemWatcher newWatcher = new InMemoryFileSystemWatcher(watchPath, filter, recurseSubdirectories);
            lock (_changeWatchers)
            {
                PruneFileWatchers();
                _changeWatchers.Add(newWatcher);
            }

            return Task.FromResult<IFileSystemWatcher>(newWatcher);
        }

        public void WriteLines(VirtualPath resourceName, IEnumerable<string> data, IRealTimeProvider realTime)
        {
            using (Stream fileStream = OpenStreamInternal(new FileStreamParams(resourceName, FileOpenMode.Create, FileAccessMode.Write), realTime))
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

        public async Task WriteLinesAsync(VirtualPath resourceName, IEnumerable<string> data, IRealTimeProvider realTime)
        {
            using (Stream fileStream = OpenStreamInternal(
                new FileStreamParams(resourceName, FileOpenMode.Create, FileAccessMode.Write), realTime))
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

        /// <summary>
        /// Notifies all file system watchers of changes that happened within the file system
        /// </summary>
        /// <param name="args"></param>
        /// <param name="realTime"></param>
        internal void NotifyWatchers(FileSystemChangedEventArgs args, IRealTimeProvider realTime)
        {
            // We make a clone of the watchers list here because otherwise, we could hit a rare bug
            // in which the handling of the Notify() method triggers more file changes which in turn trigger a PruneFileWatchers() event,
            // leading to the list being modified within the same thread stack even though the thread holds the lock.
            List<InMemoryFileSystemWatcher> clonedWatchers;

            lock (_changeWatchers)
            {
                PruneFileWatchers();
                clonedWatchers = new List<InMemoryFileSystemWatcher>(_changeWatchers);
            }

            foreach (var watcher in clonedWatchers)
            {
                watcher.Notify(args, realTime);
            }
        }

        private void PruneFileWatchers()
        {
            List<InMemoryFileSystemWatcher> prunedWatchers = new List<InMemoryFileSystemWatcher>();

            foreach (InMemoryFileSystemWatcher watcher in _changeWatchers)
            {
                if (!watcher.IsDisposed)
                {
                    prunedWatchers.Add(watcher);
                }
            }

            _changeWatchers.Clear();
            _changeWatchers.FastAddRangeList(prunedWatchers);
        }

        // TODO implement (though it would only be for performance reasons)
        //public void Move(VirtualPath source, VirtualPath target)
        //{
        //    throw new NotImplementedException();
        //}

        //public Task MoveAsync(VirtualPath source, VirtualPath target)
        //{
        //    throw new NotImplementedException();
        //}

        #region Helper classes

        /// <summary>
        /// Represents a virtual directory inside of an in-memory file system
        /// </summary>
        private class InMemoryFileTreeNode
        {
            public readonly VirtualPath Path;
            public readonly IDictionary<string, InMemoryFileTreeNode> Containers = new Dictionary<string, InMemoryFileTreeNode>();
            public readonly IDictionary<string, InMemoryFileReference> Files = new Dictionary<string, InMemoryFileReference>();

            public InMemoryFileTreeNode(VirtualPath path)
            {
                Path = path;
            }

            /// <summary>
            /// Reads this virtual filesystem node from a serialized stream
            /// </summary>
            /// <param name="inStream">The stream containing the node data</param>
            /// <param name="path">The path of this node</param>
            private InMemoryFileTreeNode(BinaryReader inStream, VirtualPath path, int version)
            {
                Path = path;
                int numFiles = inStream.ReadInt32();
                for (int f = 0; f < numFiles; f++)
                {
                    string key = inStream.ReadString();
                    long ticks = inStream.ReadInt64();
                    // in protocol 1, we only had modification time
                    // in protocol 2+ we have modification time + creation time
                    DateTimeOffset lastWriteTime = new DateTimeOffset(ticks, TimeSpan.Zero);
                    DateTimeOffset creationTime = lastWriteTime;
                    if (version > 1)
                    {
                        ticks = inStream.ReadInt64();
                        creationTime = new DateTimeOffset(ticks, TimeSpan.Zero);
                    }

                    int fileLength = inStream.ReadInt32();

                    // Read serialized file in LOH-friendly chunks
                    const int MAX_SEGMENT_SIZE = 65536;
                    int sizeProcessed = 0;
                    MemorySegment<byte> firstSegment = null;
                    MemorySegment<byte> lastSegment = null;
                    while (sizeProcessed < fileLength)
                    {
                        int thisSegmentSize = (int)Math.Min(MAX_SEGMENT_SIZE, fileLength - sizeProcessed);
                        byte[] segmentData = new byte[thisSegmentSize];
                        int thisSegmentRead = 0;
                        while (thisSegmentRead < thisSegmentSize)
                        {
                            int amountRead = inStream.Read(segmentData, thisSegmentRead, thisSegmentSize - thisSegmentRead);
                            if (amountRead == 0)
                            {
                                throw new Exception("Unexpected end of stream while deserializing virtual file system");
                            }

                            thisSegmentRead += amountRead;
                        }

                        if (firstSegment == null)
                        {
                            firstSegment = new MemorySegment<byte>(segmentData);
                            lastSegment = firstSegment;
                        }
                        else
                        {
                            lastSegment = lastSegment.Append(segmentData);
                        }

                        sizeProcessed += thisSegmentSize;
                    }

                    ReadOnlySequence<byte> file = new ReadOnlySequence<byte>(firstSegment, 0, lastSegment, lastSegment.Memory.Length);
                    Files[key] = new InMemoryFileReference(file, creationTime, lastWriteTime);
                }

                int numContainers = inStream.ReadInt32();
                for (int f = 0; f < numContainers; f++)
                {
                    string key = inStream.ReadString();
                    InMemoryFileTreeNode subnode = new InMemoryFileTreeNode(inStream, path.Combine(key), version);
                    Containers[key] = subnode;
                }
            }

            /// <summary>
            /// Writes this virtual filesystem node to a stream
            /// </summary>
            /// <param name="outStream"></param>
            /// <returns></returns>
            public void Serialize(BinaryWriter outStream)
            {
                // # of files
                outStream.Write((int)Files.Count);

                // Serialize each file
                foreach (var virtualFile in Files)
                {
                    outStream.Write(virtualFile.Key);
                    outStream.Write((long)virtualFile.Value.LastWriteTime.Ticks);
                    outStream.Write((long)virtualFile.Value.CreationTime.Ticks);
                    outStream.Write((int)virtualFile.Value.Data.Length);
                    ReadOnlySequence<byte> dataSeq = virtualFile.Value.Data;
                    foreach (var segment in dataSeq)
                    {
#if NET5_0_OR_GREATER
                        outStream.Write(segment.Span);
#else
                        using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent(segment.Length))
                        {
                            segment.Span.CopyTo(scratch.Buffer.AsSpan(0, segment.Length));
                            outStream.Write(scratch.Buffer, 0, segment.Length);
                        }
#endif
                    }
                }

                // # of subnodes
                outStream.Write((int)Containers.Count);

                // Serialize each subnode recursively
                foreach (var virtualNode in Containers)
                {
                    outStream.Write(virtualNode.Key);
                    virtualNode.Value.Serialize(outStream);
                }
            }

            public static InMemoryFileTreeNode Deserialize(BinaryReader inStream, int version)
            {
                return new InMemoryFileTreeNode(inStream, VirtualPath.Root, version);
            }
        }

        /// <summary>
        /// A virtual file handle including file metadata
        /// </summary>
        private class InMemoryFileReference
        {
            public ReadOnlySequence<byte> Data;
            public DateTimeOffset CreationTime;
            public DateTimeOffset LastWriteTime;

            public InMemoryFileReference(ReadOnlySequence<byte> data)
            {
                Data = data;
                CreationTime = DateTimeOffset.UtcNow;
                LastWriteTime = DateTimeOffset.UtcNow;
            }

            public InMemoryFileReference(ReadOnlySequence<byte> data, DateTimeOffset creationTime, DateTimeOffset lastWriteTime)
            {
                Data = data;
                CreationTime = creationTime;
                LastWriteTime = lastWriteTime;
            }
        }

        /// <summary>
        /// An IFileSystemWatcher that processes notifications from the master file system and turns them into events if they match the desired watch path + filters
        /// </summary>
        private class InMemoryFileSystemWatcher : IFileSystemWatcher
        {
            private readonly Regex _pathMatchRegex;
            private bool _disposed = false;

            public InMemoryFileSystemWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories)
            {
                if (string.IsNullOrWhiteSpace(filter))
                {
                    filter = "*";
                }

                filter = filter.Replace("/", "\\").TrimStart('\\');

                // Build a regex that matches files that this watcher is supposed to watch
                using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                {
                    StringBuilder regexBuilder = pooledSb.Builder;
                    regexBuilder.Append("^");

                    // Match the initial watch path
                    regexBuilder.Append(Regex.Escape(watchPath.IsRoot ? string.Empty : watchPath.FullName));
                    regexBuilder.Append("\\\\");

                    // Match an optional number of subdirectories
                    if (recurseSubdirectories)
                    {
                        regexBuilder.Append("(.+?\\\\)*");
                    }

                    // Interpret the filter string by escaping all characters to match literally, and then converting all "*" chars into regex catch-alls.
                    regexBuilder.Append(Regex.Escape(filter).Replace("\\*", "[^\\\\]+"));
                    regexBuilder.Append("$");
                    _pathMatchRegex = new Regex(regexBuilder.ToString());
                    ChangedEvent = new AsyncEvent<FileSystemChangedEventArgs>();
                }
            }

            public AsyncEvent<FileSystemChangedEventArgs> ChangedEvent { get; private set; }

            public void Notify(FileSystemChangedEventArgs args, IRealTimeProvider realTime)
            {
                if (_disposed)
                {
                    return;
                }

                // Does the path of the changed file match our regex?
                if (_pathMatchRegex.IsMatch(args.AffectedPath.FullName) ||
                    (args.RenamedPath != null && _pathMatchRegex.IsMatch(args.RenamedPath.FullName)))
                {
                    ChangedEvent.FireInBackground(this, args, NullLogger.Singleton, realTime);
                }
            }

            public bool IsDisposed => _disposed;

            public void Dispose()
            {
                _disposed = true;
            }
        }

        private class VirtualFileStream : NonRealTimeStream, IDisposable
        {
            private readonly VirtualPath _fullFilePath;
            private readonly InMemoryFileSystem _owningFileSystem;
            private readonly InMemoryFileTreeNode _targetNode;
            private readonly RecyclableMemoryStream _base;
            private readonly bool _canRead;
            private readonly IRealTimeProvider _realTime; // not the greatest design but we can't override the Flush() method so this is the best we can do
            private int _disposed = 0;

            public VirtualFileStream(InMemoryFileSystem owningFileSystem, VirtualPath targetFile, InMemoryFileTreeNode targetNode, IRealTimeProvider realTime, bool canRead)
            {
                _fullFilePath = targetFile;
                _targetNode = targetNode;
                _owningFileSystem = owningFileSystem;
                _realTime = realTime;
                _canRead = canRead;
                _base = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default);
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

            public override bool CanRead
            {
                get
                {
                    return _canRead;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return true;
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

            public override Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                FlushInternal(realTime);
                return DurandalTaskExtensions.NoOpTask;
            }

            public override void Flush()
            {
                FlushInternal(_realTime);
            }

            private void FlushInternal(IRealTimeProvider realTime)
            {
                _base.Flush();
                if (_targetNode.Files.ContainsKey(_fullFilePath.Name))
                {
                    _targetNode.Files.Remove(_fullFilePath.Name);
                }

                _targetNode.Files[_fullFilePath.Name] = new InMemoryFileReference(_base.ToReadOnlySequenceAvoidLOH());
                _owningFileSystem.NotifyWatchers(new FileSystemChangedEventArgs()
                {
                    ChangeType = FileSystemChangeType.FileChanged,
                    AffectedPath = _fullFilePath
                },
                realTime);
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
                        Flush();
                        _base.Dispose();
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
                return _base.Read(buffer, offset, count);
            }

            public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                return _base.Read(targetBuffer, offset, count);
            }

            public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken)
            {
                return Task.FromResult(_base.Read(targetBuffer, offset, count));
            }

            public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                return Task.FromResult(_base.Read(targetBuffer, offset, count));
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _base.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _base.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _base.Write(buffer, offset, count);
            }

            public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                _base.Write(sourceBuffer, offset, count);
            }

            public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                _base.Write(sourceBuffer, offset, count);
                return DurandalTaskExtensions.NoOpTask;
            }

#if NETCOREAPP
            public override void Write(ReadOnlySpan<byte> data)
#else
            public void Write(ReadOnlySpan<byte> data)
#endif
            {
                _base.Write(data);
            }
        }

#endregion
    }
}
