/* P/Invoke adapter for CRC-32C library: https://crc32c.machinezoo.com/ */
/*
  Copyright (c) 2013 - 2014, 2016 Mark Adler, Robert Vazan, Max Vysokikh

  This software is provided 'as-is', without any express or implied
  warranty.  In no event will the author be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
  claim that you wrote the original software. If you use this software
  in a product, an acknowledgment in the product documentation would be
  appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
  misrepresented as being the original software.
  3. This notice may not be removed or altered from any source distribution.
*/

using Durandal.Common.IO.Crc;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Durandal.Extensions.Compression.Crc
{
    /// <summary>
    /// Implementation of CRC32C backed by a P/Invoke native layer
    /// </summary>
    public class NativeCRC32C : ICRC32C
    {
        private const string LibraryName = "crc32c";

        private readonly ICRC32C _singleByteFallbackImpl;

        public static bool Initialize(ILogger logger)
        {
            NativeLibraryStatus status = NativePlatformUtils.PrepareNativeLibrary(LibraryName, logger);
            return status == NativeLibraryStatus.Available;
        }

        /// <summary>
        /// Constructs a P/Invoke adapter to CRC32C calculation code.
        /// </summary>
        /// <param name="singleByteFallbackImpl">Calling into native code to process a single byte is slower than just doing
        /// it in managed code. Therefore, we accept a different CRC implementation here which is called by the
        /// single-byte Slurp() overload</param>
        public NativeCRC32C(ICRC32C singleByteFallbackImpl)
        {
            _singleByteFallbackImpl = singleByteFallbackImpl.AssertNonNull(nameof(singleByteFallbackImpl));
        }

        /// <inheritdoc />
        public void Slurp(ref CRC32CState state, byte value)
        {
            // It's wasteful to call into native code and do unsafe pointers just to process a single byte.
            // So we just do managed code here instead.
            _singleByteFallbackImpl.Slurp(ref state, value);

            //byte* dataPtr = (byte*)Unsafe.AsPointer(ref value);
            //state.Checksum = crc32c_append(state.Checksum, dataPtr, new IntPtr(1));
        }

        /// <inheritdoc />
        public unsafe void Slurp(ref CRC32CState state, ReadOnlySpan<byte> value)
        {
            if (value.Length == 0)
            {
                return;
            }

            fixed (byte* dataPtr = &value[0])
            {
                IntPtr length = new IntPtr(value.Length);
                state.Checksum = crc32c_append(state.Checksum, dataPtr, length);
            }
        }

        /// <summary>
        /// Computes CRC-32C (Castagnoli) checksum. Uses Intel's CRC32 instruction if it is available.
        /// Otherwise it uses a very fast software fallback.
        /// </summary>
        /// <param name="crc">Initial CRC value. Typically it's 0.
        /// You can supply non-trivial initial value here.
        /// Initial value can be used to chain CRC from multiple buffers.</param>
        /// <param name="input">Data to be put through the CRC algorithm.</param>
        /// <param name="length">Length of the data in the input buffer.</param>
        /// <returns>The updated CRC value.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern uint crc32c_append(uint crc, byte* input, IntPtr length);
    }
}
