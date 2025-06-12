using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class SearchImage
    {
        public string thumbnailUrl { get; set; }
        public string contentUrl { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }
}
