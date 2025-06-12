using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Durandal.Common.Speech.SR.Cortana
{
    internal class ConfusionNetworkResult
    {
        public int Version { get; set; }
        public List<string> WordTable { get; private set; }
        public List<ConfusionNetworkNode> Nodes { get; private set; }
        public List<ConfusionNetworkArc> Arcs { get; private set; }
        public List<ushort> BestArcsIndexes { get; private set; }

        internal ConfusionNetworkResult(XElement xml)
        {
            WordTable = new List<string>();
            Nodes = new List<ConfusionNetworkNode>();
            Arcs = new List<ConfusionNetworkArc>();
            BestArcsIndexes = new List<ushort>();

            foreach (XElement child in xml.Elements())
            {
                if (string.Equals(child.Name.LocalName, "Version"))
                {
                    Version = int.Parse(child.Value);
                }
                else if (string.Equals(child.Name.LocalName, "WordTable"))
                {
                    foreach (XElement subElement in child.Elements(XName.Get("Word")))
                    {
                        WordTable.Add(subElement.Value);
                    }
                }
                else if (string.Equals(child.Name.LocalName, "Nodes"))
                {
                    foreach (XElement subElement in child.Elements(XName.Get("Node")))
                    {
                        Nodes.Add(new ConfusionNetworkNode(subElement));
                    }
                }
                else if (string.Equals(child.Name.LocalName, "Arcs"))
                {
                    foreach (XElement subElement in child.Elements(XName.Get("Arc")))
                    {
                        Arcs.Add(new ConfusionNetworkArc(subElement));
                    }
                }
                else if (string.Equals(child.Name.LocalName, "BestArcsIndexes"))
                {
                    foreach (XElement subElement in child.Elements(XName.Get("ArcIndex")))
                    {
                        BestArcsIndexes.Add(ushort.Parse(child.Value));
                    }
                }
            }
        }
    }
}
