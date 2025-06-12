using Durandal.Common.File;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Collections.Indexing
{
    public class PagingFilePageStorage : IMemoryPageStorage, IDisposable
    {
        private readonly IFileSystem _fileManager;
        private readonly VirtualPath _pagingFile;
        private readonly Stream _fileStream;
        private uint _numPages = 0;
        private uint _blockSize;
        private int _disposed = 0;

        public PagingFilePageStorage(IFileSystem fileManager, VirtualPath pagingFile, uint blockSize)
        {
            _fileManager = fileManager;
            _pagingFile = pagingFile;
            _blockSize = blockSize;
            _fileStream = _fileManager.OpenStream(_pagingFile, FileOpenMode.Create, FileAccessMode.ReadWrite);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PagingFilePageStorage()
        {
            Dispose(false);
        }
#endif

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
                _fileStream.Dispose();
                _fileManager.Delete(_pagingFile);
            }
        }

        public double CompressionRatio
        {
            get
            {
                return 0;
            }
        }

        public long IndexSize
        {
            get
            {
                return _numPages * _blockSize;
            }
        }

        public long MemoryUse
        {
            get
            {
                return 0;
            }
        }

        public void Clear()
        {
            _fileStream.SetLength(0);
            _numPages = 0;
        }

        public byte[] Retrieve(uint blockNum)
        {
            byte[] returnVal = new byte[_blockSize];
            _fileStream.Seek(blockNum * _blockSize, SeekOrigin.Begin);
            _fileStream.Read(returnVal, 0, (int)_blockSize);
            return returnVal;
        }

        public uint Store(byte[] block)
        {
            _fileStream.Seek(0, SeekOrigin.End);
            _fileStream.Write(block, 0, (int)_blockSize);
            return _numPages++;
        }
    }
}
