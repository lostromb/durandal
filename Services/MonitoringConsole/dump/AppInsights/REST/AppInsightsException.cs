using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.AppInsights.REST
{
    /// <summary>
    /// An exception indicating an error with the AppInsights data source
    /// </summary>
    /// <param name="message"></param>
    [Serializable]
    public class AppInsightsException : Exception
    {
        public AppInsightsException()
        {
        }

        public AppInsightsException(string message) : base(message)
        {
        }

        public AppInsightsException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AppInsightsException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
