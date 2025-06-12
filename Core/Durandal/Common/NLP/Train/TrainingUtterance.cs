using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.Common.NLP.Feature;

namespace Durandal.Common.NLP.Train
{
    public class TrainingUtterance : ITrainingFeature
    {
        public string Domain;
        public string Intent;
        public string Utterance;

        public TrainingUtterance() { }

        public TrainingUtterance(string input)
        {
            Parse(input);
        }

        public bool Parse(string input)
        {
            int slashIndex = input.IndexOf('/');
            int tabIndex = input.IndexOf('\t');
            if (slashIndex > 0 && tabIndex > slashIndex)
            {
                Domain = input.Substring(0, slashIndex);
                Intent = input.Substring(slashIndex + 1, tabIndex - slashIndex - 1);
                Utterance = input.Substring(tabIndex + 1);
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return Domain + "/" + Intent + "\t" + Utterance;
        }
    }
}
