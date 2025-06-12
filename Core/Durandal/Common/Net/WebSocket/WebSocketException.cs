using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.WebSocket
{
    public class WebSocketException : Exception
    {
        public WebSocketException() : base("An error occurred in the web socket") { }
        public WebSocketException(string errorMessage) : base(errorMessage) { }
        public WebSocketException(string errorMessage, Exception innerException) : base(errorMessage, innerException) { }
    }
}
