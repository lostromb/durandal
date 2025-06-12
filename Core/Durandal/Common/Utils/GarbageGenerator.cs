using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// A testing class for exercising the garbage collector. It has only one method, to generate
    /// a certain amount of garbage which is guaranteed to eventually reach GC gen 2 and then be collected.
    /// </summary>
    public class GarbageGenerator
    {
        private const int BYTES_PER_OBJECT = 340;
        private int _lastGen2CollectionCount = 0;
        private readonly HashSet<GarbageObject> _gen0 = new HashSet<GarbageObject>();
        private readonly HashSet<GarbageObject> _gen1 = new HashSet<GarbageObject>();
        private readonly HashSet<GarbageObject> _gen2 = new HashSet<GarbageObject>();
        private readonly Random _rand = new Random();

        /// <summary>
        /// Generates random garbage data and store references to it locally, effectively adding managed memory pressure.
        /// </summary>
        /// <param name="bytesRequested">The number of bytes of garbage you wish to generate.</param>
        /// <returns>The approximate number of bytes actually generated.</returns>
        public int GenerateGarbage(int bytesRequested)
        {

            int objectsToCreate = Math.Max(1, bytesRequested / BYTES_PER_OBJECT); // assuming one GarbageObject is about 340 bytes to allocate
            GarbageObject rootObj = GenerateRandomObjectsRecursive(ref objectsToCreate);

            lock (this)
            {
                PromoteGenerations();
                _gen0.Add(rootObj);
            }

            return objectsToCreate * BYTES_PER_OBJECT;
        }

        private GarbageObject GenerateRandomObjectsRecursive(ref int objectsRemaining)
        {
            GarbageObject returnVal = new GarbageObject();
            objectsRemaining -= 1;
            returnVal.Field = _rand.Next();
            if (objectsRemaining > 0)
            {
                int numChildren = _rand.Next(0, objectsRemaining / 2);
                for (int c = 0; c < numChildren; c++)
                {
                    returnVal.Junk.Add(GenerateRandomObjectsRecursive(ref objectsRemaining));
                }
            }

            return returnVal;
        }

        private void PromoteGenerations()
        {
            // Has a gen2 collection ran since we last checked?
            if (GC.CollectionCount(2) != _lastGen2CollectionCount)
            {
                _lastGen2CollectionCount = GC.CollectionCount(2);

                // Free generation 2 objects so they are eligible for collection next pass
                _gen2.Clear();

                // Promote most of gen1 to gen2
                // Use hash indexing so that we leave random fragmentation between generations
                int pruneCount = Math.Max(0, _gen0.Count - 1000);
                for (int c = 0; c < pruneCount; c++)
                {
                    var enumerator = _gen1.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        _gen2.Add(enumerator.Current);
                        _gen1.Remove(enumerator.Current);
                    }
                }

                // And promote most of gen0 to gen1
                pruneCount = Math.Max(0, _gen0.Count - 1000);
                for (int c = 0; c < pruneCount; c++)
                {
                    var enumerator = _gen0.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        _gen1.Add(enumerator.Current);
                        _gen0.Remove(enumerator.Current);
                    }
                }
            }
        }

        /// <summary>
        /// An objects whose purpose is to take up space and also hold references to other junk objects.
        /// </summary>
        private class GarbageObject
        {
            public long Field;
            public List<GarbageObject> Junk = new List<GarbageObject>();
        }
    }
}
