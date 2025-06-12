using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using System.Threading;
using Durandal.Common.Time;
using Durandal.API;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;
using System.Diagnostics;
using Durandal.Common.Logger;
using Durandal.Common.IO;

namespace Durandal.Common.Remoting.Proxies
{
    public class RemotedFileSystem : AbstractFileSystem
    {
        private readonly WeakPointer<RemoteDialogMethodDispatcher> _dispatcher;
        private readonly IRealTimeProvider _realTime;
        private readonly ILogger _logger;

        public RemotedFileSystem(WeakPointer<RemoteDialogMethodDispatcher> dispatcher, ILogger logger, IRealTimeProvider realTime)
        {
            _dispatcher = dispatcher.AssertNonNull(nameof(dispatcher));
            _logger = logger.AssertNonNull(nameof(logger));
            _realTime = realTime.AssertNonNull(nameof(realTime));
        }
        
        public override Task CreateDirectoryAsync(VirtualPath directory)
        {
            return _dispatcher.Value.File_CreateDirectory(directory, _realTime, CancellationToken.None);
        }
        
        public override Task<bool> DeleteAsync(VirtualPath path)
        {
            return _dispatcher.Value.File_Delete(path, _realTime, CancellationToken.None);
        }
        
        public override async Task<bool> ExistsAsync(VirtualPath path)
        {
            RemoteFileStat stat = await _dispatcher.Value.File_Stat(path, _realTime, CancellationToken.None).ConfigureAwait(false);
            return stat.Exists;
        }
        
        public override async Task<IEnumerable<VirtualPath>> ListDirectoriesAsync(VirtualPath directoryName)
        {
            return await _dispatcher.Value.File_List(directoryName, true, _realTime, CancellationToken.None).ConfigureAwait(false);
        }
        
        public override async Task<IEnumerable<VirtualPath>> ListFilesAsync(VirtualPath directoryName)
        {
            return await _dispatcher.Value.File_List(directoryName, false, _realTime, CancellationToken.None).ConfigureAwait(false);
        }
        
        public override Task MoveAsync(VirtualPath source, VirtualPath target)
        {
            return _dispatcher.Value.File_Move(source, target, _realTime, CancellationToken.None);
        }

        public override Task<NonRealTimeStream> OpenStreamAsync(VirtualPath file, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            return OpenStreamAsync(new FileStreamParams(file, openMode, accessMode, bufferSizeHint));
        }

        public override async Task<NonRealTimeStream> OpenStreamAsync(FileStreamParams fileParams)
        {
            RemoteFileStreamOpenResult openResult = await _dispatcher.Value.FileStream_Open(
                fileParams.Path,
                fileParams.OpenMode,
                fileParams.AccessMode,
                fileParams.ShareMode,
                _realTime,
                CancellationToken.None);
            return new RemotedFileStream(fileParams.Path, openResult, _dispatcher, _logger);

            // Old code that just read the entire contents
            //ArraySegment<byte> remoteFile = default(ArraySegment<byte>);
            //if (fileParams.OpenMode == FileOpenMode.Open || fileParams.OpenMode == FileOpenMode.OpenOrCreate)
            //{
            //    // Fetch the remote file if it exists
            //    remoteFile = await _dispatcher.File_ReadContents(fileParams.Path, _realTime).ConfigureAwait(false);
            //}
            
            //if (fileParams.AccessMode.HasFlag(FileAccessMode.Write))
            //{
            //    // If the file is writable, return an expandable stream that has a write callback attached to its dispose method
            //    MemoryStream localCopyOfFile = new MemoryStream();
            //    if (remoteFile != default(ArraySegment<byte>) && remoteFile.Count > 0)
            //    {
            //        localCopyOfFile.Write(remoteFile.Array, remoteFile.Offset, remoteFile.Count);
            //        localCopyOfFile.Seek(0, SeekOrigin.Begin);
            //    }

            //    return new SimpleRemotedFileStream(localCopyOfFile, async () =>
            //    {
            //        ArraySegment<byte> updatedFile = new ArraySegment<byte>(localCopyOfFile.ToArray());
            //        await _dispatcher.File_WriteContents(fileParams.Path, updatedFile, _realTime).ConfigureAwait(false);
            //    });
            //}
            //else
            //{
            //    // Otherwise just return the readonly stream
            //    return new NonRealTimeStreamWrapper(new MemoryStream(remoteFile.Array, remoteFile.Offset, remoteFile.Count, false), true);
            //}
        }

