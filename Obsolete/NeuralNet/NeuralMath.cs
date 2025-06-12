using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Classification.NeuralNet
{
    public static class NeuralMath
    {
        public static float Activate(ActivationType actType, float input)
        {
            switch (actType)
            {
                case ActivationType.Binary:
                    if (input > 0)
                        return 1;
                    else
                        return 0;

                case ActivationType.Bipolar:

                    if (input > 0)
                        return 1;
                    else
                        return -1;

                case ActivationType.Ramp:
                    return input;

                case ActivationType.Sigmoid:
                    return FastMath.Sigmoid(input);

                default:
                    return 0;
                case ActivationType.BipolarSigmoid:
                    return (float)((2 / (1 + Math.Exp(0 - input))) - 1);

            }
        }
    }
}
