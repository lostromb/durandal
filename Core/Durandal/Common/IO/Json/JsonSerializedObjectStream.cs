using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.IO.Json
{
    /// <summary>
    /// Implements a stream for which you can give it an object and you can read JSON-serialized bytes
    /// directly from that object without calling JsonConvert.SerializeObject() or anything in advance.
    /// </summary>
    public class JsonSerializedObjectStream : Stream
    {
        private readonly RecyclableMemoryStream _innerStream;
        private int _disposed;

        public JsonSerializedObjectStream(object data, JsonSerializer serializer)
        {
            data.AssertNonNull(nameof(data));
            serializer.AssertNonNull(nameof(serializer));

            _innerStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default);
            // Do the serialization now to a pooled memory stream from which we will read from
            using (Utf8StreamWriter writer = new Utf8StreamWriter(_innerStream, leaveOpen: true))
            using (JsonTextWriter textWriter = new JsonTextWriter(writer))
            {
                serializer.Serialize(textWriter, data);
            }

            _innerStream.Seek(0, SeekOrigin.Begin);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~JsonSerializedObjectStream()
        {
            Dispose(false);
        }
#endif

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _innerStream.Length;

        public override long Position
        {
            get
            {
                return _innerStream.Position;
            }
            set
            {
                _innerStream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _innerStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }

        public override void Flush()
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                if (disposing)
                {
                    _innerStream?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
