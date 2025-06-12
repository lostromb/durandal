using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Durandal.Common.Speech.SR.Cortana
{
    internal class PhraseElement
    {
        public float SREngineConfidence { get; set; }
        public float Confidence { get; set; }
        public string LexicalForm { get; set; }
        public string DisplayText { get; set; }
        public TimeSpan AudioTimeOffset { get; set; }
        public TimeSpan MediaDuration { get; set; }

        internal PhraseElement(XElement xml)
        {
            foreach (XElement child in xml.Elements())
            {
                if (string.Equals(child.Name.LocalName, "SREngineConfidence"))
                {
                    SREngineConfidence = float.Parse(child.Value);
                }
                else if (string.Equals(child.Name.LocalName, "Confidence"))
                {
                    Confidence = float.Parse(child.Value);
                }
                else if (string.Equals(child.Name.LocalName, "LexicalForm"))
                {
                    LexicalForm = child.Value;
                }
                else if (string.Equals(child.Name.LocalName, "DisplayText"))
                {
                    DisplayText = child.Value;
                }
                else if (string.Equals(child.Name.LocalName, "AudioTimeOffset"))
                {
                    AudioTimeOffset = TimeSpan.FromTicks(long.Parse(child.Value));
                }
                else if (string.Equals(child.Name.LocalName, "MediaDuration"))
                {
                    MediaDuration = TimeSpan.FromTicks(long.Parse(child.Value));
                }
            }
        }
    }
}
