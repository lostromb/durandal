namespace Durandal.Common.NLP.Feature
{
    public class DomainIntentContextFeature : ITrainingFeature
    {
        public string Domain;
        public string Intent;
        public string[] Context;

        private DomainIntentContextFeature() { }

        public DomainIntentContextFeature(string dom, string intent, string[] features)
        {
            Domain = dom;
            Intent = intent;
            Context = features;
        }

        public DomainIntentContextFeature(string input)
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
                Context = input.Substring(tabIndex + 1).Split('\t');
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return Domain + "/" + Intent + "\t" + string.Join("\t", Context);
        }

        public static DomainIntentContextFeature CreateStatic(string dataLine)
        {
            DomainIntentContextFeature returnVal = new DomainIntentContextFeature();
            if (returnVal.Parse(dataLine))
            {
                return returnVal;
            }

            return null;
        }
    }
}
