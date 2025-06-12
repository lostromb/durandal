using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers
{
    public class KeywordSpottingConfiguration
    {
        public string PrimaryKeyword;
        public double PrimaryKeywordSensitivity;
        public List<string> SecondaryKeywords;
        public double SecondaryKeywordSensitivity;
    }
}
