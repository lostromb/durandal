using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public abstract class RemoteProcedureRequest
    {
        public static readonly RemoteMessageType REQUEST_MESSAGE_TYPE = RemoteMessageType.Request;

        public RemoteMessageType MessageType => REQUEST_MESSAGE_TYPE;

        /// <summary>
        /// The name of the remote method to be invoked
        /// </summary>
        public abstract string MethodName { get; }
    }
}
