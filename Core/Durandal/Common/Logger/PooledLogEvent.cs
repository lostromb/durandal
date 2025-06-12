namespace Durandal.Common.Logger
{
    using Durandal.API;
    using Durandal.Common.Cache;
    using Durandal.Common.Utils;
    using System;
    using System.Threading;

    /// <summary>
    /// An advanced internal logging class which contains a single log message, but rather than
    /// storing it as a string, it is stored as a <see cref="PooledStringBuilder"/>. If used
    /// carefully, this can greatly reduce string allocations when creating and writing
    /// log messages in many scenarios. However, it comes at the cost of requiring careful management
    /// of reference counts, disposability, and non-mutability.
    /// <para>
    /// The way it typically works is this:
    /// - A format string comes into a logger implementation
    /// - A PooledLogEvent is created with a handle to a pooled string builder and a reference count of 1.
    /// - The formatted string is written to that stringbuilder's buffer
    /// - If the event needs to go to multiple destinations, the reference count gets incremented by one for each split
    /// - Each consumer of the log event reads from the stringbuilder buffer and then decrements reference count
    /// - The last user of the event disposes of the pooled resource when refcount == 0
    /// </para>
    /// It is VERY IMPORTANT that the contents of the pooled string builder are not mutated by
    /// any logger implementation after the event is first created.
    /// </summary>
    public class PooledLogEvent : IDisposable, IComparable<PooledLogEvent>, IEquatable<PooledLogEvent>
    {
        /// <summary>
        /// The precise UTC timestamp of the log message
        /// </summary>
        public DateTimeOffset Timestamp { get; private set; }

        /// <summary>
        /// A <see cref="PooledStringBuilder"/> containing the logged message.
        /// Guaranteed non-null.
        /// <b>THE LOG EVENT OWNS THIS BUILDER. DO NOT MUTATE THE BUILDER'S CONTENTS.
        /// DO NOT DISPOSE OF IT MANUALLY.</b>
        /// </summary>
        public PooledStringBuilder MessageBuffer { get; private set;  }

        /// <summary>
        /// The name of the component which originated this message.
        /// </summary>
        public string Component { get; private set; }

        /// <summary>
        /// The log level of the message.
        /// </summary>
        public LogLevel Level { get; private set; }

        /// <summary>
        /// The trace ID associated with the message, or null.
        /// </summary>
        public Guid? TraceId { get; private set; }

        /// <summary>
        /// The privacy classification of the message.
        /// </summary>
        public DataPrivacyClassification PrivacyClassification { get; set; }

        private int _referenceCount = 0;

        // cached string value, to avoid reallocations if this event was originally created from a string,
        // or if ToString() is called multiple times
        private string _stringVal = null;

        // In an interesting turn of optimization, PooledLogEvent itself is pooled in addition to the StringBuilders that
        // are a part of each event.
        // After the overall event is disposed, its instance can get reused later on without reallocation.
        private static DynamicLockFreeCache<PooledLogEvent> POOLED_EVENTS = new DynamicLockFreeCache<PooledLogEvent>(4096);
#if DEBUG
        private int _inUse = 0;
#endif

        public static PooledLogEvent Create(
            string component,
            PooledStringBuilder messageBuffer,
            LogLevel level,
            DateTimeOffset utcTimestamp,
            Guid? traceId = null,
            DataPrivacyClassification privacyClassification = DataPrivacyClassification.Unknown)
        {
            PooledLogEvent returnVal = POOLED_EVENTS.TryDequeue();
            if (returnVal == null)
            {
                returnVal = new PooledLogEvent(component, messageBuffer, level, utcTimestamp, traceId, privacyClassification);
            }
            else
            {
                returnVal.Reset(component, messageBuffer, level, utcTimestamp, traceId, privacyClassification);
            }

#if DEBUG
            if (Interlocked.CompareExchange(ref returnVal._inUse, 1, 0) != 0)
            {
                throw new ObjectDisposedException("Invalid state: Reallocating a pooled log event that is already marked as \"in use\"");
            }
#endif
            return returnVal;
        }

        public static PooledLogEvent Create(
            string component,
            string message,
            LogLevel level,
            DateTimeOffset utcTimestamp,
            Guid? traceId = null,
            DataPrivacyClassification privacyClassification = DataPrivacyClassification.Unknown)
        {
            PooledStringBuilder messageBuffer = StringBuilderPool.Rent();
            messageBuffer.Builder.Append(message.AssertNonNull(nameof(message)));

            PooledLogEvent returnVal = POOLED_EVENTS.TryDequeue();
            if (returnVal == null)
            {
                returnVal = new PooledLogEvent(component, messageBuffer, level, utcTimestamp, traceId, privacyClassification);
            }
            else
            {
                returnVal.Reset(component, messageBuffer, level, utcTimestamp, traceId, privacyClassification);
            }

#if DEBUG
            if (Interlocked.CompareExchange(ref returnVal._inUse, 1, 0) != 0)
            {
                throw new ObjectDisposedException("Invalid state: Reallocating a pooled log event that is already marked as \"in use\"");
            }
#endif

            return returnVal;
        }

        /// <summary>
        /// Constructs a new <see cref="PooledLogEvent"/> from a message buffer provided by the caller.
        /// This object will take ownership of the PooledStringBuilder.
        /// </summary>
        /// <param name="component">The component logging the messsage.</param>
        /// <param name="messageBuffer">A buffer containing the message. This object will take ownership of the buffer.</param>
        /// <param name="level">The log level.</param>
        /// <param name="utcTimestamp">The precise time of the log message.</param>
        /// <param name="traceId">The trace ID of the message, or null.</param>
        /// <param name="privacyClassification">The privacy level of the message.</param>
        private PooledLogEvent(
            string component,
            PooledStringBuilder messageBuffer,
            LogLevel level,
            DateTimeOffset utcTimestamp,
            Guid? traceId,
            DataPrivacyClassification privacyClassification)
        {
            MessageBuffer = messageBuffer.AssertNonNull(nameof(MessageBuffer));
            Component = component.AssertNonNull(nameof(component));
            Level = level;
            Timestamp = utcTimestamp;
            TraceId = traceId;
            PrivacyClassification = privacyClassification;
            _stringVal = null;
            _referenceCount = 1;
        }

        /// <summary>
        /// Reallocates this log event for a different message
        /// </summary>
        /// <param name="component"></param>
        /// <param name="messageBuffer"></param>
        /// <param name="level"></param>
        /// <param name="utcTimestamp"></param>
        /// <param name="traceId"></param>
        /// <param name="privacyClassification"></param>
        private void Reset(string component,
            PooledStringBuilder messageBuffer,
            LogLevel level,
            DateTimeOffset utcTimestamp,
            Guid? traceId,
            DataPrivacyClassification privacyClassification)
        {
            MessageBuffer = messageBuffer.AssertNonNull(nameof(MessageBuffer));
            Component = component.AssertNonNull(nameof(component));
            Level = level;
            Timestamp = utcTimestamp;
            TraceId = traceId;
            PrivacyClassification = privacyClassification;
            _stringVal = null;
            _referenceCount = 1;
        }

        /// <summary>
        /// Converts a log event into a pooled log event. The conversion is a bit wasteful
        /// so this is usually discouraged.
        /// </summary>
        /// <param name="fromEvent">The event to convert from.</param>
        /// <returns>A new pooled log event with the same properties.</returns>
        public static PooledLogEvent FromLogEvent(LogEvent fromEvent)
        {
            fromEvent.AssertNonNull(nameof(fromEvent));
            return Create(
                fromEvent.Component.AssertNonNull(nameof(fromEvent.Component)),
                fromEvent.Message,
                fromEvent.Level,
                fromEvent.Timestamp,
                fromEvent.TraceId,
                fromEvent.PrivacyClassification);
        }

        /// <summary>
        /// Converts this pooled log event to a static log event. The benefits of pooling are lost,
        /// but you can safely serialize or pass the message around without worrying about ownership.
        /// </summary>
        /// <returns>A newly created converted log event.</returns>
        public LogEvent ToLogEvent()
        {
            return new LogEvent(
                Component,
                this.ToString(),
                Level,
                Timestamp,
                TraceId,
                PrivacyClassification);
        }

        /// <summary>
        /// Increments the reference count of the number of loggers or processors that are using
        /// this event. We assume each processor will Dispose() and thus decrease the reference
        /// count by one when it's finished, and the count has to be correct to make sure that
        /// the last one to dispose of the reference will dispose of the buffer.
        /// For advanced scenarios; usually when you need to send this same event to multiple processors.
        /// </summary>
        public void IncrementReferenceCount()
        {
            Interlocked.Increment(ref _referenceCount);
        }

        /// <summary>
        /// Returns the value of the buffered log message, as a string.
        /// Doing so incurs an allocation which we are specifically trying to avoid,
        /// so try not to use this method ever.</summary>
        public override string ToString()
        {
            if (_stringVal == null)
            {
                _stringVal = MessageBuffer.Builder.ToString();
            }

            return _stringVal;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null) || GetType() != obj.GetType())
            {
                return false;
            }

            PooledLogEvent other = (PooledLogEvent)obj;

            return Equals(other);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Decrement(ref _referenceCount) == 0)
            {
                MessageBuffer?.Dispose();
                MessageBuffer = null;
#if DEBUG
                if (Interlocked.CompareExchange(ref _inUse, 0, 1) != 1)
                {
                    throw new ObjectDisposedException("Invalid state: Disposing of a pooled log event that is not marked as \"in use\"");
                }
#endif
                POOLED_EVENTS.TryEnqueue(this);
            }
        }

        /// <inheritdoc/>
        public bool Equals(PooledLogEvent other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return other.Timestamp == Timestamp &&
                other.Level == Level &&
                StringUtils.StringBuildersEqual(MessageBuffer.Builder, other.MessageBuffer.Builder, StringComparison.Ordinal) &&
                (Component == null ? other.Component == null : Component.Equals(other.Component)) &&
                (TraceId == null ? other.TraceId == null : TraceId.Equals(other.TraceId)) &&
                PrivacyClassification == other.PrivacyClassification;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Timestamp.GetHashCode() +
                (int)StringUtils.GetFNV1AHash(MessageBuffer.Builder) +
                Level.GetHashCode() +
                (Component != null ? Component.GetHashCode() : 0) +
                (TraceId != null ? TraceId.GetHashCode() : 0) +
                PrivacyClassification.GetHashCode();
        }

        /// <inheritdoc/>
        public int CompareTo(PooledLogEvent o)
        {
            if (o == null)
            {
                return -1;
            }

            // Sort ascending by default
            return Timestamp.CompareTo(o.Timestamp);
        }

        public static bool operator ==(PooledLogEvent left, PooledLogEvent right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(PooledLogEvent left, PooledLogEvent right)
        {
            return !(left == right);
        }

        public static bool operator <(PooledLogEvent left, PooledLogEvent right)
        {
            return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
        }

        public static bool operator <=(PooledLogEvent left, PooledLogEvent right)
        {
            return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(PooledLogEvent left, PooledLogEvent right)
        {
            return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(PooledLogEvent left, PooledLogEvent right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
        }
    }
}
