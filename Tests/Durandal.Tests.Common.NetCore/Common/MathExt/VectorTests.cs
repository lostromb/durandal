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
    public class VectorTests
    {
        [TestMethod]
        public void TestVector3f_AngleBetween()
        {
            Vector3f a = new Vector3f(-10, 0, 0);
            Vector3f b = new Vector3f(0, 0, 15);
            Assert.AreEqual(FastMath.PI / 2f, a.AngleBetween(b), 0.01f);
        }
    }
}
