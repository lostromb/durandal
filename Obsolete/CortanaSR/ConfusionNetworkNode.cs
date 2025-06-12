using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Durandal.Common.Speech.SR.Cortana
{
    internal class ConfusionNetworkNode
    {
        public TimeSpan AudioTimeOffset { get; set; }
        public ushort FirstFollowingArc { get; set; }

        internal ConfusionNetworkNode(XElement xml)
        {
            foreach (XElement child in xml.Elements())
            {
                if (string.Equals(child.Name.LocalName, "AudioTimeOffset"))
                {
                    AudioTimeOffset = TimeSpan.FromTicks(long.Parse(child.Value));
                }
                else if (string.Equals(child.Name.LocalName, "FirstFollowingArc"))
                {
                    FirstFollowingArc = ushort.Parse(child.Value);
                }
            }
        }
    }
}
