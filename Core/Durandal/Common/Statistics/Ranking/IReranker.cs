using System.Collections.Generic;
using Durandal.API;

namespace Durandal.Common.Statistics.Ranking
{
    public interface IReranker
    {
        void Rerank(ref List<RecoResult> results);
    }

    public class NullReranker : IReranker
    {
        public void Rerank(ref List<RecoResult> results) { }
    }
}
