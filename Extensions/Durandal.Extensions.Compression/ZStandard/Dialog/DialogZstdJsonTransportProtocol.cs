using System;
using System.Collections.Generic;
using System.Text;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.IO;
using Durandal.Common.Compression.LZ4;
using System.IO;
using Durandal.Common.Dialog.Web;

namespace Durandal.Extensions.Compression.ZStandard.Dialog
{
    public class DialogZstdJsonTransportProtocol : ZstdDialogProtocolWrapper
    {
        public DialogZstdJsonTransportProtocol(int compressionLevel = 8)
            : base(
                new DialogJsonTransportProtocol(),
                protocolName: "zstdjson",
                contentEncoding: "zstd",
                compressionLevel: compressionLevel)
        { }
    }
}
