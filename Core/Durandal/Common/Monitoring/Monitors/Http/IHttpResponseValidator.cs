using Durandal.Common.Net.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Monitoring.Monitors.Http
{
    /// <summary>
    /// Defines an object which performs validation on a single HTTP response.
    /// </summary>
    public interface IHttpResponseValidator
    {
        Task<SingleTestResult> Validate(HttpResponse responseMessage, ArraySegment<byte> responseContent);
    }
}
