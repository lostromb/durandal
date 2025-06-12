namespace Durandal.Common.IO.Hashing
{
    using Durandal.Common.Collections;
    using Durandal.Common.IO;
    using System;
    using System.IO;
    using System.Security;

    /// <summary>
    /// Portable version of Microsoft's SHA1Managed
    /// </summary>
    public class SHA1
    {
        private byte[] _buffer;
        private long _count; // Number of bytes in the hashed message
        private uint[] _stateSHA1;
        private uint[] _expandedBuffer;
        private byte[] _hashValue;
        private int _state = 0;

        public SHA1()
        {
            _stateSHA1 = new uint[5];
            _buffer = new byte[64];
            _expandedBuffer = new uint[80];

            InitializeState();
        }

        #region Public methods

        /// <summary>
        /// Gets the current hash value
        /// </summary>
        public byte[] Hash
        {
            get
            {
                if (_state != 0)
                    throw new InvalidOperationException("Hash is not yet finalized");
                return (byte[])_hashValue.Clone();
            }
        }

        /// <summary>
        /// Gets the number of bits in the hash digest
        /// </summary>
        public int HashSize
        {
            get { return 160; }
        }

        /// <summary>
        /// Resets all internal state of this hasher
        /// </summary>
        public void Reset()
        {
            Initialize();
        }

        public byte[] ComputeHash(Stream inputStream)
        {
            using (PooledBuffer<byte> pooledBuffer = BufferPool<byte>.Rent())
            {
                byte[] buffer = pooledBuffer.Buffer;
                int bytesRead;
                do
                {
                    bytesRead = inputStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        HashDataInternal(buffer.AsSpan(0, bytesRead));
                    }
                } while (bytesRead > 0);
            }

            _hashValue = EndHashInternal();
            byte[] Tmp = (byte[])_hashValue.Clone();
            Initialize();
            return (Tmp);
        }

        public byte[] ComputeHash(byte[] buffer)
        {
            // Do some validation
            if (buffer == null) throw new ArgumentNullException("buffer");

            HashDataInternal(buffer.AsSpan(0, buffer.Length));
            _hashValue = EndHashInternal();
            byte[] Tmp = (byte[])_hashValue.Clone();
            Initialize();
            return (Tmp);
        }

        public Hash128 ComputeHash(ReadOnlySpan<byte> buffer)
        {
            HashDataInternal(buffer);
            _hashValue = EndHashInternal();
            Hash128 returnVal = new Hash128(_hashValue.AsSpan(0, 16));
            Initialize();
            return returnVal;
        }

        public byte[] ComputeHash(byte[] buffer, int offset, int count)
        {
            // Do some validation
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");
            if (count < 0 || (count > buffer.Length))
                throw new ArgumentException("count");
            if ((buffer.Length - count) < offset)
                throw new ArgumentException("offset");

            HashDataInternal(buffer.AsSpan(offset, count));
            _hashValue = EndHashInternal();
            byte[] Tmp = (byte[])_hashValue.Clone();
            Initialize();
            return (Tmp);
        }

        //// We implement TransformBlock and TransformFinalBlock here
        //public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        //{
        //    // Do some validation, we let BlockCopy do the destination array validation
        //    if (inputBuffer == null)
        //        throw new ArgumentNullException("inputBuffer");
        //    if (inputOffset < 0)
        //        throw new ArgumentOutOfRangeException("inputOffset");
        //    if (inputCount < 0 || (inputCount > inputBuffer.Length))
        //        throw new ArgumentException("inputCount");
        //    if ((inputBuffer.Length - inputCount) < inputOffset)
        //        throw new ArgumentException("inputOffset");

        //    // Change the State value
        //    _state = 1;
        //    HashData(inputBuffer, inputOffset, inputCount);
        //    if ((outputBuffer != null) && ((inputBuffer != outputBuffer) || (inputOffset != outputOffset)))
        //        Buffer.BlockCopy(inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);
        //    return inputCount;
        //}

        //public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        //{
        //    // Do some validation
        //    if (inputBuffer == null)
        //        throw new ArgumentNullException("inputBuffer");
        //    if (inputOffset < 0)
        //        throw new ArgumentOutOfRangeException("inputOffset");
        //    if (inputCount < 0 || (inputCount > inputBuffer.Length))
        //        throw new ArgumentException("inputOffset");
        //    if ((inputBuffer.Length - inputCount) < inputOffset)
        //        throw new ArgumentException("inputCount");

        //    HashData(inputBuffer, inputOffset, inputCount);
        //    _hashValue = EndHash();
        //    byte[] outputBytes;
        //    if (inputCount != 0)
        //    {
        //        outputBytes = new byte[inputCount];
        //        Buffer.BlockCopy(inputBuffer, inputOffset, outputBytes, 0, inputCount);
        //    }
        //    else
        //    {
        //        outputBytes = BinaryHelpers.EMPTY_BYTE_ARRAY;
        //    }
        //    // reset the State value
        //    _state = 0;
        //    return outputBytes;
        //}

        #endregion

        #region Private methods

        private void Initialize()
        {
            InitializeState();

            // Zero potentially sensitive information.
            ArrayExtensions.WriteZeroes(_buffer, 0, _buffer.Length);
            ArrayExtensions.WriteZeroes(_expandedBuffer, 0, _expandedBuffer.Length);
        }

        private void InitializeState()
        {
            _count = 0;

            _stateSHA1[0] = 0x67452301;
            _stateSHA1[1] = 0xefcdab89;
            _stateSHA1[2] = 0x98badcfe;
            _stateSHA1[3] = 0x10325476;
            _stateSHA1[4] = 0xc3d2e1f0;
        }

        #endregion
        
        #region Hash implementation

        /// <summary>
        /// SHA block update operation. Continues an SHA message-digest
        /// operation, processing another message block, and updating the
        /// context.
        /// </summary>
        /// <param name="input">The span of bytes to process</param>
        private void HashDataInternal(ReadOnlySpan<byte> input)
        {
            int bufferLen;
            int partInLen = input.Length;
            int partInBase = 0;

            /* Compute length of buffer */
            bufferLen = (int)(_count & 0x3f);

            /* Update number of bytes */
            _count += partInLen;
            
            // partial block of 64 bytes
            if ((bufferLen > 0) && (bufferLen + partInLen >= 64))
            {
                int blockSize = (64 - bufferLen);
                input.Slice(partInBase, blockSize).CopyTo(_buffer.AsSpan(bufferLen, blockSize));
                partInBase += blockSize;
                partInLen -= blockSize;
                SHATransform(_expandedBuffer, _stateSHA1, _buffer);
                bufferLen = 0;
            }

            /* Copy input to temporary buffer and hash */
            // Full 64 byte blocks
            while (partInLen >= 64)
            {
                input.Slice(partInBase, 64).CopyTo(_buffer.AsSpan());
                partInBase += 64;
                partInLen -= 64;
                SHATransform(_expandedBuffer, _stateSHA1, _buffer);
            }

            // And any remainder to use for the next block transform
            if (partInLen > 0)
            {
                input.Slice(partInBase, partInLen).CopyTo(_buffer.AsSpan(bufferLen, partInLen));
            }
        }

        /// <summary>
        /// SHA finalization. Ends an SHA message-digest operation, writing
        /// the message digest.
        /// </summary>
        /// <returns></returns>
        private byte[] EndHashInternal()
        {
            byte[] pad;
            int padLen;
            long bitCount;
            byte[] hash = new byte[20];

            /* Compute padding: 80 00 00 ... 00 00 <bit count>
             */

            padLen = 64 - (int)(_count & 0x3f);
            if (padLen <= 8)
                padLen += 64;

            pad = new byte[padLen];
            pad[0] = 0x80;

            //  Convert count to bit count
            bitCount = _count * 8;

            pad[padLen - 8] = (byte)((bitCount >> 56) & 0xff);
            pad[padLen - 7] = (byte)((bitCount >> 48) & 0xff);
            pad[padLen - 6] = (byte)((bitCount >> 40) & 0xff);
            pad[padLen - 5] = (byte)((bitCount >> 32) & 0xff);
            pad[padLen - 4] = (byte)((bitCount >> 24) & 0xff);
            pad[padLen - 3] = (byte)((bitCount >> 16) & 0xff);
            pad[padLen - 2] = (byte)((bitCount >> 8) & 0xff);
            pad[padLen - 1] = (byte)((bitCount >> 0) & 0xff);

            /* Digest padding */
            HashDataInternal(pad.AsSpan(0, pad.Length));

            /* Store digest */
            DWORDToBigEndian(hash, _stateSHA1, 5);

            _hashValue = hash;
            return hash;
        }

        private static void SHATransform(uint[] expandedBuffer, uint[] state, byte[] block)
        {
            uint a = state[0];
            uint b = state[1];
            uint c = state[2];
            uint d = state[3];
            uint e = state[4];

            int i;

            DWORDFromBigEndian(expandedBuffer, 16, block);
            SHAExpand(expandedBuffer, 0);

            /* Round 1 */
            for (i = 0; i < 20; i += 5)
            {
                { (e) += (((((a)) << (5)) | (((a)) >> (32 - (5)))) + ((d) ^ ((b) & ((c) ^ (d)))) + (expandedBuffer[i]) + 0x5a827999); (b) = ((((b)) << (30)) | (((b)) >> (32 - (30)))); }
                { (d) += (((((e)) << (5)) | (((e)) >> (32 - (5)))) + ((c) ^ ((a) & ((b) ^ (c)))) + (expandedBuffer[i + 1]) + 0x5a827999); (a) = ((((a)) << (30)) | (((a)) >> (32 - (30)))); }
                { (c) += (((((d)) << (5)) | (((d)) >> (32 - (5)))) + ((b) ^ ((e) & ((a) ^ (b)))) + (expandedBuffer[i + 2]) + 0x5a827999); (e) = ((((e)) << (30)) | (((e)) >> (32 - (30)))); }; ;
                { (b) += (((((c)) << (5)) | (((c)) >> (32 - (5)))) + ((a) ^ ((d) & ((e) ^ (a)))) + (expandedBuffer[i + 3]) + 0x5a827999); (d) = ((((d)) << (30)) | (((d)) >> (32 - (30)))); }; ;
                { (a) += (((((b)) << (5)) | (((b)) >> (32 - (5)))) + ((e) ^ ((c) & ((d) ^ (e)))) + (expandedBuffer[i + 4]) + 0x5a827999); (c) = ((((c)) << (30)) | (((c)) >> (32 - (30)))); }; ;
            }

            /* Round 2 */
            for (; i < 40; i += 5)
            {
                { (e) += (((((a)) << (5)) | (((a)) >> (32 - (5)))) + ((b) ^ (c) ^ (d)) + (expandedBuffer[i]) + 0x6ed9eba1); (b) = ((((b)) << (30)) | (((b)) >> (32 - (30)))); }; ;
                { (d) += (((((e)) << (5)) | (((e)) >> (32 - (5)))) + ((a) ^ (b) ^ (c)) + (expandedBuffer[i + 1]) + 0x6ed9eba1); (a) = ((((a)) << (30)) | (((a)) >> (32 - (30)))); }; ;
                { (c) += (((((d)) << (5)) | (((d)) >> (32 - (5)))) + ((e) ^ (a) ^ (b)) + (expandedBuffer[i + 2]) + 0x6ed9eba1); (e) = ((((e)) << (30)) | (((e)) >> (32 - (30)))); }; ;
                { (b) += (((((c)) << (5)) | (((c)) >> (32 - (5)))) + ((d) ^ (e) ^ (a)) + (expandedBuffer[i + 3]) + 0x6ed9eba1); (d) = ((((d)) << (30)) | (((d)) >> (32 - (30)))); }; ;
                { (a) += (((((b)) << (5)) | (((b)) >> (32 - (5)))) + ((c) ^ (d) ^ (e)) + (expandedBuffer[i + 4]) + 0x6ed9eba1); (c) = ((((c)) << (30)) | (((c)) >> (32 - (30)))); }; ;
            }

            /* Round 3 */
            for (; i < 60; i += 5)
            {
                { (e) += (((((a)) << (5)) | (((a)) >> (32 - (5)))) + (((b) & (c)) | ((d) & ((b) | (c)))) + (expandedBuffer[i]) + 0x8f1bbcdc); (b) = ((((b)) << (30)) | (((b)) >> (32 - (30)))); }; ;
                { (d) += (((((e)) << (5)) | (((e)) >> (32 - (5)))) + (((a) & (b)) | ((c) & ((a) | (b)))) + (expandedBuffer[i + 1]) + 0x8f1bbcdc); (a) = ((((a)) << (30)) | (((a)) >> (32 - (30)))); }; ;
                { (c) += (((((d)) << (5)) | (((d)) >> (32 - (5)))) + (((e) & (a)) | ((b) & ((e) | (a)))) + (expandedBuffer[i + 2]) + 0x8f1bbcdc); (e) = ((((e)) << (30)) | (((e)) >> (32 - (30)))); }; ;
                { (b) += (((((c)) << (5)) | (((c)) >> (32 - (5)))) + (((d) & (e)) | ((a) & ((d) | (e)))) + (expandedBuffer[i + 3]) + 0x8f1bbcdc); (d) = ((((d)) << (30)) | (((d)) >> (32 - (30)))); }; ;
                { (a) += (((((b)) << (5)) | (((b)) >> (32 - (5)))) + (((c) & (d)) | ((e) & ((c) | (d)))) + (expandedBuffer[i + 4]) + 0x8f1bbcdc); (c) = ((((c)) << (30)) | (((c)) >> (32 - (30)))); }; ;
            }

            /* Round 4 */
            for (; i < 80; i += 5)
            {
                { (e) += (((((a)) << (5)) | (((a)) >> (32 - (5)))) + ((b) ^ (c) ^ (d)) + (expandedBuffer[i]) + 0xca62c1d6); (b) = ((((b)) << (30)) | (((b)) >> (32 - (30)))); }; ;
                { (d) += (((((e)) << (5)) | (((e)) >> (32 - (5)))) + ((a) ^ (b) ^ (c)) + (expandedBuffer[i + 1]) + 0xca62c1d6); (a) = ((((a)) << (30)) | (((a)) >> (32 - (30)))); }; ;
                { (c) += (((((d)) << (5)) | (((d)) >> (32 - (5)))) + ((e) ^ (a) ^ (b)) + (expandedBuffer[i + 2]) + 0xca62c1d6); (e) = ((((e)) << (30)) | (((e)) >> (32 - (30)))); }; ;
                { (b) += (((((c)) << (5)) | (((c)) >> (32 - (5)))) + ((d) ^ (e) ^ (a)) + (expandedBuffer[i + 3]) + 0xca62c1d6); (d) = ((((d)) << (30)) | (((d)) >> (32 - (30)))); }; ;
                { (a) += (((((b)) << (5)) | (((b)) >> (32 - (5)))) + ((c) ^ (d) ^ (e)) + (expandedBuffer[i + 4]) + 0xca62c1d6); (c) = ((((c)) << (30)) | (((c)) >> (32 - (30)))); }; ;
            }

            state[0] += a;
            state[1] += b;
            state[2] += c;
            state[3] += d;
            state[4] += e;
        }

        #endregion

        #region Helpers

        private static void DWORDFromBigEndian(uint[] x, int digits, byte[] block)
        {
            int i;
            int j;

            for (i = 0, j = 0; i < digits; i++, j += 4)
            {
                x[i] = (uint)((block[j] << 24) | (block[j + 1] << 16) | (block[j + 2] << 8) | block[j + 3]);
            }
        }

        /// <summary>
        /// encodes x (DWORD) into block (unsigned char), most significant byte first.
        /// digits == number of DWORDs
        /// </summary>
        /// <param name="block"></param>
        /// <param name="x"></param>
        /// <param name="digits"></param>
        private static void DWORDToBigEndian(byte[] block, uint[] x, int digits)
        {
            int i;
            int j;

            for (i = 0, j = 0; i < digits; i++, j += 4)
            {
                block[j] = (byte)((x[i] >> 24) & 0xff);
                block[j + 1] = (byte)((x[i] >> 16) & 0xff);
                block[j + 2] = (byte)((x[i] >> 8) & 0xff);
                block[j + 3] = (byte)(x[i] & 0xff);
            }
        }

        /// <summary>
        /// Expands x[0..15] into x[16..79], according to the recurrence
        /// x[i] = x[i - 3] ^ x[i - 8] ^ x[i - 14] ^ x[i - 16].
        /// </summary>
        /// <param name="x"></param>
        /// <param name="x_ptr"></param>
        private static void SHAExpand(uint[] x, int x_ptr)
        {
            int i;
            uint tmp;

            for (i = 16; i < 80; i++)
            {
                tmp = (x[i - 3 + x_ptr] ^ x[i - 8 + x_ptr] ^ x[i - 14 + x_ptr] ^ x[i - 16 + x_ptr]);
                x[i + x_ptr] = ((tmp << 1) | (tmp >> 31));
            }
        }

        #endregion
    }
}