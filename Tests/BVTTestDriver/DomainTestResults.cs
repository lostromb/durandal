using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.Common.MathExt;

namespace BVTTestDriver
{
    public class DomainTestResults
    {
        public string Domain;
        public int TestsRun;
        public int TestsSucceeded;
        public int TestsFailed;
        public int FailedByQasTimeout;
        public int FailedByNoQasResults;
        public int FailedByQuerySkipped;
        public int FailedByWrongDomainAndIntent;
        public int FailedByWrongIntent;
        public int FailedByDialogError;

        private StaticAverage _precision = new StaticAverage();
        private StaticAverage _recall = new StaticAverage();

        public float GetSuccessPercentage()
        {
            if (TestsRun == 0)
                return 100f;
            return (100f * (float)TestsSucceeded / (float)TestsRun);
        }

        public void AddPrecisionRecall(float precision, float recall)
        {
            _precision.Add(precision);
            _recall.Add(recall);
        }

        public bool HasTags()
        {
            return (_precision.NumItems > 0 &&
                _recall.NumItems > 0);
        }

        public float GetPrecision()
        {
            if (_precision.NumItems == 0)
                return 1.0f;
            return (float)_precision.Average;
        }

        public float GetRecall()
        {
            if (_recall.NumItems == 0)
                return 1.0f;
            return (float)_recall.Average;
        }

        public float GetF1()
        {
            if (GetPrecision() + GetRecall() <= 0)
                return 0;
            return (2 * GetPrecision() * GetRecall()) / (GetPrecision() + GetRecall());
        }
    }
}
