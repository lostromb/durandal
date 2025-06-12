namespace Durandal.Common.NLP.Feature
{
    public class TagFeature : ITrainingFeature
    {
        public string SourceTag;
        public string DestTag;
        public string FeatureName;

        private TagFeature() { }

        public TagFeature(string source, string dest, string featureName)
        {
            SourceTag = source;
            DestTag = dest;
            FeatureName = featureName;
        }

        public TagFeature(string input)
        {
            Parse(input);
        }

        public bool Parse(string input)
        {
            int tabIndex = input.IndexOf('\t');
            int secondTabIndex = input.IndexOf('\t', tabIndex + 1);
            if (tabIndex > 0 && secondTabIndex > 0)
            {
                SourceTag = input.Substring(0, tabIndex);
                DestTag = input.Substring(tabIndex + 1, secondTabIndex - tabIndex - 1);
                FeatureName = input.Substring(secondTabIndex + 1);
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return SourceTag + "\t" + DestTag + "\t" + FeatureName;
        }
        public static TagFeature CreateStatic(string dataLine)
        {
            TagFeature returnVal = new TagFeature();
            if (returnVal.Parse(dataLine))
            {
                return returnVal;
            }

            return null;
        }
    }
}
