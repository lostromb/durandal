using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http2
{
    public class Http2ProtocolException : Exception
    {
        public Http2ProtocolException() : base("An error occurred in the HTTP/2 protocol") { }
        public Http2ProtocolException(string errorMessage) : base(errorMessage) { }
        public Http2ProtocolException(string errorMessage, Exception innerException) : base(errorMessage, innerException) { }
    }
}