        public override async Task<FileStat> StatAsync(VirtualPath fileName)
        {
            RemoteFileStat stat = await _dispatcher.Value.File_Stat(fileName, _realTime, CancellationToken.None).ConfigureAwait(false);
            if (stat.Exists && !stat.IsDirectory)
            {
                return new FileStat()
                {
                    CreationTime = stat.CreationTime.GetValueOrDefault(),
                    LastAccessTime = stat.LastAccessTime.GetValueOrDefault(),
                    LastWriteTime = stat.LastWriteTime.GetValueOrDefault(),
                    Size = stat.Size
                };
            }
            else
            {
                return null;
            }
        }

        public override async Task WriteStatAsync(VirtualPath fileName, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime)
        {
            await _dispatcher.Value.File_WriteStat(fileName, _realTime, newCreationTime, newModificationTime, CancellationToken.None).ConfigureAwait(false);
        }

        public override async Task<ResourceType> WhatIsAsync(VirtualPath resourceName)
        {
            RemoteFileStat stat = await _dispatcher.Value.File_Stat(resourceName, _realTime, CancellationToken.None).ConfigureAwait(false);
            if (stat.Exists)
            {
                return stat.IsDirectory ? ResourceType.Directory : ResourceType.File;
            }
            else
            {
                return ResourceType.Unknown;
            }
        }

        public override Task<IFileSystemWatcher> CreateDirectoryWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories)
        {
            return Task.FromResult(NullFileSystemWatcher.Singleton);
        }

//      /// <summary>
//      /// Remoted file stream, which works by copying the original file into a memory stream,
//      /// applying all manipulations to that memory stream, and then updating the original file upon disposal
//      /// </summary>
//        private class SimpleRemotedFileStream : NonRealTimeStream, IDisposable
//        {
//            private readonly MemoryStream _base;
//            private readonly Func<Task> _flushCallback;
//            private int _disposed = 0;

//            public SimpleRemotedFileStream(MemoryStream baseStream, Func<Task> flushCallback)
//            {
//                _base = baseStream;
//                _flushCallback = flushCallback;
//                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
//            }

//#if TRACK_IDISPOSABLE_LEAKS
//            ~SimpleRemotedFileStream()
//            {
//                Dispose(false);
//            }
//#endif

//            public override bool CanRead => _base.CanRead;
//            public override bool CanSeek => _base.CanSeek;
//            public override bool CanWrite => _base.CanWrite;
//            public override long Length => _base.Length;

//            public override long Position
//            {
//                get
//                {
//                    return _base.Position;
//                }

//                set
//                {
//                    _base.Position = value;
//                }
//            }

//            public override void Flush()
//            {
//                _flushCallback().Await();
//            }

//            public override Task FlushAsync(CancellationToken cancellationToken)
//            {
//                return _flushCallback();
//            }

//            public override int Read(byte[] buffer, int offset, int count)
//            {
//                return _base.Read(buffer, offset, count);
//            }

//            public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
//            {
//                throw new NotImplementedException();
//            }

//            public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
//            {
//                throw new NotImplementedException();
//            }

//            public override long Seek(long offset, SeekOrigin origin)
//            {
//                return _base.Seek(offset, origin);
//            }

//            public override void SetLength(long value)
//            {
//                _base.SetLength(value);
//            }

//            public override void Write(byte[] buffer, int offset, int count)
//            {
//                _base.Write(buffer, offset, count);
//            }

//            public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
//            {
//                throw new NotImplementedException();
//            }

//            public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
//            {
//                throw new NotImplementedException();
//            }

//            protected override void Dispose(bool disposing)
//            {
//                if (!AtomicOperations.ExecuteOnce(ref _disposed))
//                {
//                    return;
//                }

//                try
//                {
//                    DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

//                    if (disposing)
//                    {
//                        Flush();
//                    }
//                }
//                catch (Exception e)
//                {
//                    DebugLogger.Default.Log(e);
//                }
//                finally
//                {
//                    base.Dispose(disposing);
//                }
//            }
//        }
    }
}
