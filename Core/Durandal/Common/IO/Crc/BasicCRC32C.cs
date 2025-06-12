using System;

namespace Durandal.Common.IO.Crc
{
    /// <summary>
    /// This algorithm is very very slow. Its only advantage is that is endian-independent and can
    /// be used as a baseline for testing correctness of other implementations.
    /// </summary>
    internal class BasicCRC32C : ICRC32C
    {
        private const uint POLY = 0x82f63b78U;

        /// <inheritdoc />
        public void Slurp(ref CRC32CState state, byte value)
        {
            state.Checksum = state.Checksum ^ value;
            for (int j = 0; j < 8; j++)
            {
                state.Checksum = (state.Checksum >> 1) ^ 0x80000000 ^ ((~state.Checksum & 1) * POLY);
            }
        }

        /// <inheritdoc />
        public void Slurp(ref CRC32CState state, ReadOnlySpan<byte> value)
        {
            foreach (byte b in value)
            {
                state.Checksum = state.Checksum ^ b;
                for (int j = 0; j < 8; j++)
                {
                    state.Checksum = (state.Checksum >> 1) ^ 0x80000000 ^ ((~state.Checksum & 1) * POLY);
                }
            }
        }
    }
}
