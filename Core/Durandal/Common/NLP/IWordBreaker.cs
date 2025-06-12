using Durandal.API;
using Durandal.Common.LU;

namespace Durandal.Common.NLP
{
    public interface IWordBreaker
    {
        Sentence Break(LUInput input);
        Sentence Break(string input);
    }
}
