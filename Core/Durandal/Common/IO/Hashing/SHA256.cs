namespace Durandal.Common.IO.Hashing
{
    using System;
    using System.Security;
    using System.IO;
    using Durandal.Common.IO;
    using Durandal.Common.Collections;

    /// <summary>
    /// Portable version of Microsoft's SHA256
    /// </summary>
    public class SHA256
    {
        private byte[] _hashValue;
        private int _state = 0;
        private byte[] _buffer;
        private long _count; // Number of bytes in the hashed message
        private uint[] _stateSHA256;
        private uint[] _W;

        public SHA256()
        {
            _stateSHA256 = new uint[8];
            _buffer = new byte[64];
            _W = new uint[64];

            InitializeInternal();
        }

        #region Public methods

        /// <summary>
        /// Gets the number of bits in the hash digest
        /// </summary>
        public int HashSize
        {
            get { return 256; }
        }

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
                        HashDataInternal(buffer, 0, bytesRead);
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

            HashDataInternal(buffer, 0, buffer.Length);
            _hashValue = EndHashInternal();
            byte[] Tmp = (byte[])_hashValue.Clone();
            Initialize();
            return (Tmp);
        }

        public byte[] ComputeHash(byte[] buffer, int offset, int count)
        {
            // Do some validation
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", "Offset must be non-negative");
            if (count < 0 || (count > buffer.Length))
                throw new ArgumentException("Input count must be non-zero and less then size of buffer");
            if ((buffer.Length - count) < offset)
                throw new ArgumentException("Count is larger than input buffer size");

            HashDataInternal(buffer, offset, count);
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
        //        throw new ArgumentOutOfRangeException("inputOffset", "Offset must be non-negative");
        //    if (inputCount < 0 || (inputCount > inputBuffer.Length))
        //        throw new ArgumentException("Input count must be non-zero and less then size of buffer");
        //    if ((inputBuffer.Length - inputCount) < inputOffset)
        //        throw new ArgumentException("Count is larger than input buffer size");

        //    // Change the State value
        //    _state = 1;
        //    HashDataInternal(inputBuffer, inputOffset, inputCount);
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
        //        throw new ArgumentOutOfRangeException("inputOffset", "Offset must be non-negative");
        //    if (inputCount < 0 || (inputCount > inputBuffer.Length))
        //        throw new ArgumentException("Input count must be non-zero and less then size of buffer");
        //    if ((inputBuffer.Length - inputCount) < inputOffset)
        //        throw new ArgumentException("Count is larger than input buffer size");

        //    HashDataInternal(inputBuffer, inputOffset, inputCount);
        //    _hashValue = EndHashInternal();
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

        public void Reset()
        {
            Initialize();
        }

        #endregion

        #region Private methods

        private void InitializeInternal()
        {
            _count = 0;

            _stateSHA256[0] = 0x6a09e667;
            _stateSHA256[1] = 0xbb67ae85;
            _stateSHA256[2] = 0x3c6ef372;
            _stateSHA256[3] = 0xa54ff53a;
            _stateSHA256[4] = 0x510e527f;
            _stateSHA256[5] = 0x9b05688c;
            _stateSHA256[6] = 0x1f83d9ab;
            _stateSHA256[7] = 0x5be0cd19;
        }

        private void Initialize()
        {
            InitializeInternal();

            // Zeroize potentially sensitive information.
            ArrayExtensions.WriteZeroes(_buffer, 0, _buffer.Length);
            ArrayExtensions.WriteZeroes(_W, 0, _W.Length);
        }

        #endregion

        #region Hash implementation

        /// <summary>
        /// SHA256 block update operation. Continues an SHA message-digest
        /// operation, processing another message block, and updating the
        /// context.
        /// </summary>
        /// <param name="partIn"></param>
        /// <param name="ibStart"></param>
        /// <param name="cbSize"></param>
        private void HashDataInternal(byte[] partIn, int ibStart, int cbSize)
        {
            int bufferLen;
            int partInLen = cbSize;
            int partInBase = ibStart;

            /* Compute length of buffer */
            bufferLen = (int)(_count & 0x3f);

            /* Update number of bytes */
            _count += partInLen;

            if ((bufferLen > 0) && (bufferLen + partInLen >= 64))
            {
                ArrayExtensions.MemCopy(partIn, partInBase, _buffer, bufferLen, 64 - bufferLen);
                partInBase += (64 - bufferLen);
                partInLen -= (64 - bufferLen);
                SHATransform(_W, _stateSHA256, _buffer);
                bufferLen = 0;
            }

            /* Copy input to temporary buffer and hash */
            while (partInLen >= 64)
            {
                ArrayExtensions.MemCopy(partIn, partInBase, _buffer, 0, 64);
                partInBase += 64;
                partInLen -= 64;
                SHATransform(_W, _stateSHA256, _buffer);
            }

            if (partInLen > 0)
            {
                ArrayExtensions.MemCopy(partIn, partInBase, _buffer, bufferLen, partInLen);
            }
        }

        /// <summary>
        /// SHA256 finalization. Ends an SHA256 message-digest operation, writing
        /// the message digest.
        /// </summary>
        /// <returns></returns>
        private byte[] EndHashInternal()
        {
            byte[] pad;
            int padLen;
            long bitCount;
            byte[] hash = new byte[32]; // HashSizeValue = 256

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
            HashDataInternal(pad, 0, pad.Length);

            /* Store digest */
            DWORDToBigEndian(hash, _stateSHA256, 8);

            _hashValue = hash;
            return hash;
        }

        private readonly static uint[] _K = {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5,
            0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3,
            0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
            0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc,
            0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7,
            0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
            0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3,
            0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5,
            0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
            0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
        };
        
        private static void SHATransform(uint[] expandedBuffer, uint[] state, byte[] block)
        {
            uint a, b, c, d, e, f, h, g;
            uint aa, bb, cc, dd, ee, ff, hh, gg;
            uint T1;

            a = state[0];
            b = state[1];
            c = state[2];
            d = state[3];
            e = state[4];
            f = state[5];
            g = state[6];
            h = state[7];

            // fill in the first 16 bytes of W.
            DWORDFromBigEndian(expandedBuffer, 16, block);
            SHA256Expand(expandedBuffer);

            /* Apply the SHA256 compression function */
            // We are trying to be smart here and avoid as many copies as we can
            // The perf gain with this method over the straightforward modify and shift 
            // forward is >= 20%, so it's worth the pain
            for (int j = 0; j < 64;)
            {
                T1 = h + Sigma_1B(e) + Ch(e, f, g) + _K[j] + expandedBuffer[j];
                ee = d + T1;
                aa = T1 + Sigma_0B(a) + Maj(a, b, c);
                j++;

                T1 = g + Sigma_1B(ee) + Ch(ee, e, f) + _K[j] + expandedBuffer[j];
                ff = c + T1;
                bb = T1 + Sigma_0B(aa) + Maj(aa, a, b);
                j++;

                T1 = f + Sigma_1B(ff) + Ch(ff, ee, e) + _K[j] + expandedBuffer[j];
                gg = b + T1;
                cc = T1 + Sigma_0B(bb) + Maj(bb, aa, a);
                j++;

                T1 = e + Sigma_1B(gg) + Ch(gg, ff, ee) + _K[j] + expandedBuffer[j];
                hh = a + T1;
                dd = T1 + Sigma_0B(cc) + Maj(cc, bb, aa);
                j++;

                T1 = ee + Sigma_1B(hh) + Ch(hh, gg, ff) + _K[j] + expandedBuffer[j];
                h = aa + T1;
                d = T1 + Sigma_0B(dd) + Maj(dd, cc, bb);
                j++;

                T1 = ff + Sigma_1B(h) + Ch(h, hh, gg) + _K[j] + expandedBuffer[j];
                g = bb + T1;
                c = T1 + Sigma_0B(d) + Maj(d, dd, cc);
                j++;

                T1 = gg + Sigma_1B(g) + Ch(g, h, hh) + _K[j] + expandedBuffer[j];
                f = cc + T1;
                b = T1 + Sigma_0B(c) + Maj(c, d, dd);
                j++;

                T1 = hh + Sigma_1B(f) + Ch(f, g, h) + _K[j] + expandedBuffer[j];
                e = dd + T1;
                a = T1 + Sigma_0B(b) + Maj(b, c, d);
                j++;
            }

            state[0] += a;
            state[1] += b;
            state[2] += c;
            state[3] += d;
            state[4] += e;
            state[5] += f;
            state[6] += g;
            state[7] += h;
        }

        #endregion

        #region Helpers

        private static uint RotateRight(uint x, int n)
        {
            return (((x) >> (n)) | ((x) << (32 - (n))));
        }

        private static uint Ch(uint x, uint y, uint z)
        {
            return ((x & y) ^ ((x ^ 0xffffffff) & z));
        }

        private static uint Maj(uint x, uint y, uint z)
        {
            return ((x & y) ^ (x & z) ^ (y & z));
        }

        private static uint Sigma_0A(uint x)
        {
            return (RotateRight(x, 7) ^ RotateRight(x, 18) ^ (x >> 3));
        }

        private static uint Sigma_1A(uint x)
        {
            return (RotateRight(x, 17) ^ RotateRight(x, 19) ^ (x >> 10));
        }

        private static uint Sigma_0B(uint x)
        {
            return (RotateRight(x, 2) ^ RotateRight(x, 13) ^ RotateRight(x, 22));
        }

        private static uint Sigma_1B(uint x)
        {
            return (RotateRight(x, 6) ^ RotateRight(x, 11) ^ RotateRight(x, 25));
        }

        /* This function creates W_16,...,W_63 according to the formula
           W_j <- sigma_1a(W_{j-2}) + W_{j-7} + sigma_0a(W_{j-15}) + W_{j-16};
        */
        
        private static void SHA256Expand(uint[] x)
        {
            for (int i = 16; i < 64; i++)
            {
                x[i] = Sigma_1A(x[i - 2]) + x[i - 7] + Sigma_0A(x[i - 15]) + x[i - 16];
            }
        }

        // digits == number of DWORDs
        private static void DWORDFromBigEndian(uint[] x, int digits, byte[] block)
        {
            int i;
            int j;

            for (i = 0, j = 0; i < digits; i++, j += 4)
            {
                x[i] = (uint)((block[j] << 24) | (block[j + 1] << 16) | (block[j + 2] << 8) | block[j + 3]);
            }
        }

        // encodes x (DWORD) into block (unsigned char), most significant byte first. 
        // digits == number of DWORDs
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

        #endregion
    }
}