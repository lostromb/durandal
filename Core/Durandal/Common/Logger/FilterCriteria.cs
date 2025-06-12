namespace Durandal.Common.Logger
{
    using Durandal.API;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A class which specifies a set of filters to be applied to a set of log events
    /// </summary>
    public class FilterCriteria
    {
        public string ExactComponentName { get; set; }
        public LogLevel Level { get; set; }
        public string SearchTerm { get; set; }
        public ISet<string> AllowedComponentNames { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public Guid? TraceId { get; set; }
        public DataPrivacyClassification PrivacyClass { get; set; }

        public FilterCriteria()
        {
            ExactComponentName = null;
            Level = LogLevel.All;
            SearchTerm = null;
            AllowedComponentNames = null;
            TraceId = null;
            PrivacyClass = DataPrivacyClassification.All;
        }

        public bool PassesFilter(LogEvent e)
        {
            if (ExactComponentName != null && !e.Component.Equals(ExactComponentName))
            {
                return false;
            }
            if ((Level & e.Level) == 0)
            {
                return false;
            }
            if (StartTime.HasValue && e.Timestamp < StartTime.Value)
            {
                return false;
            }
            if (EndTime.HasValue && e.Timestamp > EndTime.Value)
            {
                return false;
            }
            if (AllowedComponentNames != null && !AllowedComponentNames.Contains(e.Component))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(SearchTerm) && !e.Message.Contains(SearchTerm) && !e.Component.Contains(SearchTerm))
            {
                return false;
            }
            if (TraceId.HasValue && (!e.TraceId.HasValue || !Guid.Equals(TraceId.Value, e.TraceId.Value)))
            {
                 return false;
            }
            if ((e.PrivacyClassification & ~PrivacyClass) != 0)
            {
                return false;
            }

            return true;
        }

        public static FilterCriteria ByTraceId(Guid traceId)
        {
            return new FilterCriteria()
            {
                TraceId = traceId
            };
        }
    }
}
