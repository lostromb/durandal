using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Security
{
    public class SystemAESDelegates : IAESDelegates, IDisposable
    {
        private readonly Aes _aes;
        private int _disposed = 0;

        public SystemAESDelegates()
        {
            _aes = Aes.Create();
            _aes.Mode = CipherMode.CBC;
            _aes.Padding = PaddingMode.PKCS7;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~SystemAESDelegates()
        {
            Dispose(false);
        }
#endif

        public FinalizableStream CreateEncryptionStream(Stream innerStream, byte[] encryptionKey, byte[] IV)
        {
            ICryptoTransform transform = _aes.CreateEncryptor(encryptionKey, IV);
            return new CryptoStreamWrapper(new CryptoStream(innerStream, transform, CryptoStreamMode.Write));
        }

        public FinalizableStream CreateDecryptionStream(Stream innerStream, byte[] encryptionKey, byte[] IV)
        {
            ICryptoTransform transform = _aes.CreateDecryptor(encryptionKey, IV);
            return new CryptoStreamWrapper(new CryptoStream(innerStream, transform, CryptoStreamMode.Read));
        }

        public byte[] GenerateKey(string passphrase, int keySizeBytes)
        {
            // The returned key is the SHA512 hash of the passphrase
            using (SHA512 m = SHA512.Create())
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(passphrase);
                byte[] dat = m.ComputeHash(passwordBytes);
                byte[] generatedBlock = new byte[keySizeBytes];
                int outIdx = 0;
                while (outIdx < keySizeBytes)
                {
                    int copySize = Math.Min(keySizeBytes - outIdx, dat.Length);
                    ArrayExtensions.MemCopy(dat, 0, generatedBlock, outIdx, copySize);
                    outIdx += copySize;
                }

                return generatedBlock;
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
                _aes?.Dispose();
            }
        }

        private class CryptoStreamWrapper : FinalizableStream
        {
            private CryptoStream _innerStream;

            public CryptoStreamWrapper(CryptoStream nativeStream)
            {
                _innerStream = nativeStream;
            }

            public override bool CanRead => _innerStream.CanRead;
            public override bool CanSeek => _innerStream.CanSeek;
            public override bool CanWrite => _innerStream.CanWrite;
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

            public override void Finish(CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                _innerStream.FlushFinalBlock();
            }

            public override Task FinishAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                _innerStream.FlushFinalBlock();
                return DurandalTaskExtensions.NoOpTask;
            }

            public override void Flush()
            {
                _innerStream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _innerStream.Read(buffer, offset, count);
            }

            public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                return _innerStream.Read(targetBuffer, offset, count);
            }

            public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                return _innerStream.ReadAsync(targetBuffer, offset, count, cancelToken);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _innerStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _innerStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _innerStream.Write(buffer, offset, count);
            }

            public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                _innerStream.Write(sourceBuffer, offset, count);
            }

            public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                return _innerStream.WriteAsync(sourceBuffer, offset, count, cancelToken);
            }
        }
    }
}
