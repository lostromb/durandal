using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.IO.Crc
{
    /// <summary>
    /// State object that is manipulated by <see cref="ICRC32C"/> implementations.
    /// You can "reset" the CRC by just recreating this object.
    /// The current checksum is the value of the Checksum field.
    /// </summary>
    public struct CRC32CState
    {
        public uint Checksum;
    }
}
