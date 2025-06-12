using System;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// Represents the result of a tentative retrieval operation (such as TryDequeue(), or a fetch from a cache),
    /// which can possibly not return a value. This exists mostly because common TryGet___() patterns use out parameters for the result,
    /// which we can't use in async methods.
    /// </summary>
    /// <typeparam name="T">The type of result being fetched.</typeparam>
    public struct RetrieveResult<T> : IEquatable<RetrieveResult<T>>
    {
        /// <summary>
        /// Whether retrieval was a success.
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// The value that was retrieved, if successful.
        /// </summary>
        public T Result { get; private set; }

        /// <summary>
        /// The amount of time, in milliseconds, that the retrieval attempt took.
        /// </summary>
        public double LatencyMs { get; private set; }

        /// <summary>
        /// Creates a success result
        /// </summary>
        /// <param name="value"></param>
        public RetrieveResult(T value)
        {
            Success = true;
            Result = value;
            LatencyMs = 0;
        }

        /// <summary>
        /// Creates a success result with latency
        /// </summary>
        /// <param name="value"></param>
        /// <param name="latencyMs"></param>
        public RetrieveResult(T value, double latencyMs)
        {
            Success = true;
            Result = value;
            LatencyMs = latencyMs;
        }

        /// <summary>
        /// Creates a retrieval result manually
        /// </summary>
        /// <param name="value"></param>
        /// <param name="latencyMs"></param>
        /// <param name="success"></param>
        public RetrieveResult(T value, double latencyMs, bool success)
        {
            Success = success;
            Result = value;
            LatencyMs = latencyMs;
        }

        public override string ToString()
        {
            return string.Format("Success={0} Latency={1} Value={2}", Success, LatencyMs, Result);
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() == this.GetType())
            {
                return false;
            }

            RetrieveResult<T> other = (RetrieveResult<T>)obj;
            return (ReferenceEquals(Result, null) && ReferenceEquals(other.Result, null)) ||
                (Result.Equals(other.Result));
        }

        public bool Equals(RetrieveResult<T> other)
        {
            return (ReferenceEquals(Result, null) && ReferenceEquals(other.Result, null)) ||
                (Result.Equals(other.Result));
        }

        public override int GetHashCode()
        {
            if (ReferenceEquals(Result, null))
            {
                return 0;
            }

            return Result.GetHashCode();
        }

        public static bool operator ==(RetrieveResult<T> left, RetrieveResult<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RetrieveResult<T> left, RetrieveResult<T> right)
        {
            return !(left == right);
        }
    }
}
