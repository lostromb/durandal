using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas.Responses
{
    public class WeightLogsResponse
    {
        public IList<WeightLogInternal> weight { get; set; }
    }
}
