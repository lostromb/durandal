namespace Durandal.Common.Logger
{
    using Durandal.API;
    using Durandal.Common.Utils;
    using Instrumentation;
    using System;
    using System.Text;

    public class LogEvent : IComparable<LogEvent>, IEquatable<LogEvent>
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Message { get; set; }
        public string Component { get; set; }
        public LogLevel Level { get; set; }
        public Guid? TraceId { get; set; }
        public DataPrivacyClassification PrivacyClassification { get; set; }

        public LogEvent(string component, string message, LogLevel level, DateTimeOffset utcTimestamp, Guid? traceId = null, DataPrivacyClassification privacyClassification = DataPrivacyClassification.Unknown)
        {
            Message = message;
            Component = component;
            Level = level;
            Timestamp = utcTimestamp;
            TraceId = traceId;
            PrivacyClassification = privacyClassification;
        }

        //public LogEvent(string component, string message, LogLevel level, string traceId = null)
        //{
        //    Message = message;
        //    Component = component;
        //    Level = level;
        //    Timestamp = DateTimeOffset.Now;
        //    TraceId = traceId;
        //}

        public string ToDetailedString()
        {
            // FIXME these don't account for privacy class
            return string.Format("[{0:yyyy-MM-ddTHH:mm:ss.fffff}] [{1}] [{2}:{3}]  {4}",
                Timestamp,
                CommonInstrumentation.FormatTraceId(TraceId),
                Level.ToChar(),
                Component,
                Message);
        }

        public void ToDetailedString(StringBuilder buffer)
        {
            // Write log values piecewise for performance. This correlates with the format string
            // "[{Timestamp:yyyy-MM-ddTHH:mm:ss.fffff}] [{TraceId:N}] [{Level}:{Component}] {Message}"
            // FIXME these don't account for privacy class
            buffer.Append("[");
            StringUtils.FormatDateTime_ISO8601WithMicroseconds(Timestamp, buffer);
            buffer.Append("] [");
            CommonInstrumentation.FormatTraceId(TraceId.GetValueOrDefault(Guid.Empty), buffer);
            buffer.Append("] [");
            buffer.Append(Level.ToChar());
            buffer.Append(':');
            buffer.Append(Component);
            buffer.Append("] ");
            buffer.Append(Message);
        }

        public string ToShortStringLocalTime()
        {
            if (TraceId.HasValue)
            {
                return string.Format("[{0}] [{1:HH:mm:ss}] [{2}:{3}] {4}",
                    CommonInstrumentation.GetFirst3DigitsOfTraceId(TraceId.Value),
                    Timestamp.ToLocalTime(),
                    Level.ToChar(),
                    Component,
                    Message);
            }
            else
            {
                return string.Format("[{0:HH:mm:ss}] [{1}:{2}] {3}",
                    Timestamp.ToLocalTime(),
                    Level.ToChar(),
                    Component,
                    Message);
            }
        }

        public void ToShortStringLocalTime(StringBuilder buffer)
        {
            // Write log values piecewise for performance. This correlates with the format string
            // "[{TraceId:D3}] ?[{Timestamp:HH:mm:ss}] [{Level}:{Component}] {Message}"
            if (TraceId.HasValue)
            {
                buffer.Append("[");
                CommonInstrumentation.GetFirst3DigitsOfTraceId(TraceId.Value, buffer);
                buffer.Append("] [");
            }
            else
            {
                buffer.Append("[");
            }

            StringUtils.FormatTime_ISO8601(Timestamp.ToLocalTime(), buffer);
            buffer.Append("] [");
            buffer.Append(Level.ToChar());
            buffer.Append(':');
            buffer.Append(Component);
            buffer.Append("] ");
            buffer.Append(Message);
        }

        public string ToShortStringHighPrecisionTime()
        {
            if (TraceId.HasValue)
            {
                return string.Format("[{0}] [{1:HH:mm:ss.fffff}] [{2}:{3}] {4}",
                    CommonInstrumentation.GetFirst3DigitsOfTraceId(TraceId.Value),
                    Timestamp.ToLocalTime(),
                    Level.ToChar(),
                    Component,
                    Message);
            }
            else
            {
                return string.Format("[{0:HH:mm:ss.fffff}] [{1}:{2}] {3}",
                    Timestamp.ToLocalTime(),
                    Level.ToChar(),
                    Component,
                    Message);
            }
        }

        public override string ToString()
        {
            return ToShortStringLocalTime();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null) || GetType() != obj.GetType())
            {
                return false;
            }

            LogEvent other = (LogEvent)obj;

            return Equals(other);
        }

        public bool Equals(LogEvent other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return other.Timestamp == Timestamp &&
                other.Level == Level &&
                (Message == null ? other.Message == null : Message.Equals(other.Message)) &&
                (Component == null ? other.Component == null : Component.Equals(other.Component)) &&
                (TraceId == null ? other.TraceId == null : TraceId.Equals(other.TraceId)) &&
                PrivacyClassification == other.PrivacyClassification;
        }

        public override int GetHashCode()
        {
            return Timestamp.GetHashCode() +
                (Message != null ? Message.GetHashCode() : 0) +
                Level.GetHashCode() +
                (Component != null ? Component.GetHashCode() : 0) +
                (TraceId != null ? TraceId.GetHashCode() : 0) +
                PrivacyClassification.GetHashCode();
        }

        public int CompareTo(LogEvent o)
        {
            if (o == null)
            {
                return -1;
            }
            
            // Sort ascending by default
            return Timestamp.CompareTo(o.Timestamp);
        }

        public static bool operator ==(LogEvent left, LogEvent right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(LogEvent left, LogEvent right)
        {
            return !(left == right);
        }

        public static bool operator <(LogEvent left, LogEvent right)
        {
            return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
        }

        public static bool operator <=(LogEvent left, LogEvent right)
        {
            return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(LogEvent left, LogEvent right)
        {
            return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(LogEvent left, LogEvent right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
        }
    }
}
