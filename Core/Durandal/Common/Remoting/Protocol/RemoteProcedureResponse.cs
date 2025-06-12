using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteProcedureResponse<T>
    {
        public static readonly RemoteMessageType RESPONSE_MESSAGE_TYPE = RemoteMessageType.Response;

        public RemoteMessageType MessageType => RESPONSE_MESSAGE_TYPE;

        /// <summary>
        /// The name of the method that was invoked
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// If the remote method threw an exception, the details will be found here
        /// </summary>
        public RemoteException Exception { get; set; }

        /// <summary>
        /// The return value of the method
        /// </summary>
        public T ReturnVal { get; set; }

        [JsonConstructor]
        private RemoteProcedureResponse()
        {
        }

        public RemoteProcedureResponse(string methodName, T response)
        {
            MethodName = methodName;
            ReturnVal = response;
        }

        public RemoteProcedureResponse(string methodName, Exception exception)
        {
            MethodName = methodName;
            Exception = new RemoteException(exception);
        }

#pragma warning disable CA1030 // Use events where appropriate
        public void RaiseExceptionIfPresent()
#pragma warning restore CA1030 // Use events where appropriate
        {
            if (Exception != null)
            {
                using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                {
                    StringBuilder messageBuilder = pooledSb.Builder;
                    messageBuilder.Append("Remote exception: ");
                    messageBuilder.Append(Exception.ExceptionType);
                    messageBuilder.Append(" Message: ");
                    messageBuilder.Append(Exception.Message);
                    messageBuilder.Append(" StackTrace: ");
                    messageBuilder.Append(Exception.StackTrace);
                    throw new Exception(messageBuilder.ToString());
                }
            }
        }
    }
}
