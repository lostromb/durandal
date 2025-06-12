using Durandal.Extensions.BondProtocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Extensions.Compression.ZStandard.Dialog
{
    /// <summary>
    /// This protocol is quite useless and is probably best avoided.
    /// </summary>
    public class DialogZstdBondTransportProtocol : ZstdDialogProtocolWrapper
    {
        public DialogZstdBondTransportProtocol(int compressionLevel = 8)
            : base(
                new DialogBondTransportProtocol(),
                protocolName: "zstdbond",
                contentEncoding: "zstd",
                compressionLevel: compressionLevel)
        { }
    }
}
