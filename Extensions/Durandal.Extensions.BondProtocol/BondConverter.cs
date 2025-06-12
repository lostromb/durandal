using System;
using System.IO;
using Bond;
using Bond.IO.Safe;
using Bond.Protocols;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using System.Text;
using Durandal.Common.Utils;
using Durandal.Common.Collections;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;

namespace Durandal.Extensions.BondProtocol
{
    /// <summary>
    /// Provided static methods to serialize and deserialize Bond-compliant objects
    /// </summary>
    public static class BondConverter
    {
        private static readonly FastConcurrentDictionary<Type, Serializer<CompactBinaryWriter<MemoryStreamOutputBuffer>>> _serializers
            = new FastConcurrentDictionary<Type, Serializer<CompactBinaryWriter<MemoryStreamOutputBuffer>>>();
        private static readonly FastConcurrentDictionary<Type, Deserializer<ModifiedCompactBinaryReader<InputBuffer>>> _deserializers
            = new FastConcurrentDictionary<Type, Deserializer<ModifiedCompactBinaryReader<InputBuffer>>>();

        /// <summary>
        /// Serializes a bond object into a blob using CompactBinary v1 protocol
        /// </summary>
        /// <typeparam name="T">The type of bond object to be serialized</typeparam>
        /// <param name="bondObject">The object to be serialized</param>
        /// <param name="logger">A logger for writing errors</param>
        /// <returns>A byte array containing the serialized object</returns>
        public static byte[] SerializeBond<T>(T bondObject, ILogger logger = null)
        {
            if (bondObject == null)
            {
                if (logger != null)
                {
                    logger.Log("Null object passed to " + nameof(SerializeBond) + " method; nothing to do!", LogLevel.Err);
                }

                return BinaryHelpers.EMPTY_BYTE_ARRAY;
            }

            using (MemoryStreamOutputBuffer outputBuffer = new MemoryStreamOutputBuffer())
            {
                try
                {
                    var writer = new CompactBinaryWriter<MemoryStreamOutputBuffer>(outputBuffer);
                    Serializer<CompactBinaryWriter<MemoryStreamOutputBuffer>> serializer = GetPooledSerializer<T>();
                    serializer.Serialize(bondObject, writer);
                    return outputBuffer.ToArray();
                }
                catch (NullReferenceException)
                {
                    if (logger != null)
                    {
                        logger.Log("NullReference exception in " + nameof(SerializeBond) + " method: This usually means that required fields in your schema are null", LogLevel.Err);

                        string whichFieldIsNull = WhichFieldIsThrowingNullExceptions(bondObject);
                        if (!string.IsNullOrEmpty(whichFieldIsNull))
                        {
                            logger.Log("The field in question appears to be " + whichFieldIsNull, LogLevel.Err);
                        }
                    }

                    return BinaryHelpers.EMPTY_BYTE_ARRAY;
                }
                catch (Exception e)
                {
                    logger?.Log(e, LogLevel.Err);
                    return BinaryHelpers.EMPTY_BYTE_ARRAY;
                }
            }
        }

        /// <summary>
        /// Serializes a bond object into a blob using CompactBinary v1 protocol
        /// </summary>
        /// <typeparam name="T">The type of bond object to be serialized</typeparam>
        /// <param name="bondObject">The object to be serialized</param>
        /// <param name="logger">A logger for writing errors</param>
        /// <returns>A pooled byte buffer containing the serialized object</returns>
        public static PooledBuffer<byte> SerializeBondPooled<T>(T bondObject, ILogger logger = null)
        {
            if (bondObject == null)
            {
                if (logger != null)
                {
                    logger.Log("Null object passed to " + nameof(SerializeBond) + " method; nothing to do!", LogLevel.Err);
                }

                return BufferPool<byte>.Rent(0);
            }

            using (MemoryStreamOutputBuffer outputBuffer = new MemoryStreamOutputBuffer())
            {
                try
                {
                    var writer = new CompactBinaryWriter<MemoryStreamOutputBuffer>(outputBuffer);
                    Serializer<CompactBinaryWriter<MemoryStreamOutputBuffer>> serializer = GetPooledSerializer<T>();
                    serializer.Serialize(bondObject, writer);
                    return outputBuffer.ToPooledBuffer();
                }
                catch (NullReferenceException)
                {
                    if (logger != null)
                    {
                        logger.Log("NullReference exception in " + nameof(SerializeBond) + " method: This usually means that required fields in your schema are null", LogLevel.Err);

                        string whichFieldIsNull = WhichFieldIsThrowingNullExceptions(bondObject);
                        if (!string.IsNullOrEmpty(whichFieldIsNull))
                        {
                            logger.Log("The field in question appears to be " + whichFieldIsNull, LogLevel.Err);
                        }
                    }

                    return BufferPool<byte>.Rent(0);
                }
                catch (Exception e)
                {
                    logger?.Log(e, LogLevel.Err);
                    return BufferPool<byte>.Rent(0);
                }
            }
        }

