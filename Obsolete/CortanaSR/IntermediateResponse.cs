using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Durandal.Common.Speech.SR.Cortana
{
    internal class IntermediateResponse
    {
        public string DisplayText { get; set; }
        public DebugInfo DebugInfo { get; set; }

        public static IntermediateResponse ParseFromXml(string xml)
        {
            XDocument xd = XDocument.Parse(xml);
            return new IntermediateResponse(xd.Root);
        }

        internal IntermediateResponse(XElement xml)
        {
            foreach (XElement child in xml.Elements())
            {
                if (child.Name == "Entry" && child.HasAttributes)
                {
                    XElement contentNode = child.Element(XName.Get("Content"));
                    if (contentNode != null)
                    {
                        if (string.Equals(child.Attribute(XName.Get("type")).Value, "DebugInfo"))
                        {
                            XElement contentElement = contentNode.Elements().FirstOrDefault();
                            if (string.Equals(contentElement.Name.LocalName, "DebugInfo"))
                            {
                                DebugInfo = new DebugInfo(contentElement);
                            }
                        }
                        else if (string.Equals(child.Attribute(XName.Get("type")).Value, "DisplayText"))
                        {
                            DisplayText = contentNode.Value;
                        }
                    }
                }
            }
        }
    }
}
