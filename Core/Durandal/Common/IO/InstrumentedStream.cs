using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.IO
{
    public class InstrumentedStream : Stream
    {
        private readonly Stream _stream;
        private readonly ILogger _logger;
        private readonly LogLevel _logLevel;
        private readonly Stopwatch _lifetimeTimer;
        private int _disposed = 0;

        public InstrumentedStream(Stream baseStream, ILogger logger, LogLevel logLevel)
        {
            _stream = baseStream;
            _logger = logger;
            _logLevel = logLevel;
            _lifetimeTimer = Stopwatch.StartNew();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~InstrumentedStream()
        {
            Dispose(false);
        }
#endif

        public override bool CanRead
        {
            get
            {
                return _stream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return _stream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return _stream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return _stream.Length;
            }
        }

        public override long Position
        {
            get
            {
                return _stream.Position;
            }

            set
            {
                _stream.Position = value;
            }
        }

        public override void Flush()
        {
            _logger.LogFormat(_logLevel, DataPrivacyClassification.SystemMetadata,
                "{0} FLUSH",
                _lifetimeTimer.ElapsedMilliseconds);
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int returnVal = _stream.Read(buffer, offset, count);
            _logger.LogFormat(_logLevel, DataPrivacyClassification.SystemMetadata,
                "{0} READ {1}+{2}: {3}",
                _lifetimeTimer.ElapsedMilliseconds,
                count,
                offset,
                returnVal);
            return returnVal;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _logger.LogFormat(_logLevel, DataPrivacyClassification.SystemMetadata,
                "{0} SEEK {1} from {2}",
                _lifetimeTimer.ElapsedMilliseconds,
                offset,
                origin);
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
            _logger.LogFormat(_logLevel, DataPrivacyClassification.SystemMetadata,
                "{0} WRITE {1}+{2}",
                _lifetimeTimer.ElapsedMilliseconds,
                count,
                offset);
        }

        public override int ReadByte()
        {
            int returnVal = _stream.ReadByte();
            _logger.LogFormat(_logLevel, DataPrivacyClassification.SystemMetadata,
                "{0} READ BYTE {1}",
                _lifetimeTimer.ElapsedMilliseconds,
                returnVal);
            return returnVal;
        }

        public override void WriteByte(byte value)
        {
            _logger.LogFormat(_logLevel, DataPrivacyClassification.SystemMetadata,
                "{0} WRITE BYTE {1}",
                _lifetimeTimer.ElapsedMilliseconds,
                value);
            _stream.WriteByte(value);
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
                    _stream?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
