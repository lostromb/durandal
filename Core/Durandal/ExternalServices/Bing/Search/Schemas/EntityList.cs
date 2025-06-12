using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class EntityList
    {
        public string readLink { get; set; }
        public string queryScenario { get; set; }
        public List<SearchEntity> value { get; set; }
    }
}
