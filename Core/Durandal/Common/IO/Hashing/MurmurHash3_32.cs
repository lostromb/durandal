using Durandal.Common.Collections;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.IO.Hashing
{
    /// <summary>
    /// An implementation of MurmurHash3 - 32-bit variant
    /// </summary>
    public struct MurmurHash3_32
    {
        private readonly uint _seed;
        private bool _finished;
        private uint _hash;
        private uint _length;
        private uint _keyBlock;
        private int _keyShift;

        public MurmurHash3_32(uint seed)
        {
            _seed = seed;
            _hash = _seed;
            _keyBlock = 0;
            _keyShift = 0;
            _length = 0;
            _finished = false;
        }

        public void Reset()
        {
            _hash = _seed;
            _keyBlock = 0;
            _keyShift = 0;
            _length = 0;
            _finished = false;
        }

        public void Ingest(byte[] data, int offset, int count)
        {
            data.AssertNonNull(nameof(data));
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("Count cannot be negative");
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException("Offset cannot be negative");
            }

            Ingest(data.AsSpan(offset, count));
        }

        public void Ingest(ReadOnlySpan<byte> data)
        {
            int used = 0;

            // Continue a partial key if we didn't fill one out last time
            if (_keyShift != 0)
            {
                if (BitConverter.IsLittleEndian)
                {
                    while (_keyShift < 32 && used < data.Length)
                    {
                        _keyBlock |= (uint)(data[used] << _keyShift);
                        _keyShift += 8;
                        used++;
                    }
                }
                else
                {
                    while (_keyShift < 32 && used < data.Length)
                    {
                        _keyBlock = _keyBlock >> 8;
                        _keyBlock |= (uint)(data[used] << 24);
                        _keyShift += 8;
                        used++;
                    }
                }

                if (_keyShift == 32)
                {
                    _hash ^= murmur_32_scramble(_keyBlock);
                    _hash = (_hash << 13) | (_hash >> 19);
                    _hash = _hash * 5 + 0xe6546b64;
                    _keyShift = 0;
                    _keyBlock = 0;
                }

                if (used == data.Length)
                {
                    _length += (uint)data.Length;
                    return;
                }
            }

            // Read in groups of 4
            int blocksRemaining = (data.Length - used) / sizeof(uint);

            // FIXME we would get better performance if we ensured that all the data accesses here
            // were aligned to sizeof(uint) in memory. Also the cast itself may fail on platforms
            // that don't support unaligned reads e.g. ARM7
            ReadOnlySpan<uint> castData = MemoryMarshal.Cast<byte, uint>(data.Slice(used, blocksRemaining * sizeof(uint)));
            for (int block = 0; block < blocksRemaining; block++)
            {
                _keyBlock = castData[block]; // this copy assumes little endian - might get messy on other platforms
                _hash ^= murmur_32_scramble(_keyBlock);
                _hash = (_hash << 13) | (_hash >> 19);
                _hash = _hash * 5 + 0xe6546b64;
                used += sizeof(uint);
            }

            // Read any remainder into k and store the state for later
            _keyBlock = 0;
            if (BitConverter.IsLittleEndian)
            {
                // rather than doing byte swapping in the main block copy operation,
                // we do byte swapping here because we assume this code will be
                // much less on the hot path. It makes the outcome of hashing different
                // when compared across platforms, but it is consistent within one platform
                while (used < data.Length)
                {
                    _keyBlock |= (uint)(data[used] << _keyShift);
                    _keyShift += 8;
                    used++;
                }
            }
            else
            {
                while (used < data.Length)
                {
                    _keyBlock = _keyBlock >> 8;
                    _keyBlock |= (uint)(data[used] << 24);
                    _keyShift += 8;
                    used++;
                }
            }

            _length += (uint)data.Length;
        }

        public uint Finish()
        {
            if (_finished)
            {
                return _hash;
            }

            _finished = true;

            _hash ^= murmur_32_scramble(_keyBlock);
            _hash ^= _length;
            _hash ^= _hash >> 16;
            _hash *= 0x85ebca6b;
            _hash ^= _hash >> 13;
            _hash *= 0xc2b2ae35;
            _hash ^= _hash >> 16;
            return _hash;
        }

        public static uint HashSingleInteger(uint input, uint seed)
        {
            uint h = seed;
            h ^= murmur_32_scramble(input);
            h = (h << 13) | (h >> 19);
            h = h * 5 + 0xe6546b64;
            //h ^= murmur_32_scramble(0x00); // not necessary because it scrambles to zero anyways
            h ^= 0x4; // length
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;
            return h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint murmur_32_scramble(uint k)
        {
            k *= 0xcc9e2d51;
            k = (k << 15) | (k >> 17);
            k *= 0x1b873593;
            return k;
        }
    }
}
