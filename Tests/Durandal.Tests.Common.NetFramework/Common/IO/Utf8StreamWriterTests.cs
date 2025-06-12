using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.IO
{
    [TestClass]
    public class Utf8StreamWriterTests
    {
        [TestMethod]
        public void TestUtf8StreamWriterNullConstructor()
        {
            try
            {
                new Utf8StreamWriter(null, leaveOpen: true);
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestUtf8StreamWriterProperties()
        {
            using (MemoryStream stream = new MemoryStream())
            using (Utf8StreamWriter testWriter = new Utf8StreamWriter(stream))
            {
                Assert.AreEqual(Encoding.UTF8.BodyName, testWriter.Encoding.BodyName);
            }
        }

        [TestMethod]
        public void TestUtf8StreamWriterMultipleDisposal()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                testWriter.WriteLine("This is a basic test of the string writer");
                truthWriter.WriteLine("This is a basic test of the string writer");
                testWriter.Dispose();
                testWriter.Dispose();
            });
        }

        [TestMethod]
        public void TestUtf8StreamWriterBasic()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                for (int loop = 0; loop < 1000; loop++)
                {
                    testWriter.WriteLine("This is a basic test of the string writer");
                    truthWriter.WriteLine("This is a basic test of the string writer");
                }

                testWriter.Flush();
            });
        }

        [TestMethod]
        public async Task TestUtf8StreamWriterBasicAsync()
        {
            await RunTestBaseAsync(async (testWriter, truthWriter) =>
            {
                for (int loop = 0; loop < 1000; loop++)
                {
                    await testWriter.WriteLineAsync("This is a basic test of the string writer").ConfigureAwait(false);
                    await truthWriter.WriteLineAsync("This is a basic test of the string writer").ConfigureAwait(false);
                }

                await testWriter.FlushAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(7)]
        [DataRow(8)]
        [DataRow(9)]
        [DataRow(15)]
        [DataRow(16)]
        [DataRow(33)]
        [DataRow(7024)]
        [DataRow(78436)]
        [DataRow(71234)]
        [DataRow(91811)]
        [DataRow(150017)]
        public async Task TestUtf8StreamWriterVaryingLengths(int inputLength)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('X', inputLength);
            string s = sb.ToString();
            RunTestBase((testWriter, truthWriter) =>
            {
                testWriter.WriteLine(s);
                truthWriter.WriteLine(s);
                testWriter.Flush();
            });

            await RunTestBaseAsync(async (testWriter, truthWriter) =>
            {
                await testWriter.WriteLineAsync(s).ConfigureAwait(false);
                await truthWriter.WriteLineAsync(s).ConfigureAwait(false);
                await testWriter.FlushAsync();
            }).ConfigureAwait(false);
        }

        [TestMethod]
        public void TestUtf8StreamWriterComplexSymbols()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                for (int loop = 0; loop < 1000; loop++)
                {
                    testWriter.Write("Get ready for some wild stuff here in a minute ЖЖЖ⚕️🐢 wow 🐰 isn't that crazy oh no here's more turtles 🐢🐢🐢");
                    truthWriter.Write("Get ready for some wild stuff here in a minute ЖЖЖ⚕️🐢 wow 🐰 isn't that crazy oh no here's more turtles 🐢🐢🐢");
                }
            });
        }

        [TestMethod]
        public async Task TestUtf8StreamWriterComplexSymbolsAsync()
        {
            await RunTestBaseAsync(async (testWriter, truthWriter) =>
            {
                for (int loop = 0; loop < 1000; loop++)
                {
                    await testWriter.WriteAsync("Get ready for some wild stuff here in a minute ЖЖЖ⚕️🐢 wow 🐰 isn't that crazy oh no here's more turtles 🐢🐢🐢").ConfigureAwait(false);
                    await truthWriter.WriteAsync("Get ready for some wild stuff here in a minute ЖЖЖ⚕️🐢 wow 🐰 isn't that crazy oh no here's more turtles 🐢🐢🐢").ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        [TestMethod]
        public void TestUtf8StreamWriterPartialSurrogates()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                char[] field = "⚕️🐢🐰hey⚕️お🐢🐢yo_🐰⚕️🐢🐰hey⚕️お🐢🐢yo_🐰".ToCharArray();
                IRandom random = new FastRandom();
                for (int loop = 0; loop < 1000; loop++)
                {
                    int charsWritten = 0;
                    while (charsWritten < field.Length)
                    {
                        int nextWriteSize = Math.Min(random.NextInt(1, 10), field.Length - charsWritten);
                        testWriter.Write(field, charsWritten, nextWriteSize);
                        truthWriter.Write(field, charsWritten, nextWriteSize);
                        charsWritten += nextWriteSize;
                    }

                    testWriter.Write("This is some basic ascii to flush the encoder state");
                    truthWriter.Write("This is some basic ascii to flush the encoder state");
                }
            });
        }

        [TestMethod]
        public async Task TestUtf8StreamWriterPartialSurrogatesAsync()
        {
            await RunTestBaseAsync(async (testWriter, truthWriter) =>
            {
                char[] field = "⚕️🐢Ж🐰hey⚕️お🐢Ж🐢yo_🐰⚕️🐢🐰hey⚕️お🐢🐢Жyo_🐰".ToCharArray();
                IRandom random = new FastRandom();
                for (int loop = 0; loop < 1000; loop++)
                {
                    int charsWritten = 0;
                    while (charsWritten < field.Length)
                    {
                        int nextWriteSize = Math.Min(random.NextInt(1, 10), field.Length - charsWritten);
                        await testWriter.WriteAsync(field, charsWritten, nextWriteSize).ConfigureAwait(false);
                        await truthWriter.WriteAsync(field, charsWritten, nextWriteSize).ConfigureAwait(false);
                        charsWritten += nextWriteSize;
                    }

                    await testWriter.WriteAsync("This is some basic ascii to flush the encoder state").ConfigureAwait(false);
                    await truthWriter.WriteAsync("This is some basic ascii to flush the encoder state").ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        [TestMethod]
        public void TestUtf8StreamWriterJapanese()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                for (int loop = 0; loop < 1000; loop++)
                {
                    testWriter.Write("すみません、最寄りの外国大使館への道順を教えていただけますか");
                    truthWriter.Write("すみません、最寄りの外国大使館への道順を教えていただけますか");
                }
            });
        }

        [TestMethod]
        public async Task TestUtf8StreamWriterJapaneseAsync()
        {
            await RunTestBaseAsync(async (testWriter, truthWriter) =>
            {
                for (int loop = 0; loop < 1000; loop++)
                {
                    await testWriter.WriteAsync("すみません、最寄りの外国大使館への道順を教えていただけますか").ConfigureAwait(false);
                    await truthWriter.WriteAsync("すみません、最寄りの外国大使館への道順を教えていただけますか").ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        [TestMethod]
        public void TestUtf8StreamWriterWriteInt()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                testWriter.Write(int.MinValue);
                truthWriter.Write(int.MinValue);
                testWriter.Write(int.MaxValue);
                truthWriter.Write(int.MaxValue);
            });
        }

        [TestMethod]
        public void TestUtf8StreamWriterWriteUInt()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                testWriter.Write(uint.MinValue);
                truthWriter.Write(uint.MinValue);
                testWriter.Write(uint.MaxValue);
                truthWriter.Write(uint.MaxValue);
            });
        }

        [TestMethod]
        public void TestUtf8StreamWriterWriteLong()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                testWriter.Write(long.MinValue);
                truthWriter.Write(long.MinValue);
                testWriter.Write(long.MaxValue);
                truthWriter.Write(long.MaxValue);
            });
        }

        [TestMethod]
        public void TestUtf8StreamWriterWriteULong()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                testWriter.Write(ulong.MinValue);
                truthWriter.Write(ulong.MinValue);
                testWriter.Write(ulong.MaxValue);
                truthWriter.Write(ulong.MaxValue);
            });
        }

        [TestMethod]
        public void TestUtf8StreamWriterWriteFloatBasic()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                testWriter.Write(0.0012531933f);
                truthWriter.Write(0.0012531933f);
                testWriter.Write(float.MinValue);
                truthWriter.Write(float.MinValue);
                testWriter.Write(float.MaxValue);
                truthWriter.Write(float.MaxValue);
            });
        }

        [Ignore]
        [TestMethod]
        public void TestUtf8StreamWriterWriteFloatExhaustive()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                IRandom rand = new FastRandom(127234);
                Span<byte> bytes = stackalloc byte[4];
                Span<float> floats = MemoryMarshal.Cast<byte, float>(bytes);
                for (int loop = 0; loop < 100000; loop++)
                {
                    rand.NextBytes(bytes);
                    float val = floats[0];
                    if (!float.IsNaN(val) && !float.IsInfinity(val))
                    {
                        testWriter.Write(val);
                        truthWriter.Write(val);
                    }
                }
            });
        }

        [TestMethod]
        public void TestUtf8StreamWriterWriteSingleChars()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                for (int loop = 0; loop < 100000; loop++)
                {
                    testWriter.Write('a');
                    truthWriter.Write('a');
                }
            });
        }

        [TestMethod]
        public async Task TestUtf8StreamWriterWriteSingleCharsAsync()
        {
            await RunTestBaseAsync(async (testWriter, truthWriter) =>
            {
                for (int loop = 0; loop < 100000; loop++)
                {
                    await testWriter.WriteAsync('a').ConfigureAwait(false);
                    await truthWriter.WriteAsync('a').ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        [TestMethod]
        public void TestUtf8StreamWriterWriteTwoByteChars()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                for (int loop = 0; loop < 100000; loop++)
                {
                    testWriter.Write('Ж');
                    truthWriter.Write('Ж');
                }
            });
        }

        [TestMethod]
        public async Task TestUtf8StreamWriterWriteTwoByteCharsAsync()
        {
            await RunTestBaseAsync(async (testWriter, truthWriter) =>
            {
                for (int loop = 0; loop < 100000; loop++)
                {
                    await testWriter.WriteAsync('Ж').ConfigureAwait(false);
                    await truthWriter.WriteAsync('Ж').ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        [TestMethod]
        public void TestUtf8StreamWriterWriteSingleWideChars()
        {
            RunTestBase((testWriter, truthWriter) =>
            {
                for (int loop = 0; loop < 100000; loop++)
                {
                    testWriter.Write('の');
                    truthWriter.Write('の');
                }
            });
        }

        [TestMethod]
        public async Task TestUtf8StreamWriterWriteSingleWideCharsAsync()
        {
            await RunTestBaseAsync(async (testWriter, truthWriter) =>
            {
                for (int loop = 0; loop < 100000; loop++)
                {
                    await testWriter.WriteAsync('の').ConfigureAwait(false);
                    await truthWriter.WriteAsync('の').ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        private static void RunTestBase(Action<Utf8StreamWriter, StreamWriter> testImpl)
        {
            using (MemoryStream stream1 = new MemoryStream())
            using (MemoryStream stream2 = new MemoryStream())
            {
                using (Utf8StreamWriter testWriter = new Utf8StreamWriter(stream1, leaveOpen: true))
                using (StreamWriter truthWriter = new StreamWriter(stream2, StringUtils.UTF8_WITHOUT_BOM, 1024, leaveOpen: true))
                {
                    testImpl(testWriter, truthWriter);
                }

                byte[] testOutput = stream1.ToArray();
                byte[] truthOutput = stream2.ToArray();
                //Console.WriteLine("Test output: " + BinaryHelpers.ToHexString(testOutput));
                //Console.WriteLine("Truth output: " + BinaryHelpers.ToHexString(truthOutput));
                Assert.IsTrue(ArrayExtensions.ArrayEquals(testOutput, truthOutput));
            }
        }

        private static async Task RunTestBaseAsync(Func<Utf8StreamWriter, StreamWriter, Task> testImpl)
        {
            using (MemoryStream stream1 = new MemoryStream())
            using (MemoryStream stream2 = new MemoryStream())
            {
                using (Utf8StreamWriter testWriter = new Utf8StreamWriter(stream1, leaveOpen: true))
                using (StreamWriter truthWriter = new StreamWriter(stream2, StringUtils.UTF8_WITHOUT_BOM, 1024, leaveOpen: true))
                {
                    await testImpl(testWriter, truthWriter).ConfigureAwait(false);
                }

                byte[] testOutput = stream1.ToArray();
                byte[] truthOutput = stream2.ToArray();
                //Console.WriteLine("Test output: " + BinaryHelpers.ToHexString(testOutput));
                //Console.WriteLine("Truth output: " + BinaryHelpers.ToHexString(truthOutput));
                Assert.IsTrue(ArrayExtensions.ArrayEquals(testOutput, truthOutput));
            }
        }
    }
}
