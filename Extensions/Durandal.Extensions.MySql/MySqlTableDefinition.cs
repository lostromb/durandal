using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.MySql
{
    public class MySqlTableDefinition
    {
        public string TableName { get; set; }
        public string CreateStatement { get; set; }
    }
}
