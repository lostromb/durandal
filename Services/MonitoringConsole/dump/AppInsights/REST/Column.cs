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
    public class Column
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public string ColumnType { get; set; }
    }
}
