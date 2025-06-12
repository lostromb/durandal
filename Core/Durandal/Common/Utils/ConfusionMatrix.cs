

namespace Durandal.Common.Utils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Durandal.API;
    using Durandal.Common.MathExt;
    using Durandal.Common.Logger;

    public class ConfusionMatrix
    {
        private IDictionary<string, Counter<string>> _matrix;
        private Counter<string> _totalCounts;
        private ILogger _outputLogger;

        public ConfusionMatrix(ILogger outputLogger)
        {
            _matrix = new Dictionary<string, Counter<string>>();
            _totalCounts = new Counter<string>();
            _outputLogger = outputLogger;
        }

        public void Increment(string actual, string expected)
        {
            if (!_matrix.ContainsKey(expected))
            {
                _matrix[expected] = new Counter<string>();
            }
            _matrix[expected].Increment(actual);
        }

        public void IncrementExpected(string expected)
        {
            _totalCounts.Increment(expected);
        }

        private ISet<string> GetDomainsIntents()
        {
            HashSet<string> domainsIntents = new HashSet<string>();
            foreach (string d in _matrix.Keys)
            {
                if (!domainsIntents.Contains(d))
                {
                    domainsIntents.Add(d);
                }
            }
            foreach (Counter<string> ctr in _matrix.Values)
            {
                foreach (KeyValuePair<string, float> d in ctr)
                {
                    if (!domainsIntents.Contains(d.Key))
                    {
                        domainsIntents.Add(d.Key);
                    }
                }
            }
            return domainsIntents;
        }

        public void PrintTopCells(int count)
        {
            List<ConfusionPoint> points = new List<ConfusionPoint>();
            ISet<string> domainsIntents = GetDomainsIntents();
            foreach (string y in domainsIntents)
            {
                float testsInExpectedDomain = _totalCounts.GetCount(y);

                foreach (string x in domainsIntents)
                {
                    if (_matrix.ContainsKey(y))
                    {
                        float testsFailedInThisDomain = _matrix[y].GetCount(x);
                        points.Add(new ConfusionPoint()
                        {
                            Actual = x,
                            Expected = y,
                            ErrorRate = testsFailedInThisDomain / testsInExpectedDomain
                        });
                    }
                }
            }
            points.Sort();
            for (int c = 0; c < count && c < points.Count; c++)
            {
                ConfusionPoint pt = points[c];
                _outputLogger.Log(string.Format("{0:F1}% of time expected {1} and got {2}", (pt.ErrorRate * 100), pt.Expected, pt.Actual));
            }
        }

        private class ConfusionPoint : IComparable
        {
            public string Expected;
            public string Actual;
            public float ErrorRate;

            public int CompareTo(object obj)
            {
                ConfusionPoint o = obj as ConfusionPoint;
                if (o == null)
                    return 0;
                return System.Math.Sign(o.ErrorRate - ErrorRate);
            }

            protected bool Equals(ConfusionPoint other)
            {
                return string.Equals(Expected, other.Expected) && ErrorRate == other.ErrorRate && string.Equals(Actual, other.Actual);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ConfusionPoint) obj);
            }

            public override int GetHashCode()
            {
                return Expected.GetHashCode() + Actual.GetHashCode() + ErrorRate.GetHashCode();
            }
        }

        public void WriteToCSV(Stream outStream)
        {
            using (StreamWriter fileOut = new StreamWriter(outStream))
            {
                ISet<string> domainsIntents = GetDomainsIntents();
                fileOut.Write("Y=Expected X=Actual,");
                foreach (string x in domainsIntents)
                {
                    fileOut.Write(x + ",");
                }

                fileOut.WriteLine();
                foreach (string y in domainsIntents)
                {
                    float testsInExpectedDomain = _totalCounts.GetCount(y);
                    fileOut.Write(y + ",");
                    foreach (string x in domainsIntents)
                    {
                        if (_matrix.ContainsKey(y))
                        {
                            float testsFailedInThisDomain = _matrix[y].GetCount(x);
                            fileOut.Write((testsFailedInThisDomain / testsInExpectedDomain) + ",");
                        }
                        else
                            fileOut.Write("0,");
                    }
                    fileOut.WriteLine();
                }
            }
        }
    }
}
