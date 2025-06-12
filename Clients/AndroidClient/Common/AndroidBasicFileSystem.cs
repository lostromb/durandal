using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Durandal.Common.Utils.Tasks;
using System.Threading;
using Durandal.Common.File;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.IO;

namespace Durandal.AndroidClient.Common
{
    /// <summary>
    /// Wraps the standard Java.IO accessors to interact with an android filesystem
    /// </summary>
    public class AndroidBasicFileSystem : AbstractFileSystem
    {
        private Java.IO.File _rootDirectory;

        public AndroidBasicFileSystem(Java.IO.File baseDirectory)
        {
            _rootDirectory = baseDirectory;
        }

        /// <inheritdoc />
        public override bool Delete(VirtualPath path)
        {
            Java.IO.File file = ResolveFilename(path);
            return file.Delete();
        }

        /// <inheritdoc />
        public override bool Exists(VirtualPath path)
        {
            Java.IO.File file = ResolveFilename(path);
            return file.Exists();
        }

        /// <inheritdoc />
        public override ResourceType WhatIs(VirtualPath path)
        {
            Java.IO.File file = ResolveFilename(path);
            if (file.Exists())
            {
                return file.IsDirectory ? ResourceType.Directory : ResourceType.File;
            }

            return ResourceType.Unknown;
        }