        /// <summary>
        /// Deserializes a bond object from a (CompactBinary) byte blob.
        /// </summary>
        /// <typeparam name="T">The type of object to be deserialized</typeparam>
        /// <param name="data">A byte array containing the input binary data</param>
        /// <param name="offset">The offset to begin reading binary data</param>
        /// <param name="length">The amount of byte to read</param>
        /// <param name="resultContainer">The variable to store the output</param>
        /// <param name="logger">A logger for error messages</param>
        /// <returns>True if the deserialization succeeded</returns>
        public static bool DeserializeBond<T>(byte[] data, int offset, int length, out T resultContainer, ILogger logger = null)
        {
            return DeserializeBondInternal<T>(data, offset, length, out resultContainer, logger, false);
        }

        /// <summary>
        /// Deserializes a bond object from a (CompactBinary) byte buffer.
        /// </summary>
        /// <typeparam name="T">The type of object to be deserialized</typeparam>
        /// <param name="data">A pooled buffer containing the input binary data</param>
        /// <param name="offset">The offset to begin reading binary data</param>
        /// <param name="length">The amount of byte to read</param>
        /// <param name="resultContainer">The variable to store the output</param>
        /// <param name="logger">A logger for error messages</param>
        /// <returns>True if the deserialization succeeded</returns>
        public static bool DeserializeBond<T>(PooledBuffer<byte> data, int offset, int length, out T resultContainer, ILogger logger = null)
        {
            return DeserializeBondInternal<T>(data.Buffer, offset, length, out resultContainer, logger, true);
        }

        private static bool DeserializeBondInternal<T>(byte[] data, int offset, int length, out T resultContainer, ILogger logger, bool copyByteBlobs)
        {
            resultContainer = default(T);

            if (data.Length == 0)
            {
                if (logger != null)
                {
                    logger.Log("Empty data passed to " + nameof(DeserializeBond) + " method; nothing to do!", LogLevel.Err);
                }

                return false;
            }

            try
            {
                // Debug - dump the data we got
                //StringBuilder str = new StringBuilder();
                //str.Append("new byte[] {");
                //foreach (byte b in data)
                //{
                //    str.AppendFormat("0x{0:X2}, ", b);
                //}
                //str.Append("}");
                //logger.Log(str.ToString());

                InputBuffer buf = new InputBuffer(data, offset, length);
                var reader = new ModifiedCompactBinaryReader<InputBuffer>(buf, version: 1, copyByteBlobs: copyByteBlobs);
                Deserializer<ModifiedCompactBinaryReader<InputBuffer>> deserializer = GetPooledDeserializer<T>();
                resultContainer = (T)deserializer.Deserialize(reader);
                return true;
            }
            catch (Exception e)
            {
                logger?.Log(e, LogLevel.Err);
                return false;
            }
        }

        /// <summary>
        /// Diagnostic method to determine why serialization failed in the case of a non-nullable field being set to null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bondObject">The bond object you are attempting to serialize</param>
        /// <returns>The name of the first null field, if there is a non-nullable field inside the object that is currently null</returns>
        public static string WhichFieldIsThrowingNullExceptions<T>(T bondObject)
        {
            if (bondObject == null)
            {
                return "Entire bond object is null";
            }

            NullRefDiagnosingProtocolWriter diagnosisWriter = new NullRefDiagnosingProtocolWriter(NullLogger.Singleton);

            try
            {
                Serialize.To(diagnosisWriter, bondObject);
                return string.Empty;
            }
            catch (IOException e)
            {
                return e.Message;
            }
            catch (Exception)
            {
                return "after " + diagnosisWriter.MostRecentField;
            }
        }

        /// <summary>
        /// As a way of "JITting", this method will ensure that pooled serializers / deserializers exist for the specified bond type before actual serialization takes place.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void PrecacheSerializers<T>()
        {
            GetPooledSerializer<T>();
            GetPooledDeserializer<T>();
        }

        private static Serializer<CompactBinaryWriter<MemoryStreamOutputBuffer>> GetPooledSerializer<T>()
        {
            Type keyType = typeof(T);
            Serializer<CompactBinaryWriter<MemoryStreamOutputBuffer>> returnVal;
            _serializers.TryGetValueOrSet(keyType, out returnVal, () => new Serializer<CompactBinaryWriter<MemoryStreamOutputBuffer>>(keyType));
            return returnVal;
        }

        private static Deserializer<ModifiedCompactBinaryReader<InputBuffer>> GetPooledDeserializer<T>()
        {
            Type keyType = typeof(T);
            Deserializer<ModifiedCompactBinaryReader<InputBuffer>> returnVal;
            _deserializers.TryGetValueOrSet(keyType, out returnVal, () => new Deserializer<ModifiedCompactBinaryReader<InputBuffer>>(keyType));
            return returnVal;
        }
    }
}
