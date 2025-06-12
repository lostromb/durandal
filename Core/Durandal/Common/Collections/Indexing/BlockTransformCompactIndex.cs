using Durandal.Common.File;

namespace Durandal.Common.Collections.Indexing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Durandal.Common.Utils;
    using Durandal.Common.Cache;
    using Durandal.Common.IO;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// Rather than using a naive dictionary, this class stores data contiguously inside large memory pages
    /// which can be optionally cached and transformed
    /// </summary>
    public class BlockTransformCompactIndex<T> : ICompactIndex<T> where T : class
    {
        public readonly Compact<T> NULL_INDEX = new Compact<T>(0xFFFFFFFF);

        private readonly ReaderWriterLockSlim _lock;
        private readonly IDictionary<int, Compact<T>> _forwardLookup;
        private readonly IByteConverter<T> _byteConverter;
        private readonly IMemoryPageStorage _pageStorage;
        
        private byte[] _buffer;
        private uint _currentBlock = 0;
        private uint _currentByte = 0;

        private int BYTE_BITS; // number of bits in the address to devote to byte offset. Block size is 2 ^ this value
        private int BLOCK_BITS; // number of bits in the address to devote to block #
        private uint BLOCK_SIZE; // size of each block, in bytes
        private uint BLOCK_MASK; // mask for block # portion of address
        private uint SHIFT_BLOCK_MASK; // block mask shifted to the right
        private uint BYTE_MASK; // mask for byte # portion of address
        
        private IReadThroughCache<Compact<T>, byte[]> _itemCache;
        private int _disposed = 0;
        
        public BlockTransformCompactIndex(IByteConverter<T> contentEncoder, IMemoryPageStorage pageStorage, int blockSize = 32768, int itemCacheSize = 0)
        {
            int pageSizeBits = (int)(Math.Log(blockSize) / Math.Log(2));

            BYTE_BITS = pageSizeBits;
            BLOCK_BITS = 32 - BYTE_BITS;
            BLOCK_SIZE = 1U << BYTE_BITS;
            BLOCK_MASK = 0xFFFFFFFFU << BYTE_BITS;
            SHIFT_BLOCK_MASK = 0xFFFFFFFFU >> BYTE_BITS;
            BYTE_MASK = 0xFFFFFFFFU ^ BLOCK_MASK;

            _forwardLookup = new Dictionary<int, Compact<T>>();
            _pageStorage = pageStorage;
            _byteConverter = contentEncoder;
            _buffer = new byte[BLOCK_SIZE];
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

            if (itemCacheSize > 0)
            {
                _itemCache = new MFUCache<Compact<T>, byte[]>(RetrieveNoCache, itemCacheSize);
            }
            else
            {
                _itemCache = new NullCache<Compact<T>, byte[]>(RetrieveNoCache);
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~BlockTransformCompactIndex()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Returns the number of bytes stored in this index
        /// </summary>
        public long MemoryUse
        {
            get
            {
                _lock.EnterReadLock();
                // 3.2 is just a correction constant for "dark memory" that the runtime uses
                long returnVal = (long)((double)((_forwardLookup.Count * 8) + _pageStorage.MemoryUse + _currentByte) * 3.2);
                // TODO include the cache size in this calculation
                _lock.ExitReadLock();
                return returnVal;
            }
        }

        /// <summary>
        /// Returns the current compression ratio for pooled data
        /// </summary>
        public double CompressionRatio
        {
            get
            {
                return _pageStorage.CompressionRatio;
            }
        }

        /// <summary>
        /// Stores a new value in the index and return a key that can retrieve it later.
        /// </summary>
        /// <param name="value">The value to be stored</param>
        /// <returns>A key that can retrieve this object later.</returns>
        public Compact<T> Store(T value)
        {
            // Is it null?
            if (value == null)
                return NULL_INDEX;

            int hash = value.GetHashCode();

            _lock.EnterWriteLock();
            try
            {
                // Does it already exist?
                if (_forwardLookup.ContainsKey(hash))
                {
                    return _forwardLookup[hash];
                }

                byte[] encodedValue = _byteConverter.Encode(value);

                Compact<T> itemIndex = StoreInternal(encodedValue);
                _forwardLookup[hash] = itemIndex;

                return itemIndex;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Returns the index of the specified object in the store.
        /// If the object does not exist, this returns NULL_INDEX
        /// </summary>
        /// <param name="value">The item to check</param>
        /// <returns>The key that will return this item if passed into Retrieve(key)</returns>
        public Compact<T> GetIndex(T value)
        {
            _lock.EnterReadLock();
            Compact<T> returnVal;
            if (value != null && _forwardLookup.TryGetValue(value.GetHashCode(), out returnVal))
            {
                _lock.ExitReadLock();
                return returnVal;
            }
            _lock.ExitReadLock();
            return NULL_INDEX;
        }

        /// <summary>
        /// Tests to see if a given key exists in the index
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Contains(Compact<T> key)
        {
            _lock.EnterReadLock();
            if (key.Addr == NULL_INDEX.Addr) // Null is a special value that is always in the set
                return true;
            AddressInternal decodedAddr = DecodeAddress(key);
            bool returnVal = decodedAddr.BlockOffset < _currentBlock ||
                             (decodedAddr.BlockOffset == _currentBlock && decodedAddr.ByteOffset < _currentByte);
            _lock.ExitReadLock();
            return returnVal;
        }

        /// <summary>
        /// Tests to see if a given object exists in the index
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Contains(T value)
        {
            _lock.EnterReadLock();
            bool returnVal = _forwardLookup.ContainsKey(value.GetHashCode());
            _lock.ExitReadLock();
            return returnVal;
        }

        /// <summary>
        /// Returns the number of items stored in the index
        /// </summary>
        public int GetCount()
        {
            _lock.EnterReadLock();
            int returnVal = _forwardLookup.Count;
            _lock.ExitReadLock();
            return returnVal;
        }

        /// <summary>
        /// Retrieve a value from the store based on a key.
        /// </summary>
        /// <param name="key">The key to use</param>
        /// <returns>The value that was originally stored under that key</returns>
        public T Retrieve(Compact<T> key)
        {
            if (key.Addr == NULL_INDEX.Addr)
                return null;
            if (Contains(key))
            {
                byte[] returnedData = _itemCache.GetCache(key);
                return _byteConverter.Decode(returnedData, 0, returnedData.Length);
            }

            throw new ArgumentException("Attempted to retrieve nonexistent key " + key + " from compact index");
        }
        
        private byte[] RetrieveNoCache(Compact<T> key)
        {
            return RetrieveInternal(key);
        }

        /// <summary>
        /// Removes all items from this store.
        /// </summary>
        public void Clear()
        {
            _lock.EnterWriteLock();
            _currentBlock = 0;
            _currentByte = 0;
            _forwardLookup.Clear();
            _pageStorage.Clear();
            _buffer = new byte[BLOCK_SIZE];
            _lock.ExitWriteLock();
        }

        public Compact<T> GetNullIndex()
        {
            return NULL_INDEX;
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
                _lock?.Dispose();
            }
        }

#region Ugly low level code

        internal struct AddressInternal
        {
            public AddressInternal(uint bl, uint by)
            {
                BlockOffset = bl;
                ByteOffset = by;
            }

            public uint BlockOffset;
            public uint ByteOffset;
        }

        internal AddressInternal DecodeAddress(Compact<T> addr)
        {
            uint blockOffset = (addr.Addr >> BYTE_BITS) & SHIFT_BLOCK_MASK;
            uint byteOffset = addr.Addr & BYTE_MASK;
            return new AddressInternal(blockOffset, byteOffset);
        }

        internal Compact<T> EncodeAddress(AddressInternal addr)
        {
            uint blockOffset = (addr.BlockOffset << BYTE_BITS) & BLOCK_MASK;
            uint byteOffset = addr.ByteOffset & BYTE_MASK;
            return new Compact<T>(blockOffset | byteOffset);
        }

        internal AddressInternal IncrementAddress(AddressInternal addr, uint increment)
        {
            uint totalOffset = addr.ByteOffset + increment;
            AddressInternal returnVal = new AddressInternal(addr.BlockOffset + (totalOffset / BLOCK_SIZE), totalOffset % BLOCK_SIZE);
            return returnVal;
        }

        private Compact<T> StoreInternal(byte[] data)
        {
            // Compute the address
            AddressInternal recordAddress = new AddressInternal(_currentBlock, _currentByte);

            // Write the record size to the first few bytes, using OGG-style lacing
            int lengthBytes = (data.Length / 255) + 1;
            byte[] recordSizeField = new byte[lengthBytes];
            for (int c = 0; c < lengthBytes - 1; c++)
                recordSizeField[c] = (byte)255;
            recordSizeField[lengthBytes - 1] = (byte)(data.Length % 255);
            WriteData(recordSizeField);

            // And then write the raw data
            WriteData(data);

            return EncodeAddress(recordAddress);
        }

        private void WriteData(byte[] data)
        {
            uint bytesRemaining = (uint)data.Length;
            uint index = 0;
            while (bytesRemaining >= (BLOCK_SIZE - _currentByte))
            {
                // Write out one block at a time
                uint thisBlockBytes = BLOCK_SIZE - _currentByte;
                ArrayExtensions.MemCopy(data, (int)index, _buffer, (int)_currentByte, (int)thisBlockBytes);

                // Advance to the next block
                _currentBlock = _pageStorage.Store(_buffer) + 1;
                _buffer = new byte[BLOCK_SIZE];
                _currentByte = 0;
                bytesRemaining -= thisBlockBytes;
                index += thisBlockBytes;
            }

            // And write the remainder, if any
            if (bytesRemaining > 0)
            {
                ArrayExtensions.MemCopy(data, (int)index, _buffer, (int)_currentByte, (int)bytesRemaining);
                _currentByte += bytesRemaining;
            }
        }

        private byte[] RetrieveInternal(Compact<T> key)
        {
            _lock.EnterUpgradeableReadLock();
            AddressInternal dummy;
            byte[] returnVal = ReadRecord(DecodeAddress(key), out dummy);
            _lock.ExitUpgradeableReadLock();
            return returnVal;
        }

        internal byte[] ReadRecord(AddressInternal addr, out AddressInternal nextAddr)
        {
            // Read the size field
            uint size = 0;
            byte lastSize = 255;
            uint sizeHeaderOffset = 0;
            while (lastSize == 255)
            {
                lastSize = ReadByteFromBlocks(addr, sizeHeaderOffset++);
                size += lastSize;
            }

            // Calculate the address where the data begins
            AddressInternal dataStart = IncrementAddress(addr, sizeHeaderOffset);

            nextAddr = IncrementAddress(dataStart, size);

            // Now read the rest of the entire field given the size
            return ReadFromBlocks(dataStart, size);
        }

        private byte ReadByteFromBlocks(AddressInternal startAddr, uint offset)
        {
            uint blockOffset = (startAddr.ByteOffset + offset) / BLOCK_SIZE;
            uint byteOffset = (startAddr.ByteOffset + offset) % BLOCK_SIZE;
            byte[] curBlock = GetBlock(startAddr.BlockOffset + blockOffset);
            return curBlock[byteOffset];
        }

        private byte[] ReadFromBlocks(AddressInternal startAddr, uint length)
        {
            uint blockOffset = startAddr.BlockOffset;

            byte[] returnVal = new byte[length];
            uint bytesRead = 0;
            uint blockIdx = startAddr.ByteOffset;
            while (bytesRead < length)
            {
                byte[] curBlock = GetBlock(blockOffset++);
                uint bytesToRead = Math.Min(length - bytesRead, BLOCK_SIZE - blockIdx);
                ArrayExtensions.MemCopy(curBlock, (int)blockIdx, returnVal, (int)bytesRead, (int)bytesToRead);
                blockIdx = 0;
                bytesRead += bytesToRead;
            }

            return returnVal;
        }

        /// <summary>
        /// Actually retrieve a block from persistent memory
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private byte[] GetBlock(uint index)
        {
            if (index == _currentBlock)
            {
                return _buffer;
            }
            else
            {
                return _pageStorage.Retrieve(index);
            }
        }

#endregion

#region Enumerators

        public IEnumerator<T> GetEnumerator()
        {
            return new EnumeratorImpl(this, _byteConverter);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new EnumeratorImpl(this, _byteConverter);
        }

        private class EnumeratorImpl : IEnumerator<T>
        {
            private AddressInternal _currentAddr;
            private AddressInternal _nextAddr;
            private BlockTransformCompactIndex<T> _index;
            private IByteConverter<T> _decoder;
            private T _current = null;
            private int _maxn;
            private int _n;

            public EnumeratorImpl(BlockTransformCompactIndex<T> index, IByteConverter<T> decoder)
            {
                _currentAddr = new AddressInternal(0, 0);
                _nextAddr = new AddressInternal(0, 0);
                _decoder = decoder;
                _index = index;
                _n = 0;
                _maxn = index.GetCount();
            }

            public T Current
            {
                get { return _current; }
            }

            public void Dispose() { }

            object System.Collections.IEnumerator.Current
            {
                get { return _current; }
            }

            public bool MoveNext()
            {
                bool hasNext = (_n <= _maxn);
                if (hasNext)
                {
                    _currentAddr = _nextAddr;
                    byte[] raw = _index.ReadRecord(_currentAddr, out _nextAddr);
                    _current = _decoder.Decode(raw, 0, raw.Length);
                    _n++;
                }
                return hasNext;
            }

            public void Reset()
            {
                _currentAddr = new AddressInternal(0, 0);
                _nextAddr = new AddressInternal(0, 0);
                _n = 0;
            }
        }

        public static BlockTransformCompactIndex<string> BuildStringIndex(int blockSize = 1024)
        {
            return new BlockTransformCompactIndex<string>(new StringByteConverter(), new BasicPageStorage(), blockSize);
        }

#endregion
    }
}
