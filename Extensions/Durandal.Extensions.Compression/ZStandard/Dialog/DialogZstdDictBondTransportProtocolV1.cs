using Durandal.Extensions.BondProtocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Extensions.Compression.ZStandard.Dialog
{
    public class DialogZstdDictBondTransportProtocolV1 : ZstdDialogProtocolWrapper
    {
        public DialogZstdDictBondTransportProtocolV1(int compressionLevel = 8)
            : base(
                new DialogBondTransportProtocol(),
                protocolName: "zstd-cdv1-bond",
                contentEncoding: "zstd-cdv1-bond",
                dictionary: EmbeddedDictionaries.ddl_bond_v1,
                compressionLevel: compressionLevel)
        { }
    }
}
