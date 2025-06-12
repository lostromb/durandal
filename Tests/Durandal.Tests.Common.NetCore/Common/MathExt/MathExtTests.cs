
using Durandal.Common.MathExt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.MathExt
{
    [TestClass]
    public class MathExtTests
    {
        
        [TestMethod]
        public void TestBigIntegerGeneratePseudoPrime()
        {
            IRandom rand = new FastRandom(65);
            for (int c = 0; c < 20; c++)
            {
                BigInteger prime = BigInteger.GenPseudoPrime(128, 10, rand);
                Assert.IsTrue(prime.IsProbablePrime(10));
            }
        }

        [TestMethod]
        public void TestBigIntegerArithmetic()
        {
            IRandom rand = new FastRandom(65);

            for (int c = 0; c < 100; c++)
            {
                // define two BigIntegers in base 10
                BigInteger bn1 = new BigInteger(rand.NextInt(1000000, 100000000));
                BigInteger bn2 = new BigInteger(rand.NextInt(1000000, 100000000));

                // Determine the quotient and remainder by dividing
                // the first number by the second.
                BigInteger bn3 = bn1 / bn2;
                BigInteger bn4 = bn1 % bn2;

                // Recalculate the number
                BigInteger bn5 = (bn3 * bn2) + bn4;

                Assert.AreEqual(bn1, bn5);

                BigInteger bn6 = bn1 - bn5;
                Assert.AreEqual(BigInteger.Zero, bn6);
            }
        }

        [TestMethod]
        public void TestBigIntegerModExp()
        {
            // private and public key
            BigInteger bi_e = new BigInteger(
                "a932b948feed4fb2b692609bd22164fc9edb" +
                "59fae7880cc1eaff7b3c9626b7e5b241c27a" +
                "974833b2622ebe09beb451917663d4723248" +
                "8f23a117fc97720f1e7", 16);

            BigInteger bi_d = new BigInteger(
                "4adf2f7a89da93248509347d2ae506d683dd" +
                "3a16357e859a980c4f77a4e2f7a01fae289f" +
                "13a851df6e9db5adaa60bfd2b162bbbe31f7" +
                "c8f828261a6839311929d2cef4f864dde65e" +
                "556ce43c89bbbf9f1ac5511315847ce9cc8d" +
                "c92470a747b8792d6a83b0092d2e5ebaf852" +
                "c85cacf34278efa99160f2f8aa7ee7214de07b7", 16);

            BigInteger bi_n = new BigInteger(
                "e8e77781f36a7b3188d711c2190b560f205a" +
                "52391b3479cdb99fa010745cbeba5f2adc08" +
                "e1de6bf38398a0487c4a73610d94ec36f17f" +
                "3f46ad75e17bc1adfec99839589f45f95ccc" +
                "94cb2a5c500b477eb3323d8cfab0c8458c96" +
                "f0147a45d27e45a4d11d54d77684f65d48f1" +
                "5fafcc1ba208e71e921b9bd9017c16a5231af7f", 16);

            // data
            BigInteger bi_data = new BigInteger(
                "12345678901234567890", 10);

            // encrypt and decrypt data
            BigInteger bi_encrypted = bi_data.ModPow(bi_e, bi_n);
            BigInteger bi_decrypted = bi_encrypted.ModPow(
                bi_d, bi_n);

            Assert.AreEqual(bi_data, bi_decrypted);
        }

        [TestMethod]
        public void TestBigIntegerGenerateCoPrime()
        {
            IRandom rand = new FastRandom(634);
            for (int c = 0; c < 20; c++)
            {
                BigInteger prime = BigInteger.GenPseudoPrime(128, 10, rand);
                BigInteger coPrime = prime.GenCoPrime(128, rand);
                Assert.AreEqual(1, prime.Gcd(coPrime));
            }
        }

        [TestMethod]
        public void TestBigIntegerJacobi()
        {
            IRandom rand = new FastRandom(523);

            try
            {
                BigInteger.Jacobi(new BigInteger(2), new BigInteger(4));
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            for (int c = 0; c < 10; c++)
            {
                BigInteger a = new BigInteger((rand.NextInt(1, 100000) * 2) + 1);
                BigInteger b = new BigInteger((rand.NextInt(1, 100000) * 2) + 1);
                int symbol = BigInteger.Jacobi(a, b);
                Console.WriteLine(symbol);
            }

            for (int c = 0; c < 10; c++)
            {
                BigInteger a = BigInteger.GenPseudoPrime(128, 5, rand);
                BigInteger b = BigInteger.GenPseudoPrime(128, 5, rand);
                int symbol = BigInteger.Jacobi(a, b);
                Assert.AreEqual(1, Math.Abs(symbol));
            }
        }
        
        [TestMethod]
        public void TestBigIntegerLucasStrongTest()
        {
            IRandom rand = new FastRandom(686);
            for (int c = 0; c < 10; c++)
            {
                Assert.IsTrue(BigInteger.GenPseudoPrime(128, 10, rand).LucasStrongTest());
            }
        }

        [TestMethod]
        public void TestBigIntegerRabinMillerTest()
        {
            IRandom rand = new FastRandom(686);
            for (int c = 0; c < 10; c++)
            {
                Assert.IsTrue(BigInteger.GenPseudoPrime(128, 10, rand).RabinMillerTest(5));
            }
        }

        [TestMethod]
        public void TestBigIntegerSolovayStrassenTest()
        {
            IRandom rand = new FastRandom(686);
            for (int c = 0; c < 10; c++)
            {
                Assert.IsTrue(BigInteger.GenPseudoPrime(128, 10, rand).SolovayStrassenTest(5));
            }
        }

        [TestMethod]
        public void TestFastMathExp()
        {
            for (float input = 0.0f; input < 5.0f; input += 0.01f)
            {
                Assert.AreEqual((float)Math.Exp(input), FastMath.Exp(input), 0.001f);
            }
        }

        [TestMethod]
        public void TestFastMathLog()
        {
            for (float input = 0.0f; input < 5.0f; input += 0.01f)
            {
                Assert.AreEqual((float)Math.Log(input), FastMath.Log(input), 0.01f);
            }
        }

        [TestMethod]
        public void TestFastMathSigmoid()
        {
            for (float input = -5.0f; input < 5.0f; input += 0.01f)
            {
                Assert.AreEqual((float)(1 / (1 + Math.Exp(0 - input))), FastMath.Sigmoid(input), 0.01f);
            }
        }

        [TestMethod]
        public void TestTinyHistogramEmptyHistogram()
        {
            TinyHistogram h = new TinyHistogram();
            string rendering = h.RenderAsOneLine(10, true);
            Assert.AreEqual("0.00   |          |   10.00", rendering);
        }


        [TestMethod]
        public void TestTinyHistogramSingleValue()
        {
            TinyHistogram h = new TinyHistogram();
            h.AddValue(5);
            string rendering = h.RenderAsOneLine(10, true);
            Assert.AreEqual("5.00   |█         |   5.00", rendering);
        }

        [TestMethod]
        public void TestTinyHistogramInvalidSize()
        {
            TinyHistogram h = new TinyHistogram();

            try
            {
                string rendering = h.RenderAsOneLine(0, true);
                Assert.Fail("Should have thrown exception");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                string[] rendering = h.RenderAsMultiLine(0, 10);
                Assert.Fail("Should have thrown exception");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                string[] rendering = h.RenderAsMultiLine(10, 0);
                Assert.Fail("Should have thrown exception");
            }
            catch (ArgumentOutOfRangeException) { }
        }
        
        [TestMethod]
        public void TestTinyHistogramSingleLineUnicode()
        {
            TinyHistogram h = new TinyHistogram();
            FastRandom rand = new FastRandom(921122);
            double min = -50;
            double range = 100;
            double scale = 5;
            for (int c = 0; c < 100; c++)
            {
                h.AddValue(rand.NextDouble() * range + min, rand.NextDouble() * scale);
            }

            string rendering = h.RenderAsOneLine(10, true);
            Assert.AreEqual("-46.15   |▄▄▆▅▃▅▄▄▃█|   49.91", rendering);
            rendering = h.RenderAsOneLine(40, true);
            Assert.AreEqual("-46.15   |▃▂▄▃▂▁▄▆▆▄▄▄▂▅▅▁▃▂▂▁▄▁█▂▄ ▅▃ ▂▄▇▄▅ ▁▅▅█▆|   49.91", rendering);
        }
        
        [TestMethod]
        public void TestTinyHistogramSingleLineAscii()
        {
            TinyHistogram h = new TinyHistogram();
            FastRandom rand = new FastRandom(921122);
            double min = -50;
            double range = 100;
            double scale = 5;
            for (int c = 0; c < 100; c++)
            {
                h.AddValue(rand.NextDouble() * range + min, rand.NextDouble() * scale);
            }

            string rendering = h.RenderAsOneLine(10, false);
            Assert.AreEqual("-46.15   |▒▒▓▓▒▓▒▒▒█|   49.91", rendering);
            rendering = h.RenderAsOneLine(40, false);
            Assert.AreEqual("-46.15   |▒░▒▒░░▒▓▓▒▒▒░▓▓░▒░░░▒░█░▒ ▓▒ ░▒█▒▓ ░▓▓█▓|   49.91", rendering);
        }

        [TestMethod]
        public void TestTinyHistogramMultiLine()
        {
            TinyHistogram h = new TinyHistogram();
            FastRandom rand = new FastRandom(921122);
            double min = -50;
            double range = 100;
            double scale = 5;
            for (int c = 0; c < 100; c++)
            {
                h.AddValue(rand.NextDouble() * range + min, rand.NextDouble() * scale);
            }

            string[] rendering = h.RenderAsMultiLine(5, 40);
            Assert.AreEqual(5, rendering.Length);
            Assert.AreEqual("  -46.15 -> -26.94   |███████████████████████████", rendering[0]);
            Assert.AreEqual("  -26.94 -> -7.72    |███████████████████████████████████▓", rendering[1]);
            Assert.AreEqual("   -7.72 -> 11.49    |██████████████████████████░", rendering[2]);
            Assert.AreEqual("   11.49 -> 30.70    |████████████████████████████▒", rendering[3]);
            Assert.AreEqual("   30.70 -> 49.91    |████████████████████████████████████████░", rendering[4]);
        }

        [TestMethod]
        public void TestBranchlessMaxInt32()
        {
            IRandom rand = new FastRandom(5434);
            for (int iteration = 0; iteration < 100000; iteration++)
            {
                int valueA = GenerateSpecialInt(rand);
                int valueB = GenerateSpecialInt(rand);
                Assert.AreEqual(Math.Max(valueA, valueB), FastMath.Max(valueA, valueB),
                    string.Format("Failed to find max of {0} and {1}: got {2}", valueA, valueB, FastMath.Max(valueA, valueB)));
                Assert.AreEqual(Math.Max(valueA, valueB), FastMath.Max(valueB, valueA),
                    string.Format("Failed to find max of {0} and {1}: got {2}", valueB, valueA, FastMath.Max(valueB, valueA)));
            }
        }

        [TestMethod]
        public void TestBranchlessMinInt32()
        {
            IRandom rand = new FastRandom(784224);
            for (int iteration = 0; iteration < 100000; iteration++)
            {
                int valueA = GenerateSpecialInt(rand);
                int valueB = GenerateSpecialInt(rand);
                Assert.AreEqual(Math.Min(valueA, valueB), FastMath.Min(valueA, valueB),
                    string.Format("Failed to find min of {0} and {1}: got {2}", valueA, valueB, FastMath.Min(valueA, valueB)));
                Assert.AreEqual(Math.Min(valueA, valueB), FastMath.Min(valueB, valueA),
                    string.Format("Failed to find min of {0} and {1}: got {2}", valueB, valueA, FastMath.Min(valueB, valueA)));
            }
        }

        private static int GenerateSpecialInt(IRandom rand)
        {
            if (rand.NextFloat() < 0.3f)
            {
                switch (rand.NextInt(0, 6))
                {
                    case 0:
                        return int.MinValue;
                    case 1:
                        return int.MaxValue;
                    case 2:
                        return 0;
                    case 3:
                        return -1;
                    case 4:
                        return int.MinValue + 1;
                    case 5:
                        return int.MaxValue - 1;
                }
            }

            return rand.NextInt();
        }
    }
}
