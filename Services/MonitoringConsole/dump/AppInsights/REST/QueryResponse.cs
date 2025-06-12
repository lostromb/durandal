using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.AppInsights.REST
{
    /// <summary>
    /// The JSON schema for responses from the APInsights REST endpoint
    /// </summary>
    public class QueryResponse
    {
        public IList<Table> Tables { get; set; }
    }
}
