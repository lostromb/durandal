using System;
using System.Collections.Generic;
using System.Text;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.IO;
using Durandal.Common.Compression.LZ4;
using System.IO;
using Durandal.Common.Dialog.Web;

namespace Durandal.Extensions.BondProtocol
{
    /// <summary>
    /// This protocol is quite useless and is probably best avoided.
    /// </summary>
    public class DialogLZ4BondTransportProtocol : LZ4DialogProtocolWrapper
    {
        public DialogLZ4BondTransportProtocol() : base(new DialogBondTransportProtocol()) { }
    }
}
