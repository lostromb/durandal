namespace Durandal.Extensions.Compression.Brotli
{
    using System;

    /// <summary>
    /// An exception that is thrown during Brotli stream operations.
    /// </summary>
    public class BrotliException : Exception
    {
        /// <summary>
        /// Constructs a new instance of <see cref="BrotliException"/>.
        /// </summary>
        public BrotliException()
            : base()
        {
        }

        /// <summary>
        /// Constructs a new instance of <see cref="BrotliException"/> with a given message.
        /// </summary>
        /// <param name="message">The error message.</param>
        public BrotliException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs a new instance of <see cref="BrotliException"/> with a given message and inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public BrotliException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