        /// <inheritdoc />
        public override IEnumerable<VirtualPath> ListDirectories(VirtualPath resourceContainerName)
        {
            List<VirtualPath> returnVal = new List<VirtualPath>();

            Java.IO.File directory = ResolveFilename(resourceContainerName);
            if (!directory.Exists() || !directory.IsDirectory)
            {
                return returnVal;
            }

            Java.IO.File[] files = directory.ListFiles();
            foreach (var file in files)
            {
                if (file.IsDirectory)
                {
                    returnVal.Add(resourceContainerName + ("\\" + file.Name));
                }
            }

            return returnVal;
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<VirtualPath>> ListDirectoriesAsync(VirtualPath resourceContainerName)
        {
            List<VirtualPath> returnVal = new List<VirtualPath>();

            Java.IO.File directory = ResolveFilename(resourceContainerName);
            if (!directory.Exists() || !directory.IsDirectory)
            {
                return returnVal;
            }

            Java.IO.File[] files = await directory.ListFilesAsync();
            foreach (var file in files)
            {
                if (file.IsDirectory)
                {
                    returnVal.Add(resourceContainerName + ("\\" + file.Name));
                }
            }

            return returnVal;
        }

        /// <inheritdoc />
        public override IEnumerable<VirtualPath> ListFiles(VirtualPath resourceContainerName)
        {
            List<VirtualPath> returnVal = new List<VirtualPath>();

            Java.IO.File directory = ResolveFilename(resourceContainerName);
            if (!directory.Exists() || !directory.IsDirectory)
            {
                return returnVal;
            }

            Java.IO.File[] files = directory.ListFiles();
            foreach (var file in files)
            {
                if (!file.IsDirectory)
                {
                    returnVal.Add(resourceContainerName + ("\\" + file.Name));
                }
            }

            return returnVal;
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<VirtualPath>> ListFilesAsync(VirtualPath resourceContainerName)
        {
            List<VirtualPath> returnVal = new List<VirtualPath>();

            Java.IO.File directory = ResolveFilename(resourceContainerName);
            if (!directory.Exists() || !directory.IsDirectory)
            {
                return returnVal;
            }

            Java.IO.File[] files = await directory.ListFilesAsync();
            foreach (var file in files)
            {
                if (!file.IsDirectory)
                {
                    returnVal.Add(resourceContainerName + ("\\" + file.Name));
                }
            }

            return returnVal;
        }

        /// <inheritdoc />
        public override NonRealTimeStream OpenStream(VirtualPath path, FileOpenMode openMode, FileAccessMode accessMode, int? bufferSizeHint = null)
        {
            Java.IO.File file = ResolveFilename(path);
            if (accessMode == FileAccessMode.Read)
            {
                return new NonRealTimeStreamWrapper(new FileInputStreamWrapper(file), true);
            }
            else if (accessMode == FileAccessMode.Write)
            {
                return new NonRealTimeStreamWrapper(new FileOutputStreamWrapper(file), true);
            }
            else
            {
                // Java.IO.File file = ResolveFilename(path);
                // return new Java.IO.RandomAccessFile(file, "rw");
                throw new NotImplementedException();
            }
        }

        /// <inheritdoc />
        public override void WriteLines(VirtualPath path, IEnumerable<string> data)
        {
            Java.IO.File file = ResolveFilename(path);
            using (Java.IO.PrintWriter writer = new Java.IO.PrintWriter(file))
            {
                foreach (string line in data)
                {
                    if (line != null)
                    {
                        writer.Println(line);
                    }
                }

                writer.Close();
            }
        }

        /// <inheritdoc />
        public override IReadOnlyCollection<string> ReadLines(VirtualPath path)
        {
            Java.IO.File file = ResolveFilename(path);
            StringBuilder fileContents = new StringBuilder();

            using (Java.IO.FileReader reader = new Java.IO.FileReader(file))
            {
                char[] buf = new char[1024];
                bool done = false;
                while (!done)
                {
                    int charsRead = reader.Read(buf, 0, buf.Length);
                    if (charsRead <= 0)
                    {
                        done = true;
                    }
                    else
                    {
                        fileContents.Append(buf, 0, charsRead);
                    }
                }
            }

            string heckaBigString = fileContents.ToString();
            return heckaBigString.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public override Task<IFileSystemWatcher> CreateDirectoryWatcher(VirtualPath watchPath, string filter, bool recurseSubdirectories)
        {
            // Android.OS.FileObserver
            throw new NotImplementedException();
        }

        private Java.IO.File ResolveFilename(VirtualPath virtualPath)
        {
            return new Java.IO.File(_rootDirectory.AbsolutePath + (virtualPath.FullName.Replace(VirtualPath.PATH_SEPARATOR_CHAR, Java.IO.File.SeparatorChar)));
        }

        #region Stream wrappers

        private class FileInputStreamWrapper : Stream
        {
            private Java.IO.FileInputStream _innerStream;
            private long _fileLength = 0;
            private long _position = 0;
            private int _disposed = 0;

            public FileInputStreamWrapper(Java.IO.File file)
            {
                _fileLength = file.Length();
                _innerStream = new Java.IO.FileInputStream(file);
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~FileInputStreamWrapper()
            {
                Dispose(false);
            }
#endif

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => _fileLength;

            public override long Position
            {
                get
                {
                    return _position;
                }

                set
                {
                    throw new NotImplementedException();
                }
            }

            public override void Flush() { }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_position == _fileLength)
                {
                    return 0;
                }

                int bytesRead = _innerStream.Read(buffer, offset, count);
                if (bytesRead > 0)
                {
                    _position += bytesRead;
                }

                return bytesRead;
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
                throw new NotImplementedException();
            }

            public new void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected new void Dispose(bool disposing)
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                {
                    return;
                }

                DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                if (disposing)
                {
                    _innerStream.Dispose();
                }
            }
        }

        private class FileOutputStreamWrapper : Stream
        {
            private Java.IO.FileOutputStream _innerStream;
            private long _position = 0;
            private int _disposed = 0;

            public FileOutputStreamWrapper(Java.IO.File file)
            {
                _innerStream = new Java.IO.FileOutputStream(file);
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~FileOutputStreamWrapper()
            {
                Dispose(false);
            }
#endif

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => _position;

            public override long Position
            {
                get
                {
                    return _position;
                }

                set
                {
                    throw new NotImplementedException();
                }
            }

            public override void Flush()
            {
                _innerStream.Flush();
            }

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
                _innerStream.Write(buffer, offset, count);
                _position += count;
            }

            public new void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected new void Dispose(bool disposing)
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                {
                    return;
                }

                DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                if (disposing)
                {
                    _innerStream.Dispose();
                }
            }
        }

#endregion
    }
}