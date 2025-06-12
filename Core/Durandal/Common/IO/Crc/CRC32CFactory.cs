using Durandal.Common.Utils;
using System;

namespace Durandal.Common.IO.Crc
{
    /// <summary>
    /// Central location for creating <see cref="ICRC32C"/> instances.
    /// </summary>
    public static class CRC32CFactory
    {
        private static readonly ICRC32C _bestManagedImplementation;

        private static Func<ICRC32C> _globalFactory;

        static CRC32CFactory()
        {
            if (BitConverter.IsLittleEndian)
            {
                if (IntPtr.Size == 8) // 64-bit process
                {
                    _bestManagedImplementation = new ManagedCRC32C_Prefer64();
                }
                else
                {
                    _bestManagedImplementation = new ManagedCRC32C();
                }
            }
            else
            {
                _bestManagedImplementation = new ManagedCRC32C_BE();
            }

            _globalFactory = DefaultFactoryImpl;
        }

        /// <summary>
        /// Gets a "safe" fallback implementation of CRC32C which will run on all platforms.
        /// </summary>
        public static ICRC32C BestManagedImplementation => _bestManagedImplementation;

        /// <summary>
        /// Gets the best CRC32C implementation currently available to the system.
        /// </summary>
        /// <returns>A CRC32C algorithm implementation.</returns>
        public static ICRC32C Create()
        {
            return _globalFactory();
        }

        /// <summary>
        /// Used by accelerators to inject a faster platform-specific CRC32C implementation.
        /// </summary>
        /// <param name="newFactoryImpl">The new factory implementation, or null to revert to the default factory implementation.</param>
        public static void SetGlobalFactory(Func<ICRC32C> newFactoryImpl)
        {
            _globalFactory = newFactoryImpl ?? DefaultFactoryImpl;
        }

        private static ICRC32C DefaultFactoryImpl()
        {
            return _bestManagedImplementation;
        }
    }
}
