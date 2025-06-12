using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Collections
{
    [TestClass]
    public class OctreeTests
    {
        [TestMethod]
        public void TestOctree()
        {
            DynamicOctree<int> tree = new DynamicOctree<int>();
            IRandom rand = new FastRandom();
            List<Vector3f> allItems = new List<Vector3f>();
            for (int loop = 0; loop < 10; loop++)
            {
                allItems.Clear();

                // Add a bunch of things
                for (int c = 0; c < 1000; c++)
                {
                    Vector3f point = new Vector3f(100f * ((float)rand.NextDouble() - 0.5f), 100f * ((float)rand.NextDouble() - 0.5f), 100f * ((float)rand.NextDouble() - 0.5f));
                    allItems.Add(point);
                    tree.AddItem(c, point);
                }

                // Check containment of all the vectors
                for (int vc = 0; vc < 1000; vc++)
                {
                    bool foundVec = false;
                    Vector3f testVec = allItems[vc];
                    IEnumerable<Tuple<int, Vector3f>> foundPoints = tree.GetItemsNearPoint(testVec);
                    foreach (var point in foundPoints)
                    {
                        if (point.Item1 == vc)
                        {
                            foundVec = true;
                        }
                    }

                    Assert.IsTrue(foundVec);
                }

                // Then remove them
                for (int c = 0; c < 1000; c++)
                {
                    tree.RemoveItem(c);
                }
            }
        }
    }
}
