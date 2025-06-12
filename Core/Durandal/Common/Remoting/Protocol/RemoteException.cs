using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteException
    {
        public string ExceptionType { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }

        /// <summary>
        /// Serialization constructor
        /// </summary>
        [JsonConstructor]
        public RemoteException()
        {
        }

        public RemoteException(Exception e)
        {
            ExceptionType = e.GetType().Name;
            Message = e.Message;
            StackTrace = e.StackTrace;
        }
    }
}
