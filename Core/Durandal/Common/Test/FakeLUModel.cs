using Durandal.Common.Config;
using Durandal.Common.NLP.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Test
{
    public class FakeLUModel
    {
        public string Domain;
        public List<string> Regexes = new List<string>();
        public List<string> Training = new List<string>();
        public IConfiguration DomainConfig;

        public void AddRegex(string intent, string regex)
        {
            Regexes.Add(string.Format("{0}/{1}\t{2}", Domain, intent, regex));
        }

        public void AddTraining(string intent, string utterance)
        {
            Training.Add(string.Format("{0}/{1}\t{2}", Domain, intent, utterance));
        }
    }
}
