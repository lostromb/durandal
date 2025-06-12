using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Durandal.Common.Speech.SR.Cortana
{
    internal class ConversationResponse
    {
        public SpeechRecognitionResult SpeechRecognitionResult { get; set; }
        public ConfusionNetworkResult ConfusionNetworkResult { get; set; }
        public DebugInfo DebugInfo { get; set; }

        public static ConversationResponse ParseFromXml(string xml)
        {
            XDocument xd = XDocument.Parse(xml);
            return new ConversationResponse(xd.Root);
        }

        internal ConversationResponse(XElement xml)
        {
            foreach (XElement child in xml.Elements())
            {
                if (child.Name == "Entry")
                {
                    XElement contentNode = child.Element(XName.Get("Content"));
                    if (contentNode != null)
                    {
                        XElement contentElement = contentNode.Elements().FirstOrDefault();
                        if (contentElement == null)
                        {
                        }
                        else if (string.Equals(contentElement.Name.LocalName, "SpeechRecognitionResult"))
                        {
                            SpeechRecognitionResult = new SpeechRecognitionResult(contentElement);
                        }
                        else if (string.Equals(contentElement.Name.LocalName, "ConfusionNetworkResult"))
                        {
                            ConfusionNetworkResult = new ConfusionNetworkResult(contentElement);
                        }
                        else if (string.Equals(contentElement.Name.LocalName, "DebugInfo"))
                        {
                            DebugInfo = new DebugInfo(contentElement);
                        }
                    }
                }
            }
        }
    }
}
