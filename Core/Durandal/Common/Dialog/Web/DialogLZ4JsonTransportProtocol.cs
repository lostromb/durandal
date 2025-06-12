using System;
using System.Collections.Generic;
using System.Text;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.IO;
using Durandal.Common.Compression.LZ4;
using System.IO;

namespace Durandal.Common.Dialog.Web
{
    public class DialogLZ4JsonTransportProtocol : LZ4DialogProtocolWrapper
    {
        public DialogLZ4JsonTransportProtocol() : base(new DialogJsonTransportProtocol()) { }
    }
}
