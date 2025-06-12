using Durandal.Common.MathExt;
using Durandal.Common.IO.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Tests.Common.IO;
using Durandal.Common.Test;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;
using Durandal.Common.Utils;

namespace Durandal.Tests.Common.IO.Json
{
    [TestClass]
    public class JsonTests
    {
        private class ObjectWithTimeSpan
        {
            [JsonConverter(typeof(JsonTimeSpanStringConverter))]
            public TimeSpan? SpanAsString { get; set; }

            [JsonConverter(typeof(JsonTimeSpanTicksConverter))]
            public TimeSpan? SpanAsTicks { get; set; }
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerNull()
        {
            RunTimeSpanTest(null);
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerTicks()
        {
            RunTimeSpanTest(TimeSpan.FromTicks(1999));
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerSeconds()
        {
            RunTimeSpanTest(TimeSpan.FromSeconds(53.12));
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerMinutes()
        {
            RunTimeSpanTest(TimeSpan.FromMinutes(53.12));
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerHours()
        {
            RunTimeSpanTest(TimeSpan.FromHours(11.11));
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerDays()
        {
            RunTimeSpanTest(TimeSpan.FromDays(11.11));
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerYears()
        {
            RunTimeSpanTest(TimeSpan.FromDays(4834.129512));
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerNegativeTicks()
        {
            RunTimeSpanTest(TimeSpan.FromTicks(-1999));
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerNegativeSeconds()
        {
            RunTimeSpanTest(TimeSpan.FromSeconds(-53.12));
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerNegativeMinutes()
        {
            RunTimeSpanTest(TimeSpan.FromMinutes(-53.12));
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerNegativeHours()
        {
            RunTimeSpanTest(TimeSpan.FromHours(-11.11));
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerNegativeDays()
        {
            RunTimeSpanTest(TimeSpan.FromDays(-11.11));
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerNegativeYears()
        {
            RunTimeSpanTest(TimeSpan.FromDays(-4834.129512));
        }

        private static void RunTimeSpanTest(TimeSpan? testSpan)
        {
            ObjectWithTimeSpan originalObj = new ObjectWithTimeSpan()
            {
                SpanAsString = testSpan,
                SpanAsTicks = testSpan
            };

            string json = JsonConvert.SerializeObject(originalObj);
            ObjectWithTimeSpan newObj = JsonConvert.DeserializeObject<ObjectWithTimeSpan>(json);
            Assert.IsNotNull(newObj);
            if (testSpan.HasValue)
            {
                Assert.IsTrue(newObj.SpanAsString.HasValue);
                Assert.IsTrue(newObj.SpanAsTicks.HasValue);
                Assert.AreEqual(testSpan, newObj.SpanAsString.Value);
                Assert.AreEqual(testSpan, newObj.SpanAsTicks.Value);
            }
            else
            {
                Assert.IsFalse(newObj.SpanAsString.HasValue);
                Assert.IsFalse(newObj.SpanAsTicks.HasValue);
            }
        }

        [TestMethod]
        public void TestJsonTimeSpanSerializerInterpretIntegersAsTicks()
        {
            string json = "{ \"SpanAsString\": 123456 }";
            ObjectWithTimeSpan newObj = JsonConvert.DeserializeObject<ObjectWithTimeSpan>(json);
            Assert.IsNotNull(newObj);
            Assert.AreEqual(TimeSpan.FromTicks(123456), newObj.SpanAsString.Value);
        }

        private class ObjectWithByteArrays
        {
            [JsonConverter(typeof(JsonByteArrayConverter))]
            public byte[] Array { get; set; }

            [JsonConverter(typeof(JsonByteArrayConverter))]
            public ArraySegment<byte> Segment { get; set; }
        }

        [TestMethod]
        public void TestJsonByteArrayConverterNulls()
        {
            ObjectWithByteArrays originalObj = new ObjectWithByteArrays()
            {
                Array = null,
                Segment = default(ArraySegment<byte>)
            };

            string json = JsonConvert.SerializeObject(originalObj);
            ObjectWithByteArrays newObj = JsonConvert.DeserializeObject<ObjectWithByteArrays>(json);
            Assert.IsNotNull(newObj);
            Assert.IsNull(newObj.Array);
            Assert.IsNull(newObj.Segment.Array);
        }

        [TestMethod]
        public void TestJsonByteArrayConverterEmptyProperties()
        {
            string json = "{}";
            ObjectWithByteArrays newObj = JsonConvert.DeserializeObject<ObjectWithByteArrays>(json);
            Assert.IsNotNull(newObj);
            Assert.IsNull(newObj.Array);
            Assert.IsNull(newObj.Segment.Array);
        }

        [TestMethod]
        public void TestJsonByteArrayConverterArrayValues()
        {
            byte[] randomArray = new byte[100];
            new FastRandom().NextBytes(randomArray);
            ArraySegment<byte> randomArraySegment = new ArraySegment<byte>(randomArray);
            ObjectWithByteArrays originalObj = new ObjectWithByteArrays()
            {
                Array = randomArray,
                Segment = randomArraySegment
            };

            string json = JsonConvert.SerializeObject(originalObj);
            ObjectWithByteArrays newObj = JsonConvert.DeserializeObject<ObjectWithByteArrays>(json);
            Assert.IsNotNull(newObj);
            Assert.IsNotNull(newObj.Array);
            Assert.IsNotNull(newObj.Segment.Array);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(randomArray, newObj.Array));
            Assert.IsTrue(ArrayExtensions.ArrayEquals(randomArraySegment, newObj.Segment));
        }

        [TestMethod]
        public void TestJsonByteArrayConverterBackwardsCompatability()
        {
            byte[] expected = new byte[] { 1, 2, 3, 4 };
            string json = "{ \"Array\": [ 1, 2, 3, 4 ] }";
            ObjectWithByteArrays newObj = JsonConvert.DeserializeObject<ObjectWithByteArrays>(json);
            Assert.IsNotNull(newObj);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(expected, newObj.Array));
        }

        private class ObjectWithNoBinary
        {
            [JsonConverter(typeof(NoBinaryJsonConverter))]
            public byte[] Junk { get; set; }
        }

        [TestMethod]
        public void TestJsonByteArrayConverterNoBinary()
        {
            byte[] randomArray = new byte[10000];
            new FastRandom().NextBytes(randomArray);
            ObjectWithNoBinary originalObj = new ObjectWithNoBinary()
            {
                Junk = randomArray
            };

            string json = JsonConvert.SerializeObject(originalObj);
            Assert.IsTrue(json.Length < 15);
        }

        private class ObjectWithEpochTimes
        {
            [JsonConverter(typeof(JsonEpochTimeConverter))]
            public DateTimeOffset DTO { get; set; }

            [JsonConverter(typeof(JsonEpochTimeConverter))]
            public DateTime DT { get; set; }

            [JsonConverter(typeof(JsonEpochTimeConverter))]
            public DateTimeOffset? NullDTO { get; set; }

            [JsonConverter(typeof(JsonEpochTimeConverter))]
            public DateTime? NullDT { get; set; }
        }

        [TestMethod]
        public void TestJsonEpochTimeConverter()
        {
            DateTimeOffset time = new DateTimeOffset(2011, 10, 22, 15, 55, 34, TimeSpan.Zero);
            ObjectWithEpochTimes originalObj = new ObjectWithEpochTimes()
            {
                DT = time.UtcDateTime,
                DTO = time,
                NullDT = time.UtcDateTime,
                NullDTO = time
            };

            string json = JsonConvert.SerializeObject(originalObj);
            ObjectWithEpochTimes newObj = JsonConvert.DeserializeObject<ObjectWithEpochTimes>(json);
            Assert.IsNotNull(newObj);
            Assert.IsTrue(newObj.NullDT.HasValue);
            Assert.IsTrue(newObj.NullDTO.HasValue);
            Assert.AreEqual(time, newObj.DTO);
            Assert.AreEqual(time.UtcDateTime, newObj.DT);
            Assert.AreEqual(time, newObj.NullDTO.Value);
            Assert.AreEqual(time.UtcDateTime, newObj.NullDT.Value);
        }

        [TestMethod]
        public void TestJsonEpochTimeConverterFromIntegerValues()
        {
            DateTimeOffset time = new DateTimeOffset(2011, 10, 22, 15, 55, 34, TimeSpan.Zero);
            long expectedEpochTime = (time - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)).Ticks / 10000000L;

            string json = "{ \"DT\": " + expectedEpochTime + ", \"DTO\": " + expectedEpochTime + ", \"NullDT\": " + expectedEpochTime + ", \"NullDTO\": " + expectedEpochTime + "}";
            ObjectWithEpochTimes newObj = JsonConvert.DeserializeObject<ObjectWithEpochTimes>(json);
            Assert.IsNotNull(newObj);
            Assert.IsTrue(newObj.NullDT.HasValue);
            Assert.IsTrue(newObj.NullDTO.HasValue);
            Assert.AreEqual(time, newObj.DTO);
            Assert.AreEqual(time.UtcDateTime, newObj.DT);
            Assert.AreEqual(time, newObj.NullDTO.Value);
            Assert.AreEqual(time.UtcDateTime, newObj.NullDT.Value);
        }

        [TestMethod]
        public void TestJsonByteArrayConverterCompatability()
        {
            JsonConverter converter = new JsonByteArrayConverter();
            string json = "{ \"A\": \"AQIDBA==\", \"B\": [ 1, 2, 3, 4 ], \"C\": \"AQIDBA==\", \"D\": [ 1, 2, 3, 4 ] }";
            FakeObjectSchema obj = JsonConvert.DeserializeObject<FakeObjectSchema>(json, converter);
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.A);
            Assert.IsNotNull(obj.B);
            Assert.IsNotNull(obj.C);
            Assert.IsNotNull(obj.D);
            Assert.AreEqual(4, obj.A.Length);
            Assert.AreEqual(4, obj.B.Length);
            Assert.AreEqual(4, obj.C.Count);
            Assert.AreEqual(4, obj.D.Count);

            json = "{ \"A\": null, \"B\": null, \"C\": null, \"D\": null }";
            obj = JsonConvert.DeserializeObject<FakeObjectSchema>(json, converter);
            Assert.IsNotNull(obj);
            Assert.IsNull(obj.A);
            Assert.IsNull(obj.B);
            //Assert.IsNull(obj.C);
            //Assert.IsNull(obj.D);

            json = "{ \"A\": [ ], \"B\": [], \"C\": [ ], \"D\": [] }";
            obj = JsonConvert.DeserializeObject<FakeObjectSchema>(json, converter);
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.A);
            Assert.IsNotNull(obj.B);
            Assert.IsNotNull(obj.C);
            Assert.IsNotNull(obj.D);
            Assert.AreEqual(0, obj.A.Length);
            Assert.AreEqual(0, obj.B.Length);
            Assert.AreEqual(0, obj.C.Count);
            Assert.AreEqual(0, obj.D.Count);

            json = "{ \"A\": [ 1 ], \"B\": [ 1 ], \"C\": [ 1 ], \"D\": [ 1 ] }";
            obj = JsonConvert.DeserializeObject<FakeObjectSchema>(json, converter);
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.A);
            Assert.IsNotNull(obj.B);
            Assert.IsNotNull(obj.C);
            Assert.IsNotNull(obj.D);
            Assert.AreEqual(1, obj.A.Length);
            Assert.AreEqual(1, obj.B.Length);
            Assert.AreEqual(1, obj.C.Count);
            Assert.AreEqual(1, obj.D.Count);

            json = "{ \"A\": \"AQIDBA==\", \"B\": \"AQIDBA==\", \"C\": \"AQIDBA==\", \"D\": \"AQIDBA==\" }";
            obj = JsonConvert.DeserializeObject<FakeObjectSchema>(json, converter);
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.A);
            Assert.IsNotNull(obj.B);
            Assert.IsNotNull(obj.C);
            Assert.IsNotNull(obj.D);
            Assert.AreEqual(4, obj.A.Length);
            Assert.AreEqual(4, obj.B.Length);
            Assert.AreEqual(4, obj.C.Count);
            Assert.AreEqual(4, obj.D.Count);
        }

        private class FakeObjectSchema
        {
            public FakeObjectSchema()
            {
                A = null;
                B = null;
            }

            public byte[] A;
            public byte[] B;
            public ArraySegment<byte> C;
            public ArraySegment<byte> D;
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryParseNullDictionary()
        {
            FakeDictionaryClass parsedObj = JsonConvert.DeserializeObject<FakeDictionaryClass>("{ \"Dictionary\": null, \"IDictionary\": null, \"ReadOnlyDictionary\": null }");
            Assert.IsNotNull(parsedObj);
            Assert.IsNull(parsedObj.Dictionary);
            Assert.IsNull(parsedObj.IDictionary);
            Assert.IsNull(parsedObj.ReadOnlyDictionary);
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryParseEmptyDictionary()
        {
            FakeDictionaryClass parsedObj = JsonConvert.DeserializeObject<FakeDictionaryClass>("{ \"Dictionary\": { }, \"IDictionary\": { }, \"ReadOnlyDictionary\": { } }");
            Assert.IsNotNull(parsedObj);
            Assert.IsNotNull(parsedObj.Dictionary);
            Assert.IsNotNull(parsedObj.IDictionary);
            Assert.IsNotNull(parsedObj.ReadOnlyDictionary);

            Assert.AreEqual(0, parsedObj.Dictionary.Count);
            Assert.AreEqual(0, parsedObj.IDictionary.Count);
            Assert.AreEqual(0, parsedObj.ReadOnlyDictionary.Count);
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryParseBasicDictionary()
        {
            FakeDictionaryClass parsedObj = JsonConvert.DeserializeObject<FakeDictionaryClass>("{ \"Dictionary\": { \"a\": 1, \"b\": 2 }, \"IDictionary\": { \"a\": 1, \"b\": 2 }, \"ReadOnlyDictionary\": { \"a\": 1, \"b\": 2 } }");
            Assert.IsNotNull(parsedObj);
            Assert.IsNotNull(parsedObj.Dictionary);
            Assert.IsNotNull(parsedObj.IDictionary);
            Assert.IsNotNull(parsedObj.ReadOnlyDictionary);

            Assert.IsTrue(parsedObj.Dictionary.ContainsKey("a"));
            Assert.IsTrue(parsedObj.Dictionary.ContainsKey("A"));
            Assert.AreEqual(1, parsedObj.Dictionary["a"]);
            Assert.IsTrue(parsedObj.Dictionary.ContainsKey("b"));
            Assert.IsTrue(parsedObj.Dictionary.ContainsKey("B"));
            Assert.AreEqual(2, parsedObj.Dictionary["b"]);

            Assert.IsTrue(parsedObj.IDictionary.ContainsKey("a"));
            Assert.IsTrue(parsedObj.IDictionary.ContainsKey("A"));
            Assert.AreEqual(1, parsedObj.IDictionary["a"]);
            Assert.IsTrue(parsedObj.IDictionary.ContainsKey("b"));
            Assert.IsTrue(parsedObj.IDictionary.ContainsKey("B"));
            Assert.AreEqual(2, parsedObj.IDictionary["b"]);

            Assert.IsTrue(parsedObj.ReadOnlyDictionary.ContainsKey("a"));
            Assert.IsTrue(parsedObj.ReadOnlyDictionary.ContainsKey("A"));
            Assert.AreEqual(1, parsedObj.ReadOnlyDictionary["a"]);
            Assert.IsTrue(parsedObj.ReadOnlyDictionary.ContainsKey("b"));
            Assert.IsTrue(parsedObj.ReadOnlyDictionary.ContainsKey("B"));
            Assert.AreEqual(2, parsedObj.ReadOnlyDictionary["b"]);
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryParseComplexObject()
        {
            FakeDictionaryClass parsedObj = JsonConvert.DeserializeObject<FakeDictionaryClass>("{\"Dictionary\":null,\"IDictionary\":null,\"ReadOnlyDictionary\":null,\"ComplexDictionary\":{\"test\":\"3.1.10.0\"}}");
            Assert.IsNotNull(parsedObj);
            Assert.IsNotNull(parsedObj.ComplexDictionary);
            Assert.IsTrue(parsedObj.ComplexDictionary.ContainsKey("test"));
            Assert.IsTrue(parsedObj.ComplexDictionary.ContainsKey("TEST"));
            Version complexValue = parsedObj.ComplexDictionary["test"];
            Assert.AreEqual(new Version(3, 1, 10, 0), complexValue);
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryThrowsDuplicateKeyException()
        {
            try
            {
                FakeDictionaryClass parsedObj = JsonConvert.DeserializeObject<FakeDictionaryClass>("{ \"Dictionary\": { \"a\": 1, \"A\": 2 }, \"IDictionary\": { \"a\": 1, \"A\": 2 }, \"ReadOnlyDictionary\": { \"a\": 1, \"A\": 2 } }");
                Assert.Fail("Expected a JsonException");
            }
            catch (JsonException) { }
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryUnexpectedStartObject()
        {
            try
            {
                FakeDictionaryClass parsedObj = JsonConvert.DeserializeObject<FakeDictionaryClass>("{ \"Dictionary\": [ ");
                Assert.Fail("Expected a JsonException");
            }
            catch (JsonException) { }
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryUnexpectedEOF1()
        {
            try
            {
                FakeDictionaryClass parsedObj = JsonConvert.DeserializeObject<FakeDictionaryClass>("{ \"Dictionary\": { ");
                Assert.Fail("Expected a JsonException");
            }
            catch (JsonException) { }
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryUnexpectedEOF2()
        {
            try
            {
                FakeDictionaryClass parsedObj = JsonConvert.DeserializeObject<FakeDictionaryClass>("{ \"Dictionary\": { \"a\": ");
                Assert.Fail("Expected a JsonException");
            }
            catch (JsonException) { }
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryUnexpectedEOF3()
        {
            try
            {
                FakeDictionaryClass parsedObj = JsonConvert.DeserializeObject<FakeDictionaryClass>("{ \"Dictionary\": { \"a\": 1, ");
                Assert.Fail("Expected a JsonException");
            }
            catch (JsonException) { }
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryWriteNullDictionary()
        {
            FakeDictionaryClass dict = new FakeDictionaryClass()
            {
                Dictionary = null,
                IDictionary = null,
                ReadOnlyDictionary = null,
            };

            string json = JsonConvert.SerializeObject(dict, Formatting.None);
            Assert.AreEqual("{\"Dictionary\":null,\"IDictionary\":null,\"ReadOnlyDictionary\":null,\"ComplexDictionary\":null}", json);
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryWriteEmptyDictionary()
        {
            FakeDictionaryClass dict = new FakeDictionaryClass()
            {
                Dictionary = new Dictionary<string, int>(),
                IDictionary = new Dictionary<string, int>(),
                ReadOnlyDictionary = new Dictionary<string, int>(),
            };

            string json = JsonConvert.SerializeObject(dict, Formatting.None);
            Assert.AreEqual("{\"Dictionary\":{},\"IDictionary\":{},\"ReadOnlyDictionary\":{},\"ComplexDictionary\":null}", json);
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryWriteBasicDictionary()
        {
            FakeDictionaryClass dict = new FakeDictionaryClass()
            {
                Dictionary = new Dictionary<string, int>()
                {
                    { "a", 1 },
                    { "b", 2 },
                },
                IDictionary = new Dictionary<string, int>()
                {
                    { "a", 1 },
                    { "b", 2 },
                },
                ReadOnlyDictionary = new Dictionary<string, int>()
                {
                    { "a", 1 },
                    { "b", 2 },
                },
            };

            string json = JsonConvert.SerializeObject(dict, Formatting.None);
            Assert.AreEqual("{\"Dictionary\":{\"a\":1,\"b\":2},\"IDictionary\":{\"a\":1,\"b\":2},\"ReadOnlyDictionary\":{\"a\":1,\"b\":2},\"ComplexDictionary\":null}", json);
        }

        [TestMethod]
        public void TestCaseInsensitiveDictionaryWriteComplexObject()
        {
            FakeDictionaryClass dict = new FakeDictionaryClass()
            {
                ComplexDictionary = new Dictionary<string, Version>()
                {
                    { "test", new Version(3, 1, 10) }
                }
            };

            string json = JsonConvert.SerializeObject(dict, Formatting.None);
            Assert.AreEqual("{\"Dictionary\":null,\"IDictionary\":null,\"ReadOnlyDictionary\":null,\"ComplexDictionary\":{\"test\":\"3.1.10\"}}", json);
        }

        public class FakeDictionaryClass
        {
            [JsonConverter(typeof(JsonCaseInsensitiveDictionaryConverter<int>))]
            public Dictionary<string, int> Dictionary { get; set; }

            [JsonConverter(typeof(JsonCaseInsensitiveDictionaryConverter<int>))]
            public IDictionary<string, int> IDictionary { get; set; }

            [JsonConverter(typeof(JsonCaseInsensitiveDictionaryConverter<int>))]
            public IReadOnlyDictionary<string, int> ReadOnlyDictionary { get; set; }

            [JsonConverter(typeof(JsonCaseInsensitiveDictionaryConverter<Version>))]
            public Dictionary<string, Version> ComplexDictionary { get; set; }
        }



        [TestMethod]
        public void TestJsonDirectSerializationStream()
        {
            byte[] data = new byte[100];
            new FastRandom().NextBytes(data);
            BufferClass buffer = new BufferClass()
            {
                Data = new MemoryStream(data),
                SomethingElse = "Yes",
            };

            JsonSerializer serializer = new JsonSerializer()
            {
            };

            string json;
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            using (TextWriter writer = new StringBuilderTextWriter(pooledSb.Builder))
            using (JsonCustomWriter customWriter = new JsonCustomWriter(writer))
            {
                serializer.Serialize(customWriter, buffer);
                json = pooledSb.Builder.ToString();
            }

            Console.WriteLine(json);

            using (TextReader reader = new StringReader(json))
            using (JsonCustomReader2 customReader = new JsonCustomReader2(reader))
            {
                BufferClass deserialized = serializer.Deserialize<BufferClass>(customReader);
            }
        }

        private class JsonCustomReader2 : JsonTextReader
        {
            public JsonCustomReader2(TextReader reader) : base(reader)
            {
                Reader = reader;
            }

            public override bool Read()
            {
                bool success = base.Read();
                return success;
            }

            public TextReader Reader { get; private set; }
        }

        private class JsonCustomWriter : JsonTextWriter
        {
            public JsonCustomWriter(TextWriter writer) : base(writer)
            {
                Writer = writer;
            }

            public TextWriter Writer { get; private set; }
        }

        public class BufferClass
        {
            [JsonConverter(typeof(DirectBufferConverter))]
            public Stream Data { get; set; }

            public string SomethingElse { get; set; }
        }

        public class DirectBufferConverter : JsonConverter
        {
            public DirectBufferConverter()
            {
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Stream);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }

                if (reader.TokenType == JsonToken.StartArray)
                {

                    Type textReaderType = typeof(JsonTextReader);
                    FieldInfo fieldInfo = textReaderType.GetField("_reader", BindingFlags.Instance | BindingFlags.NonPublic);
                    //FieldInfo writeStateField = typeof(JsonReader).GetField("_currentState", BindingFlags.Instance | BindingFlags.NonPublic);
                    TextReader rawReader = fieldInfo.GetValue((JsonTextReader)reader) as TextReader;
                    char[] block = new char[100];
                    int charsRead = rawReader.ReadBlock(block, 0, block.Length);
                    charsRead.GetHashCode();
                }
                //if (reader.TokenType != JsonToken.StartObject)
                //{
                //    throw new JsonException("Expected JsonToken.StartObject. Path: " + reader.Path);
                //}

                return new MemoryStream();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                JsonCustomWriter customWriter = writer as JsonCustomWriter;
                customWriter.Writer.Write("\"Binary\"");
                //FieldInfo writeStateField = typeof(JsonWriter).GetField("_currentState", BindingFlags.Instance | BindingFlags.NonPublic);
                customWriter.WriteRawValue(string.Empty); // This makes the internal state of the writer consistent with "we just wrote a property value"
                //writeStateField.SetValue(writer, 3);
            }
        }
    }
}
