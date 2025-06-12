using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Durandal.Common.Speech.SR.Cortana
{
    internal class ConfusionNetworkArc
    {
        public ushort PreviousNodeIndex { get; set; }
        public ushort NextNodeIndex { get; set; }
        public uint WordStartIndex { get; set; }
        public float Score { get; set; }
        public bool IsLastArc { get; set; }

        internal ConfusionNetworkArc(XElement xml)
        {
            foreach (XElement child in xml.Elements())
            {
                if (string.Equals(child.Name.LocalName, "PreviousNodeIndex"))
                {
                    PreviousNodeIndex = ushort.Parse(child.Value);
                }
                else if (string.Equals(child.Name.LocalName, "NextNodeIndex"))
                {
                    NextNodeIndex = ushort.Parse(child.Value);
                }
                else if (string.Equals(child.Name.LocalName, "WordStartIndex"))
                {
                    WordStartIndex = uint.Parse(child.Value);
                }
                else if (string.Equals(child.Name.LocalName, "Score"))
                {
                    Score = float.Parse(child.Value);
                }
                else if (string.Equals(child.Name.LocalName, "IsLastArc"))
                {
                    IsLastArc = bool.Parse(child.Value);
                }
            }
        }
    }
}
