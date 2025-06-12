using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class SearchEntity
    {
        public string id { get; set; }
        public string bingId { get; set; }
        public string readLink { get; set; }
        public string webSearchUrl { get; set; }
        public string name { get; set; }
        public SearchImage image { get; set; }
        public string description { get; set; }
        public EntityPresentationInfo entityPresentationInfo { get; set; }
        public GeoPosition geo { get; set; }
        public Address address { get; set; }
        public string telephone { get; set; }
    }
}
