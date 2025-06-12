using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.AppInsights.REST
{
    /// <summary>
    /// Part of the JSON schema for the APInsights REST API
    /// </summary>
    public class Table
    {
        public string TableName { get; set; }
        public IList<Column> Columns { get; set; }
        public IList<object[]> Rows { get; set; }
    }
}
