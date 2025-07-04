﻿using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Statistics.SVM
{
    public class SVR
    {
        private SVMParam _param;
        private bool _crossValidation;
        private SVMModel _model;
        private ILogger _logger;

        public SVMModel Model
        {
            get { return _model; }
            set { _model = value; }
        }

        public SVMParam Param
        {
            get { return _param; }
            set { _param = value; }
        }

        public bool CrossValidation
        {
            get { return _crossValidation; }
            set { _crossValidation = value; }
        }
        
        public void Copy(SVR rhs)
        {
            _param = rhs._param == null ? null : (SVMParam)rhs._param.Clone();
            _crossValidation = rhs._crossValidation;
            _model = rhs._model == null ? null : (SVMModel)rhs._model.Clone();
            if (_model != null) _model.Param = _param;
            _logger = rhs._logger;
        }
        
        public object Clone()
        {
            SVR clone = new SVR(_logger);
            clone.Copy(this);

            return clone;
        }

        public SVR(ILogger logger)
        {
            _param = new SVMParam();
            // default values
            _param.SVMType = SVMParam.SVM_TYPE_NU_SVR;
            _param.KernelType = SVMParam.KERNEL_TYPE_RBF;
            _param.Degree = 3;
            _param.Gamma = 0;    // 1/num_features
            _param.Coef0 = 0;
            _param.nu = 0.5;
            _param.CacheSizeInMB = 100;
            _param.C = 1;
            _param.Epsilon = 1e-3;
            _param.p = 0.1;
            _param.UseShrinkingHeuristic = true;
            _param.DoProbabilityEstimate = false;
            _param.NumberWeight = 0;
            _param.WeightLabel = new int[0];
            _param.Weight = new double[0];
            _crossValidation = false;
            _logger = logger;
        }

        public SVMType getSVMType()
        {
            if (_param.SVMType == SVMParam.SVM_TYPE_EPSILON_SVR)
            {
                return SVMType.epsilon;
            }
            else {
                return SVMType.nu;
            }
        }

        public void setSVMType(SVMType type)
        {
            switch (type)
            {
                case SVMType.nu:
                    _param.SVMType = SVMParam.SVM_TYPE_NU_SVR;
                    break;
                case SVMType.epsilon:
                    _param.SVMType = SVMParam.SVM_TYPE_EPSILON_SVR;
                    break;
            }
        }

        public SVMParam getParameters()
        {
            return _param;
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

            double v = SupportVectorMachine.svm_predict(_model, x);
            return v;
        }
        
        public double Predict(double[] tuple)
        {
            return evaluate(tuple);
        }
        
        public MSEMetric Fit(List<KeyValuePair<double[], double>> train_data, List<KeyValuePair<double[], double>> test_data)
        {
            List<double> vy = new List<double>();
            List<SVMNode[]> vx = new List<SVMNode[]>();
            int max_index = 0;

            int m = train_data.Count;
            for(int i = 0; i<m; ++i)
            {
                double[] x0 = train_data[i].Key;
                int n = x0.Length;

                vy.Add(train_data[i].Value);
                SVMNode[] x = new SVMNode[n];
                for(int j = 0; j<n; j++)
                {
                    x[j] = new SVMNode();
                    x[j].index = j+1;
                    x[j].value = x0[j];
                }

                if(n>0) max_index = Math.Max(max_index, x[n - 1].index);

                vx.Add(x);
            }

            SVMProblem prob = new SVMProblem();
            prob.ProblemSize = m;
            prob.x = new SVMNode[prob.ProblemSize][];
            for(int i = 0; i<prob.ProblemSize;i++)
                prob.x[i] = vx[i];
            prob.y = new double[prob.ProblemSize];
            for(int i = 0; i<prob.ProblemSize;i++)
                prob.y[i] = vy[i];

            if(_param.Gamma == 0 && max_index > 0)
                _param.Gamma = 1.0/max_index;
            
            _model = SupportVectorMachine.svm_train(prob, _param, (iteration) =>
            {
                return false;
            },
            _logger);

            MSEMetric metric = new MSEMetric();
            metric.TrainMSE = GetMSE(train_data);
            metric.TestMSE = GetMSE(test_data);

            return metric;
        }

        private double GetMSE(List<KeyValuePair<double[], double>> data)
        {
            double result = 0;
            foreach(KeyValuePair<double[], double> entry in data)
            {
                double[] x = entry.Key;
                double y = entry.Value;
                double predicted = Predict(x);
                result += (predicted - y) * (predicted - y);
            }
            return result / data.Count;
        }

        public enum SVMType
        {
            nu,
            epsilon
        }
    }
}
