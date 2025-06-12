using Durandal.API;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Dialog.Web
{
    public interface IDialogTransportProtocol
    {
        DialogRequest ParseClientRequest(PooledBuffer<byte> input, ILogger logger);
        DialogRequest ParseClientRequest(ArraySegment<byte> input, ILogger logger);
        PooledBuffer<byte> WriteClientRequest(DialogRequest input, ILogger logger);
        DialogResponse ParseClientResponse(PooledBuffer<byte> input, ILogger logger);
        DialogResponse ParseClientResponse(ArraySegment<byte> input, ILogger logger);
        PooledBuffer<byte> WriteClientResponse(DialogResponse input, ILogger logger);

        string ContentEncoding { get; }
        string MimeType { get; }
        string ProtocolName { get; }
    }
}
