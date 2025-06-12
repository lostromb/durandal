using Durandal.Common.MathExt;
using System;

namespace Durandal.Common.Statistics.NeuralNet
{
    public static class RandomProvider
    {
        public static readonly IRandom random = new FastRandom(5);
    }
}
