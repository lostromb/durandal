using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.ICM
{
    public class TestAlertStatus
    {
        /// <summary>
        /// The suite name for this test
        /// </summary>
        public string SuiteName { get; set; }

        /// <summary>
        /// The name of this test
        /// </summary>
        public string TestName { get; set; }

        /// <summary>
        /// The beginning time of this test's most recent "failure incident", if any
        /// </summary>
        public DateTimeOffset? MostRecentFailureBegin { get; set; }

        /// <summary>
        /// The ending time of this test's most recent "failure incident", if any
        /// </summary>
        public DateTimeOffset? MostRecentFailureEnd { get; set; }

        /// <summary>
        /// The current alert level
        /// </summary>
        public AlertLevel MostRecentFailureLevel { get; set; }

        /// <summary>
        /// The default alert level that will be raised by this test if it fails
        /// </summary>
        public AlertLevel DefaultFailureLevel { get; set; }

        /// <summary>
        /// The name of the team that owns this test
        /// </summary>
        public string OwningTeamName { get; set; }

        public static string FormatDateTimeRecency(DateTimeOffset? time)
        {
            if (!time.HasValue)
            {
                return "Never";
            }

            TimeSpan recency = DateTimeOffset.UtcNow - time.Value;
            if (recency.TotalDays > 7)
            {
                return string.Format("{0:F1} days ago", recency.TotalDays);
            }
            else if (recency.TotalHours > 3)
            {
                return string.Format("{0:F1} hours ago", recency.TotalHours);
            }
            else if (recency.TotalMinutes > 3)
            {
                return string.Format("{0:F1} minutes ago", recency.TotalMinutes);
            }
            else
            {
                return string.Format("{0:F1} seconds ago", recency.TotalSeconds);
            }
        }

        public string FormatIncidentDuration()
        {
            if (!MostRecentFailureBegin.HasValue || !MostRecentFailureEnd.HasValue)
            {
                return "N/A";
            }

            TimeSpan incidentDuration = MostRecentFailureEnd.Value - MostRecentFailureBegin.Value;
            if (incidentDuration.TotalDays > 7)
            {
                return string.Format("{0:F1} days", incidentDuration.TotalDays);
            }
            else if (incidentDuration.TotalHours > 3)
            {
                return string.Format("{0:F1} hours", incidentDuration.TotalHours);
            }
            else if (incidentDuration.TotalMinutes > 3)
            {
                return string.Format("{0:F1} minutes", incidentDuration.TotalMinutes);
            }
            else
            {
                return string.Format("{0:F1} seconds", incidentDuration.TotalSeconds);
            }
        }

        public static string FormatAlertLevel(AlertLevel level)
        {
            switch (level)
            {
                case AlertLevel.NoAlert:
                    return "No alert";
                case AlertLevel.Mute:
                    return "Muted";
                case AlertLevel.Notify:
                    return "Notify";
                case AlertLevel.Alert:
                    return "Alert";
            }

            return "UNKNOWN";
        }

        /// <summary>
        /// Returns true if this incident was active in the last 15 minutes
        /// </summary>
        /// <returns></returns>
        public bool IsIncidentCurrent
        {
            get
            {
                return MostRecentFailureEnd.HasValue &&
                    MostRecentFailureEnd.Value > (DateTimeOffset.UtcNow - AlertEventProcessor.ALERT_CORRELATION_WINDOW);
            }
        }
    }
}