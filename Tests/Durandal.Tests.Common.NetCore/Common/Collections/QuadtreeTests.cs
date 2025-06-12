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
    public class QuadtreeTests
    {
        [TestMethod]
        public void TestQuadtreeAddRemove()
        {
            DynamicQuadtree<int> tree = new DynamicQuadtree<int>();
            IRandom rand = new FastRandom(75);
            const int numVectors = 1000;
            List<Vector2f> allItems = new List<Vector2f>();
            StaticAverage yes = new StaticAverage();
            for (int loop = 0; loop < 10; loop++)
            {
                allItems.Clear();

                // Add a bunch of things
                for (int c = 0; c < numVectors; c++)
                {
                    Vector2f point = new Vector2f(100f * ((float)rand.NextDouble() - 0.5f), 100f * ((float)rand.NextDouble() - 0.5f));
                    allItems.Add(point);
                    tree.AddItem(c, point);
                }

                // Check containment of all the vectors
                for (int vc = 0; vc < numVectors; vc++)
                {
                    bool foundVec = false;
                    Vector2f testVec = allItems[vc];
                    IEnumerable<Tuple<int, Vector2f>> foundPoints = tree.GetItemsNearPoint(testVec);
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
                for (int c = 0; c < numVectors; c++)
                {
                    tree.RemoveItem(c);
                }
            }
        }

        [TestMethod]
        public void TestQuadtreeGetItemsNearPoint()
        {
            DynamicQuadtree<int> tree = new DynamicQuadtree<int>();
            IRandom rand = new FastRandom(54);
            const int numVectors = 1000;

            // Add a bunch of things
            for (int c = 0; c < numVectors; c++)
            {
                Vector2f point = new Vector2f(100f * ((float)rand.NextDouble() - 0.5f), 100f * ((float)rand.NextDouble() - 0.5f));
                tree.AddItem(c, point);
            }

            // Check that nearest-neighbor calculation always returns the correct value
            for (int c = 0; c < 1000; c++)
            {
                Vector2f testPoint = new Vector2f(110f * ((float)rand.NextDouble() - 0.5f), 110f * ((float)rand.NextDouble() - 0.5f));

                int actualNearestItem = -1;
                float nearestDist = float.MaxValue;
                foreach (var item in tree.GetAllItems())
                {
                    float dist = item.Item2.Distance(testPoint);
                    if (dist < nearestDist)
                    {
                        actualNearestItem = item.Item1;
                        nearestDist = dist;
                    }
                }

                bool found = false;
                var nearbyItems = tree.GetItemsNearPoint(testPoint);
                foreach (var nearbyItem in nearbyItems)
                {
                    if (nearbyItem.Item1 == actualNearestItem)
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found, "GetItemsNearPoint did not return the actual nearest item");
            }
        }
    }
}
