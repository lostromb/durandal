using Durandal.Common.MathExt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.MathExt
{
    [TestClass]
    public class MatrixTests
    {
        [TestMethod]
        public void TestMatrix3x3f_MatrixMultiply()
        {
            Matrix3x3f A = new Matrix3x3f(
                1, 2, 3,
                4, 5, 6,
                7, 8, 9);

            Matrix3x3f B = new Matrix3x3f(
                11, 14, 17,
                12, 15, 18,
                13, 16, 19
                );

            Matrix3x3f C = A * B;
            Assert.AreEqual(74, C.R1_C1, 0.001f);
            Assert.AreEqual(92, C.R1_C2, 0.001f);
            Assert.AreEqual(110, C.R1_C3, 0.001f);
            Assert.AreEqual(182, C.R2_C1, 0.001f);
            Assert.AreEqual(227, C.R2_C2, 0.001f);
            Assert.AreEqual(272, C.R2_C3, 0.001f);
            Assert.AreEqual(290, C.R3_C1, 0.001f);
            Assert.AreEqual(362, C.R3_C2, 0.001f);
            Assert.AreEqual(434, C.R3_C3, 0.001f);

            A = new Matrix3x3f(
                5, 1, 9,
                3, 8, 3,
                6, 2, 4);

            B = new Matrix3x3f(
                4, 8, 3,
                9, 1, 6,
                7, 2, 5
                );

            C = A * B;
            Assert.AreEqual(92, C.R1_C1, 0.001f);
            Assert.AreEqual(59, C.R1_C2, 0.001f);
            Assert.AreEqual(66, C.R1_C3, 0.001f);
            Assert.AreEqual(105, C.R2_C1, 0.001f);
            Assert.AreEqual(38, C.R2_C2, 0.001f);
            Assert.AreEqual(72, C.R2_C3, 0.001f);
            Assert.AreEqual(70, C.R3_C1, 0.001f);
            Assert.AreEqual(58, C.R3_C2, 0.001f);
            Assert.AreEqual(50, C.R3_C3, 0.001f);
        }

        [TestMethod]
        public void TestMatrix3x3f_VectorMultiplyLeft()
        {
            Vector3f A = new Vector3f(5, 1, 9);

            Matrix3x3f B = new Matrix3x3f(
                4, 8, 3,
                9, 1, 6,
                7, 2, 5
                );

            Vector3f C = A * B;
            Assert.AreEqual(92, C.X, 0.001f);
            Assert.AreEqual(59, C.Y, 0.001f);
            Assert.AreEqual(66, C.Z, 0.001f);
        }

        [TestMethod]
        public void TestMatrix3x3f_VectorMultiplyRight()
        {
            Matrix3x3f A = new Matrix3x3f(
                5, 1, 9,
                3, 8, 7,
                6, 2, 4
                );

            Vector3f B = new Vector3f(4, 9, 7);

            Vector3f C = A * B;
            Assert.AreEqual(92, C.X, 0.001f);
            Assert.AreEqual(133, C.Y, 0.001f);
            Assert.AreEqual(70, C.Z, 0.001f);
        }
    }
}
