using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class Serving
    {
        public float multiplier { get; set; }
        public float servingSize { get; set; }
        public ServingUnit unit { get; set; }
        public ulong unitId { get; set; }
    }
}
