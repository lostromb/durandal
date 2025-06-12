using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.NLP.Feature;

namespace Durandal.Common.NLP.Train
{
    public interface ICrossTrainFilter<T> where T : ITrainingFeature
    {
        bool Passes(T feature);
    }
}
