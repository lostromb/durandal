using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Used with instrumented audio graphs to write warning messages to a logger whenever audio components introduce excessive latency
    /// (usually defined as an operation takes longer on the CPU than the time length of the audio being processed; in other words,
    /// slower than real-time processing).
    /// </summary>
    public class StutterReportingInstrumentationDelegate
    {
        private readonly ILogger _logger;

        public StutterReportingInstrumentationDelegate(ILogger logger)
        {
            _logger = logger.AssertNonNull(nameof(logger));
        }

        public void HandleInstrumentation(Counter<string> componentExclusiveLatencies, TimeSpan latencyBudget, TimeSpan actualInclusiveLatency)
        {
            if (latencyBudget > TimeSpan.Zero && actualInclusiveLatency > latencyBudget)
            {
                _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "Audio stutter detected! Budget {0:F2} ms actual {1:F2} ms", latencyBudget.TotalMilliseconds, actualInclusiveLatency.TotalMilliseconds);
                float thresholdLatency = (float)latencyBudget.TotalMilliseconds / 10.0f;
                foreach (var kvp in componentExclusiveLatencies)
                {
                    if (kvp.Value > thresholdLatency)
                    {
                        _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "  {0:F3} ms spent in {1}", kvp.Value, kvp.Key);
                    }
                }
            }
        }
    }
}
