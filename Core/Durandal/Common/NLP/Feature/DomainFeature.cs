namespace Durandal.Common.NLP.Feature
{
    public class DomainFeature : ITrainingFeature
    {
        public string Domain;
        public string Intent;
        public string FeatureName;

        public DomainFeature() { }

        public DomainFeature(string dom, string intent, string feat)
        {
            Domain = dom;
            Intent = intent;
            FeatureName = feat;
        }

        public DomainFeature(string input)
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
                FeatureName = input.Substring(tabIndex + 1);
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return Domain + "/" + Intent + "\t" + FeatureName;
        }
    }
}
