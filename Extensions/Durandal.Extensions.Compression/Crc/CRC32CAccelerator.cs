

namespace Durandal.Extensions.Compression.Crc
{
    using Durandal.Common.IO.Crc;
    using Durandal.Common.Logger;
    using Durandal.Common.Utils.NativePlatform;
    using System;

    /// <summary>
    /// Accelerates <see cref="CRC32CFactory" /> using a native library layer.
    /// </summary>
    public class CRC32CAccelerator : IAccelerator
    {
        private static ICRC32C _bestImplementation;

        /// <inheritdoc />
        public bool Apply(ILogger logger)
        {
            bool returnVal = false;
            ICRC32C bestManagedImplementation = null;
            string logMessage = null;

#if NETCOREAPP
            if (System.Runtime.Intrinsics.X86.Sse42.X64.IsSupported)
            {
                logMessage = "Accelerating CRC32c using x64 SSE";
                bestManagedImplementation = new ManagedCRC32C_SSE_X64();
                returnVal = true;
            }
            else if (System.Runtime.Intrinsics.X86.Sse42.IsSupported)
            {
                logMessage = "Accelerating CRC32c using x86 SSE";
                bestManagedImplementation = new ManagedCRC32C_SSE_X86();
                returnVal = true;
            }
            else if (System.Runtime.Intrinsics.Arm.Crc32.Arm64.IsSupported)
            {
                logMessage = "Accelerating CRC32c using ARM64";
                bestManagedImplementation = new ManagedCRC32C_ARM64();
                returnVal = true;
            }
            else if (System.Runtime.Intrinsics.Arm.Crc32.IsSupported)
            {
                logMessage = "Accelerating CRC32c using ARM";
                bestManagedImplementation = new ManagedCRC32C_ARM();
                returnVal = true;
            }
#endif
            
            if (bestManagedImplementation == null)
            {
                logMessage = "CRC32c can't be accelerated any more on this current platform";
                bestManagedImplementation = CRC32CFactory.BestManagedImplementation;
            }

            try
            {
                if (NativeCRC32C.Initialize(logger))
                {
                    logMessage = "Accelerating CRC32c using native code adapter";
                    _bestImplementation = new NativeCRC32C(bestManagedImplementation);
                    returnVal = true;
                }
                else
                {
                    _bestImplementation = bestManagedImplementation;
                }
            }
            catch (Exception e)
            {
                logger.Log(e);
                _bestImplementation = bestManagedImplementation;
            }

            if (returnVal)
            {
                if (logMessage != null)
                {
                    logger.Log(logMessage, LogLevel.Std);
                }

                CRC32CFactory.SetGlobalFactory(AcceleratedFactory);
            }

            return returnVal;
        }

        /// <inheritdoc />
        public void Unapply(ILogger logger)
        {
            CRC32CFactory.SetGlobalFactory(null);
        }

        private static ICRC32C AcceleratedFactory()
        {
            return _bestImplementation;
        }
    }
}
