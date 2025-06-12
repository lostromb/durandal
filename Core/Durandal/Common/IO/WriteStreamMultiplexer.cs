using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.IO
{
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Utils;
    using File;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Threading;

    /// <summary>
    /// The purpose of this class is to manage MIMO file IO where multiple threads are outputting
    /// data to multiple files, and occasionally multiple threads want to write to the same file.
    /// This class manages a dictionary of StreamWriter objects that are indexed by resource name.
    /// Make sure that you call Close() when you're done writing all your streams!
    /// </summary>
    public class WriteStreamMultiplexer : IDisposable
    {
        private IDictionary<string, WriteStream> _writers;
        private IFileSystem _fileSystem;
        private int _disposed = 0;

        public WriteStreamMultiplexer(IFileSystem fileSystem)
        {
            _writers = new Dictionary<string, WriteStream>();
            _fileSystem = fileSystem;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~WriteStreamMultiplexer()
        {
            Dispose(false);
        }
#endif

        public WriteStream GetStream(VirtualPath outFileName, bool overwriteExistingFiles = false)
        {
            string key = outFileName.FullName;
            lock(_writers)
            {
                if (!_writers.ContainsKey(key))
                {
                    _writers[key] = new WriteStream(
                        _fileSystem.OpenStream(
                            outFileName,
                            overwriteExistingFiles ? FileOpenMode.Create : FileOpenMode.CreateNew,
                            FileAccessMode.Write));
                }
                if (_writers.ContainsKey(key))
                {
                    return _writers[key];
                }
            }
            return null;
        }

        public bool StreamExists(VirtualPath outFileName)
        {
            lock (_writers)
            {
                return _writers.ContainsKey(outFileName.FullName);
            }
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

            if (disposing)
            {
                lock (_writers)
                {
                    foreach (WriteStream writer in _writers.Values)
                    {
                        writer.Dispose();
                    }
                }
            }
        }
        
        /// <summary>
        /// An implementation of TextWriter which writes output line-by-line.
        /// Functions other than WriteLine are not supported.
        /// </summary>
        public class WriteStream : TextWriter
        {
            private StreamWriter _baseStream;
            private int _disposed = 0;

            public override Encoding Encoding
            {
                get
                {
                    return StringUtils.UTF8_WITHOUT_BOM;
                }
            }

            internal WriteStream(Stream baseStream)
            {
                _baseStream = new StreamWriter(baseStream);
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

            internal WriteStream(StreamWriter baseStream)
            {
                _baseStream = baseStream;
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~WriteStream()
            {
                Dispose(false);
            }
#endif

            public override void WriteLine(string value)
            {
                lock (this)
                {
                    _baseStream.WriteLine(value);
                }
            }

            public override void WriteLine(object value)
            {
                lock (this)
                {
                    _baseStream.WriteLine(value.ToString());
                }
            }

            public override void Write(char value)
            {
                throw new InvalidOperationException("Can only write entire lines using a multiplexed text writer");
            }

            protected override void Dispose(bool disposing)
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                if (disposing)
                {
                    if (_baseStream != null)
                    {
                        _baseStream.Dispose();
                    }
                }
            }
        }
    }
}
