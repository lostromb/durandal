using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Statistics.SVM
{
    public class OneClassSVM
    {
        private SVMParam param;
        private int cross_validation;
        private SVMModel model;
        private double _threshold = 0;
        private ILogger _logger;

        public void copy(OneClassSVM rhs2)
        {
            param = rhs2.param == null ? null : (SVMParam)rhs2.param.Clone();
            cross_validation = rhs2.cross_validation;
            _logger = rhs2._logger;
            _threshold = rhs2._threshold;
            model = rhs2.model == null ? null : (SVMModel)rhs2.model.Clone();
            if (model != null) model.Param = param;
        }

        private double threshold()
        {
            return _threshold;
        }
        
        public object Clone()
        {
            OneClassSVM clone = new OneClassSVM(null);
            clone.copy(this);
            return clone;
        }

        public OneClassSVM(ILogger logger)
        {
            param = new SVMParam();
            // default values
            param.SVMType = SVMParam.SVM_TYPE_ONE_CLASS;
            param.KernelType = SVMParam.KERNEL_TYPE_RBF;
            param.Degree = 3;
            param.Gamma = 0;    // 1/num_features
            param.Coef0 = 0;
            param.nu = 0.5;
            param.CacheSizeInMB = 100;
            param.C = 1;
            param.Epsilon = 1e-3;
            param.p = 0.1;
            param.UseShrinkingHeuristic = true;
            param.DoProbabilityEstimate = false;
            param.NumberWeight = 0;
            param.WeightLabel = new int[0];
            param.Weight = new double[0];
            cross_validation = 0;
            _logger = logger;
        }

        public SVMParam getParameters()
        {
            return param;
        }

        public void set_nu(double v)
        {
            param.nu = v;
        }

        public void set_gamma(double v)
        {
            param.Gamma = v;
        }

        public double evaluate(double[] x0)
        {
            int n = x0.Length;

            SVMNode[] x = new SVMNode[n];
            for (int j = 0; j < n; j++)
            {
                x[j] = new SVMNode();
                x[j].index = j + 1;
                x[j].value = x0[j];
            }

            double v = SupportVectorMachine.svm_predict(model, x);
            return v;
        }

        public bool IsOutlier(double[] tuple)
        {
            double p = evaluate(tuple);
            return p < threshold();
        }
        
        public void Fit(List<double[]> batch)
        {
            List<SVMNode[]> vx = new List<SVMNode[]>();
            int max_index = 0;

            int m = batch.Count;
            for (int i = 0; i < m; ++i)
            {
                double[] x0 = batch[i];
                
                int n = x0.Length;

                SVMNode[] x = new SVMNode[n];
                for (int j = 0; j < n; j++)
                {
                    x[j] = new SVMNode();
                    x[j].index = j + 1;
                    x[j].value = x0[j];
                }

                if (n > 0) max_index = Math.Max(max_index, x[n - 1].index);

                vx.Add(x);
            }

            SVMProblem prob = new SVMProblem();
            prob.ProblemSize = m;
            prob.x = new SVMNode[m][];
            for (int i = 0; i < prob.ProblemSize; i++)
                prob.x[i] = vx[i];
            prob.y = new double[m];
            for (int i = 0; i < prob.ProblemSize; i++)
                prob.y[i] = 0;

            if (param.Gamma == 0 && max_index > 0)
                param.Gamma = 1.0 / max_index;


            model = SupportVectorMachine.svm_train(prob, param, (iteration) =>
            {
                return false;
            },
            _logger);
        }
    }
}
