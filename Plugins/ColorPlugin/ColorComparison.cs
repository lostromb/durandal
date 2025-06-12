using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Color
{
    internal class ColorComparison
    {
        public ColorInformation FirstComparison;
        public ColorInformation SecondComparison;
        public Vector3f SimilarityVector;

        public ColorComparison(ColorInformation color)
        {
            FirstComparison = color;
            SecondComparison = null;
            SimilarityVector = color.RGBVector;
        }

        public ColorComparison(ColorInformation one, ColorInformation two)
        {
            FirstComparison = one;
            SecondComparison = two;
            SimilarityVector = one.RGBVector.Add(two.RGBVector).Multiply(0.5f);
        }
    }
}
