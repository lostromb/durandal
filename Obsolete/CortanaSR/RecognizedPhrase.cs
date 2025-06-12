using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Durandal.Common.Speech.SR.Cortana
{
    internal class RecognizedPhrase
    {
        public string DisplayText { get; set; }
        public string LexicalForm { get; set; }
        public float SREngineConfidence { get; set; }
        public float Confidence { get; set; }
        public TimeSpan MediaDuration { get; set; }
        public TimeSpan MediaTime { get; set; }
        public List<PhraseElement> PhraseElements { get; private set; }
        public TimeSpan StartTime { get; set; }
        public List<string> InverseTextNormalizationResults { get; private set; }
        public List<string> MaskedInverseTextNormalizationResults { get; private set; }
        
        internal RecognizedPhrase(XElement xml)
        {
            PhraseElements = new List<PhraseElement>();
            InverseTextNormalizationResults = new List<string>();
            MaskedInverseTextNormalizationResults = new List<string>();

            foreach (XElement child in xml.Elements())
            {
                if (string.Equals(child.Name.LocalName, "DisplayText"))
                {
                    DisplayText = child.Value;
                }
                else if (string.Equals(child.Name.LocalName, "LexicalForm"))
                {
                    LexicalForm = child.Value;
                }
                else if (string.Equals(child.Name.LocalName, "SREngineConfidence"))
                {
                    SREngineConfidence = float.Parse(child.Value);
                }
                else if (string.Equals(child.Name.LocalName, "Confidence"))
                {
                    Confidence = float.Parse(child.Value);
                }
                else if (string.Equals(child.Name.LocalName, "MediaDuration"))
                {
                    MediaDuration = TimeSpan.FromTicks(long.Parse(child.Value));
                }
                else if (string.Equals(child.Name.LocalName, "MediaTime"))
                {
                    MediaTime = TimeSpan.FromTicks(long.Parse(child.Value));
                }
                else if (string.Equals(child.Name.LocalName, "PhraseElements"))
                {
                    foreach (XElement subElement in child.Elements(XName.Get("PhraseElement")))
                    {
                        PhraseElements.Add(new PhraseElement(subElement));
                    }
                }
                else if (string.Equals(child.Name.LocalName, "StartTime"))
                {
                    StartTime = TimeSpan.FromTicks(long.Parse(child.Value));
                }
                else if (string.Equals(child.Name.LocalName, "InverseTextNormalizationResults"))
                {
                    foreach (XElement subElement in child.Elements(XName.Get("InverseTextNormalizationResult")))
                    {
                        InverseTextNormalizationResults.Add(subElement.Value);
                    }
                }
                else if (string.Equals(child.Name.LocalName, "MaskedInverseTextNormalizationResults"))
                {
                    foreach (XElement subElement in child.Elements(XName.Get("MaskedInverseTextNormalizationResult")))
                    {
                        MaskedInverseTextNormalizationResults.Add(subElement.Value);
                    }
                }
            }
        }
    }
}