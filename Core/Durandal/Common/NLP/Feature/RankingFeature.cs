namespace Durandal.Common.NLP.Feature
{
    public class RankingFeature : ITrainingFeature
    {
        public int UtteranceId;
        public string FeatureName;
        public string Outcome;

        public RankingFeature() { }

        public RankingFeature(int id, string featureName, string outcome, float featureValue = float.NaN)
        {
            UtteranceId = id;
            FeatureName = featureName;
            Outcome = outcome;
        }

        public RankingFeature(string input)
        {
            Parse(input);
        }

        public bool Parse(string input)
        {
            int tabIndex = input.IndexOf('\t');
            int secondTabIndex = input.IndexOf('\t', tabIndex + 1);
            if (tabIndex > 0 && secondTabIndex > 0)
            {
                UtteranceId = int.Parse(input.Substring(0, tabIndex));
                FeatureName = input.Substring(tabIndex + 1, secondTabIndex - tabIndex - 1);
                Outcome = input.Substring(secondTabIndex + 1);
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return UtteranceId + "\t" + FeatureName + "\t" + Outcome;
        }
    }
}
