namespace Durandal.Common.NLP.Feature
{
    public interface ITrainingFeature
    {
        bool Parse(string input);
        string ToString();
    }
}
