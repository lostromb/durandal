using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Durandal.Common.Speech.SR.Cortana
{
    internal class SpeechRecognitionResult
    {
        public int RecognitionStatus { get; set; }
        public int InverseTextNormalizationStatus { get; set; }
        public List<RecognizedPhrase> RecognizedPhrases { get; private set; }

        internal SpeechRecognitionResult(XElement xml)
        {
            RecognizedPhrases = new List<RecognizedPhrase>();

            foreach (XElement child in xml.Elements())
            {
                if (string.Equals(child.Name.LocalName, "RecognitionStatus"))
                {
                    RecognitionStatus = int.Parse(child.Value);
                }
                else if (string.Equals(child.Name.LocalName, "InverseTextNormalizationStatus"))
                {
                    InverseTextNormalizationStatus = int.Parse(child.Value);
                }
                else if (string.Equals(child.Name.LocalName, "RecognizedPhrases"))
                {
                    foreach (XElement subElement in child.Elements(XName.Get("RecognizedPhrase")))
                    {
                        RecognizedPhrases.Add(new RecognizedPhrase(subElement));
                    }
                }
            }
        }
    }
}
