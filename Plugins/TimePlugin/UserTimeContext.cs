using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Time
{
    public class UserTimeContext
    {
        public DateTimeOffset UserLocalTime { get; set; }
        public string UserTimeZone { get; set; }
    }
}
