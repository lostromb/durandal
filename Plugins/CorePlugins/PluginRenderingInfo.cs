using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Reflection
{
    public class PluginRenderingInfo
    {
        public string Name;
        public string Subtitle;
        public string IconUrl;
        public string ShortDescription;
        public string LongDescription;
        public string InfoLink;
        public IList<PluginSampleQuery> SampleQueries;
    }
}
