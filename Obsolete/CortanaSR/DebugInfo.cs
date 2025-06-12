using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Durandal.Common.Speech.SR.Cortana
{
    internal class DebugInfo
    {
        public string TraceID { get; set; }
        public string DateTime { get; set; }
        public string MachineName { get; set; }
        public string ConversationID { get; set; }
        public IDictionary<string, string> PropertyBag { get; private set; }
        public string ImpressionGUID { get; set; }
        public string ServiceVersion { get; set; }
        
        internal DebugInfo(XElement xml)
        {
            PropertyBag = new Dictionary<string, string>();

            foreach (XElement child in xml.Elements())
            {
                if (string.Equals(child.Name.LocalName, "TraceID"))
                {
                    TraceID = child.Value;
                }
                else if (string.Equals(child.Name.LocalName, "DateTime"))
                {
                    DateTime = child.Value;
                }
                else if (string.Equals(child.Name.LocalName, "MachineName"))
                {
                    MachineName = child.Value;
                }
                else if (string.Equals(child.Name.LocalName, "ConversationID"))
                {
                    ConversationID = child.Value;
                }
                else if (string.Equals(child.Name.LocalName, "PropertyBag"))
                {
                    foreach (XElement property in child.Elements(XName.Get("Property")))
                    {
                        string k = property.Attribute(XName.Get("Key")).Value;
                        string v = property.Attribute(XName.Get("Value")).Value;
                        PropertyBag.Add(k, v);
                    }
                }
                else if (string.Equals(child.Name.LocalName, "ImpressionGUID"))
                {
                    ImpressionGUID = child.Value;
                }
                else if (string.Equals(child.Name.LocalName, "ServiceVersion"))
                {
                    ServiceVersion = child.Value;
                }
            }
        }
    }
}
