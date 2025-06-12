using System.IO;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Defines an interface for converting / marshaling / serializing a specific type of object to or from a byte array representation.
    /// </summary>
    /// <typeparam name="T">The type of object which this converter can convert</typeparam>
    public interface IByteConverter<T> where T : class
    {
        /// <summary>
        /// Encodes the input object and returns it as a newly created byte array.
        /// This method signature should be avoided if possible to reduce the number of allocations done in your code
        /// </summary>
        /// <param name="input">The object to encode</param>
        /// <returns>An array containing the serialized object</returns>
        byte[] Encode(T input);

        /// <summary>
        /// Encodes the input object and writes the output to a target stream.
        /// The stream is NOT closed afterwards.
        /// </summary>
        /// <param name="input">The object to encode</param>
        /// <param name="target">A stream to write the serialized object to</param>
        /// <returns>The number of bytes written to the stream</returns>
        int Encode(T input, Stream target);

        /// <summary>
        /// Decodes a single object from the given byte buffer
        /// </summary>
        /// <param name="input">The buffer to read from</param>
        /// <param name="offset">The offset to use when reading the buffer</param>
        /// <param name="length">The size of the serialized object</param>
        /// <returns>The deserialized object</returns>
        T Decode(byte[] input, int offset, int length);

        /// <summary>
        /// Decodes a single object from the given stream.
        /// The stream is NOT closed afterwards
        /// </summary>
        /// <param name="input">The stream to read from</param>
        /// <param name="length">The size of the serialized object</param>
        /// <returns>The deserialized object</returns>
        T Decode(Stream input, int length);
    }
}
