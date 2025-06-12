using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.IO.Json;

namespace Durandal.Tests.Common.IO.Json
{
    [TestClass]
    public class JsonCustomReaderTests
    {
        [TestMethod]
        public void TestJsonCustomReader_EmptyInput()
        {
            JsonReaderParityTest(string.Empty);
        }

        [TestMethod]
        public void TestJsonCustomReader_EmptyObject()
        {
            JsonReaderParityTest("{}");
        }

        [TestMethod]
        public void TestJsonCustomReader_BasicNull()
        {
            JsonReaderParityTest("{ \"val\": null }");
        }

        [TestMethod]
        public void TestJsonCustomReader_BasicTrue()
        {
            JsonReaderParityTest("{ \"val\": true }");
        }

        [TestMethod]
        public void TestJsonCustomReader_BasicFalse()
        {
            JsonReaderParityTest("{ \"val\": false }");
        }

        [TestMethod]
        public void TestJsonCustomReader_BasicIntegerSingleDigit()
        {
            JsonReaderParityTest("{ \"val\": 5 }");
        }

        [TestMethod]
        public void TestJsonCustomReader_BasicInteger()
        {
            JsonReaderParityTest("{ \"val\": 1234 }");
        }

        [TestMethod]
        public void TestJsonCustomReader_BasicBigInteger()
        {
            JsonReaderParityTest("{ \"val\": 123456789012345678901234567890 }");
        }

        [TestMethod]
        public void TestJsonCustomReader_BasicFloat()
        {
            JsonReaderParityTest("{ \"val\": 5.8 }");
        }

        [TestMethod]
        public void TestJsonCustomReader_BasicFloatNegative()
        {
            JsonReaderParityTest("{ \"val\": -5.8 }");
        }

        [TestMethod]
        [Ignore]
        public void TestJsonCustomReader_BasicFloatPositive()
        {
            JsonReaderParityTest("{ \"val\": +5.8 }");
        }

        [TestMethod]
        public void TestJsonCustomReader_BasicHexInteger()
        {
            JsonReaderParityTest("{ \"val\": 0xDEAFBEEF }");
        }

        [TestMethod]
        public void TestJsonCustomReader_BasicExponent()
        {
            JsonReaderParityTest("{ \"val\": 10.3e6 }");
        }

        [TestMethod]
        public void TestJsonCustomReader_BasicNaN()
        {
            JsonReaderParityTest("{ \"val\": NaN }");
        }

        [TestMethod]
        public void TestJsonCustomReader_BasicString()
        {
            JsonReaderParityTest("{ \"val\": \"antonymph\" }");
        }

        private static void JsonReaderParityTest(string json)
        {
            using (TextReader basicStringReader = new StringReader(json))
            using (TextReader customStringReader = new StringReader(json))
            using (JsonTextReader basicJsonReader = new JsonTextReader(basicStringReader))
            using (JsonCustomTextReader customJsonReader = new JsonCustomTextReader(customStringReader))
            {
                bool basicReadOk = true;
                while (basicReadOk)
                {
                    basicReadOk = basicJsonReader.Read();
                    bool customReadOk = customJsonReader.Read();
                    Console.Write("Read() " + basicReadOk);
                    Assert.AreEqual(basicReadOk, customReadOk);
                    if (basicReadOk)
                    {
                        Console.WriteLine(" TokenType " + basicJsonReader.TokenType + " ValueType " + basicJsonReader.ValueType + " Value " + basicJsonReader.Value);
                        Assert.AreEqual(basicJsonReader.TokenType, customJsonReader.TokenType);
                        Assert.AreEqual(basicJsonReader.ValueType, customJsonReader.ValueType);
                        Assert.AreEqual(basicJsonReader.Value, customJsonReader.Value);
                    }
                }
            }
        }
    }
}
