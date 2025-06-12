using Durandal.API;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.LU
{
    public interface ILUTransportProtocol
    {
        LURequest ParseLURequest(ArraySegment<byte> input, ILogger logger);
        PooledBuffer<byte> WriteLURequest(LURequest input, ILogger logger);
        LUResponse ParseLUResponse(ArraySegment<byte> input, ILogger logger);
        PooledBuffer<byte> WriteLUResponse(LUResponse input, ILogger logger);
        string MimeType { get; }
        string ProtocolName { get; }
    }
}
