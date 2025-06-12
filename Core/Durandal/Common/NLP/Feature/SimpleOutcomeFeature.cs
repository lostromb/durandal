namespace Durandal.Common.NLP.Feature
{
    public class SimpleOutcomeFeature : ITrainingFeature
    {
        public string FeatureName;
        public string Outcome;

        private SimpleOutcomeFeature() { }

        public SimpleOutcomeFeature(string input, string output)
        {
            FeatureName = input;
            Outcome = output;
        }

        public SimpleOutcomeFeature(string input)
        {
            Parse(input);
        }

        public bool Parse(string input)
        {
            int tabIndex = input.IndexOf('\t');
            if (tabIndex > 0)
            {
                FeatureName = input.Substring(0, tabIndex);
                Outcome = input.Substring(tabIndex + 1);
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return FeatureName + "\t" + Outcome;
        }

        public static SimpleOutcomeFeature CreateStatic(string dataLine)
        {
            SimpleOutcomeFeature returnVal = new SimpleOutcomeFeature();
            if (returnVal.Parse(dataLine))
            {
                return returnVal;
            }

            return null;
        }
    }
}
