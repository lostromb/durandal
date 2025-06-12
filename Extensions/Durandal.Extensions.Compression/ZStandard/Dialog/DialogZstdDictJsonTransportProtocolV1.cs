using Durandal.Common.Dialog.Web;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Extensions.Compression.ZStandard.Dialog
{
    public class DialogZstdDictJsonTransportProtocolV1 : ZstdDialogProtocolWrapper
    {
        public DialogZstdDictJsonTransportProtocolV1(int compressionLevel = 8)
            : base(
                new DialogJsonTransportProtocol(),
                protocolName: "zstd-cdv1-json",
                contentEncoding: "zstd-cdv1-json",
                dictionary: EmbeddedDictionaries.ddl_json_v1,
                compressionLevel: compressionLevel)
        { }
    }
}
