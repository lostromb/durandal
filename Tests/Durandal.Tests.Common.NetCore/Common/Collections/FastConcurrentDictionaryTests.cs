namespace Durandal.Tests.Common.Collections
{
    using Durandal.Common.Audio.Codecs.Opus.Celt;
    using Durandal.Common.Collections;
    using Durandal.Common.Compression.BZip2;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class FastConcurrentDictionaryTests
    {
        [TestMethod]
        public void TestFastConcurrentDictionaryConstructor()
        {
            FastConcurrentDictionary<int, int> dict;

            try
            {
                dict = new FastConcurrentDictionary<int, int>(-5);
                Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }

            dict = new FastConcurrentDictionary<int, int>();
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryIndexer()
        {
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(8);
            dict["key1"] = "value1";
            dict["key1"] = "value2";
            dict["key1"] = "value3";
            dict["key2"] = "value1";
            dict["key2"] = "value2";
            dict["key2"] = "value3";
            Assert.AreEqual(2, dict.Count);
            Assert.AreEqual("value3", dict["key1"]);
            Assert.AreEqual("value3", dict["key2"]);

            string val;

            try
            {
                val = dict["notexist"];
                Assert.Fail("Should have thrown a KeyNotFoundException");
            }
            catch (KeyNotFoundException) { }

            for (int c = 0; c < 100; c++)
            {
                dict["key" + c] = "value" + c;
            }

            for (int c = 0; c < 100; c++)
            {
                val = dict["key" + c];

                try
                {
                    val = dict["notexist" + c];
                    Assert.Fail("Should have thrown a KeyNotFoundException");
                }
                catch (KeyNotFoundException) { }
            }
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryAdd()
        {
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(8);

            Assert.AreEqual(0, dict.Count);
            Assert.IsFalse(dict.ContainsKey("notexist"));
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, string>("notexist", "value1")));
            Assert.IsFalse(dict.IsReadOnly);
            dict.Add("key1", "value1");
            Assert.AreEqual(1, dict.Count);
            Assert.IsFalse(dict.ContainsKey("notexist"));
            Assert.IsTrue(dict.ContainsKey("key1"));
            Assert.IsTrue(dict.Contains(new KeyValuePair<string, string>("key1", "value1")));
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, string>("wrongkey", "value1")));
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, string>("key1", "wrongvalue")));

            dict.Add("key2", "value2");
            dict.Add("key3", "value3");
            dict.Add("key4", "value4");

            Assert.AreEqual(4, dict.Count);
            Assert.IsTrue(dict.ContainsKey("key1"));
            Assert.IsTrue(dict.ContainsKey("key2"));
            Assert.IsTrue(dict.ContainsKey("key3"));
            Assert.IsTrue(dict.ContainsKey("key4"));
            Assert.IsFalse(dict.ContainsKey("notexist"));
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryClear()
        {
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(8);
            Assert.AreEqual(0, dict.Count);
            dict.Clear();
            Assert.AreEqual(0, dict.Count);

            dict.Add("key1", "value1");
            dict.Add("key2", "value2");
            dict.Add("key3", "value3");
            dict.Add("key4", "value4");

            Assert.AreEqual(4, dict.Count);
            dict.Clear();

            dict["key1"] = "value1";
            dict["key1"] = "value2";
            dict["key1"] = "value3";
            dict["key2"] = "value1";
            dict["key2"] = "value2";
            dict["key2"] = "value3";

            Assert.AreEqual(2, dict.Count);
            dict.Clear();
            Assert.AreEqual(0, dict.Count);
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryContains()
        {
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(8);
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, string>("wrongkey", "wrongvalue")));
            Assert.IsFalse(dict.ContainsKey("notexist"));

            dict.Add("key1", "value1");
            dict.Add("key2", "value2");
            dict.Add("key3", "value3");
            dict.Add("key4", "value4");

            Assert.AreEqual(4, dict.Count);

            Assert.IsFalse(dict.ContainsKey("notexist"));
            Assert.IsTrue(dict.ContainsKey("key1"));
            Assert.IsTrue(dict.ContainsKey("key2"));
            Assert.IsTrue(dict.ContainsKey("key3"));
            Assert.IsTrue(dict.ContainsKey("key4"));
            Assert.IsTrue(dict.Contains(new KeyValuePair<string, string>("key1", "value1")));
            Assert.IsTrue(dict.Contains(new KeyValuePair<string, string>("key2", "value2")));
            Assert.IsTrue(dict.Contains(new KeyValuePair<string, string>("key3", "value3")));
            Assert.IsTrue(dict.Contains(new KeyValuePair<string, string>("key4", "value4")));
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, string>("wrongkey", "value1")));
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, string>("key1", "wrongvalue")));
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, string>("wrongkey", "wrongvalue")));
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryCopyTo()
        {
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(8);
            dict.Add("key1", "value1");
            dict.Add("key2", "value2");
            dict.Add("key3", "value3");
            dict.Add("key4", "value4");

            KeyValuePair<string, string>[] array = new KeyValuePair<string, string>[dict.Count];
            dict.CopyTo(array, 0);
            Assert.IsTrue(array.Contains(new KeyValuePair<string, string>("key1", "value1")));
            Assert.IsTrue(array.Contains(new KeyValuePair<string, string>("key2", "value2")));
            Assert.IsTrue(array.Contains(new KeyValuePair<string, string>("key3", "value3")));
            Assert.IsTrue(array.Contains(new KeyValuePair<string, string>("key4", "value4")));
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryRemove()
        {
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(8);
            Assert.IsFalse(dict.Remove("notexist"));
            Assert.IsFalse(dict.Remove(new KeyValuePair<string, string>("notkey", "notvalue")));

            for (int c = 0; c < 100; c++)
            {
                dict.Add("key" + c, "value" + c);
            }

            Assert.AreEqual(100, dict.Count);
            Assert.IsFalse(dict.Remove("notexist"));
            Assert.AreEqual(100, dict.Count);

            for (int c = 0; c < 100; c++)
            {
                int removeIdx = (c + 50) % 100;
                Assert.IsTrue(dict.Remove("key" + removeIdx));
                Assert.AreEqual(99 - c, dict.Count);
            }

            for (int c = 0; c < 100; c++)
            {
                dict.Add("key" + c, "value" + c);
            }

            Assert.IsFalse(dict.Remove(new KeyValuePair<string, string>("key1", "wrongvalue")));
            Assert.IsFalse(dict.Remove(new KeyValuePair<string, string>("wrongkey", "value1")));
            Assert.IsFalse(dict.Remove(new KeyValuePair<string, string>("key50", "wrongvalue")));
            Assert.IsFalse(dict.Remove(new KeyValuePair<string, string>("wrongkey", "value50")));
            Assert.IsFalse(dict.Remove(new KeyValuePair<string, string>("key99", "wrongvalue")));
            Assert.IsFalse(dict.Remove(new KeyValuePair<string, string>("wrongkey", "value99")));

            for (int c = 0; c < 100; c++)
            {
                int removeIdx = (c + 50) % 100;
                Assert.IsTrue(dict.Remove(new KeyValuePair<string, string>("key" + removeIdx, "value" + removeIdx)));
                Assert.AreEqual(99 - c, dict.Count);
            }
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryTryGetAndRemove()
        {
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(8);
            string value;
            Assert.IsFalse(dict.TryGetValueAndRemove("notexist", out value));

            for (int c = 0; c < 100; c++)
            {
                dict.Add("key" + c, "value" + c);
            }

            Assert.AreEqual(100, dict.Count);
            Assert.IsFalse(dict.TryGetValueAndRemove("notexist", out value));
            Assert.AreEqual(100, dict.Count);

            for (int c = 0; c < 100; c++)
            {
                int removeIdx = (c + 50) % 100;
                Assert.IsTrue(dict.TryGetValueAndRemove("key" + removeIdx, out value));
                Assert.AreEqual("value" + removeIdx, value);
                Assert.AreEqual(99 - c, dict.Count);
            }
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryTryGet()
        {
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(8);
            string value;
            Assert.IsFalse(dict.TryGetValue("notexist", out value));

            for (int c = 0; c < 100; c++)
            {
                dict.Add("key" + c, "value" + c);
            }

            Assert.IsFalse(dict.TryGetValue("notexist", out value));

            for (int c = 0; c < 100; c++)
            {
                Assert.IsTrue(dict.TryGetValue("key" + c, out value));
                Assert.AreEqual("value" + c, value);
            }
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryTryGetValueOrSet()
        {
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(8);
            string value;

            for (int c = 0; c < 100; c++)
            {
                Assert.IsFalse(dict.TryGetValueOrSet("key" + c, out value, "value" + c));
                Assert.IsTrue(dict.TryGetValueOrSet("key" + c, out value, "value" + c));
                Assert.AreEqual("value" + c, value);
            }

            for (int c = 0; c < 100; c++)
            {
                Assert.IsTrue(dict.TryGetValueOrSet("key" + c, out value, "value" + c));
                Assert.AreEqual("value" + c, value);
            }

            dict.Clear();

            for (int c = 0; c < 100; c++)
            {
                Assert.IsFalse(dict.TryGetValueOrSet("key" + c, out value, () => "value" + c));
                Assert.IsTrue(dict.TryGetValueOrSet("key" + c, out value, () => "value" + c));
                Assert.AreEqual("value" + c, value);
            }

            dict.Clear();

            for (int c = 0; c < 100; c++)
            {
                Assert.IsFalse(dict.TryGetValueOrSet("key" + c, out value, (key, param1) => "value" + param1, c));
                Assert.IsTrue(dict.TryGetValueOrSet("key" + c, out value, (key, param1) => "value" + param1, c));
                Assert.AreEqual("value" + c, value);
            }

            dict.Clear();

            for (int c = 0; c < 100; c++)
            {
                Assert.IsFalse(dict.TryGetValueOrSet("key" + c, out value, (param1, param2) => param1 + param2, "value", c));
                Assert.IsTrue(dict.TryGetValueOrSet("key" + c, out value, (param1, param2) => param1 + param2, "value", c));
                Assert.AreEqual("value" + c, value);
            }
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryVariety()
        {
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(8);
            Assert.AreEqual(0, dict.Count);
            Assert.IsFalse(dict.ContainsKey("notexist"));
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, string>("notexist", "value1")));
            Assert.IsFalse(dict.IsReadOnly);
            dict.Add("key1", "value1");
            Assert.AreEqual(1, dict.Count);
            Assert.IsFalse(dict.ContainsKey("notexist"));
            Assert.IsTrue(dict.ContainsKey("key1"));
            Assert.IsTrue(dict.Contains(new KeyValuePair<string, string>("key1", "value1")));
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, string>("wrongkey", "value1")));
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, string>("key1", "wrongvalue")));

            dict.Add("key2", "value2");
            dict.Add("key3", "value3");
            dict.Add("key4", "value4");

            Assert.AreEqual(4, dict.Count);
            Assert.IsTrue(dict.ContainsKey("key1"));
            Assert.IsTrue(dict.ContainsKey("key2"));
            Assert.IsTrue(dict.ContainsKey("key3"));
            Assert.IsTrue(dict.ContainsKey("key4"));
            Assert.IsFalse(dict.ContainsKey("notexist"));

            string value;
            Assert.IsTrue(dict.TryGetValue("key1", out value));
            Assert.AreEqual("value1", value);
            Assert.IsTrue(dict.TryGetValue("key2", out value));
            Assert.AreEqual("value2", value);
            Assert.IsTrue(dict.TryGetValue("key3", out value));
            Assert.AreEqual("value3", value);
            Assert.IsTrue(dict.TryGetValue("key4", out value));
            Assert.AreEqual("value4", value);
            Assert.IsFalse(dict.TryGetValue("notexist", out value));

            HashSet<string> keySet = new HashSet<string>(dict.Keys);
            Assert.AreEqual(4, keySet.Count);
            Assert.IsTrue(keySet.Contains("key1"));
            Assert.IsTrue(keySet.Contains("key2"));
            Assert.IsTrue(keySet.Contains("key3"));
            Assert.IsTrue(keySet.Contains("key4"));

            HashSet<string> valueSet = new HashSet<string>(dict.Values);
            Assert.AreEqual(4, valueSet.Count);
            Assert.IsTrue(valueSet.Contains("value1"));
            Assert.IsTrue(valueSet.Contains("value2"));
            Assert.IsTrue(valueSet.Contains("value3"));
            Assert.IsTrue(valueSet.Contains("value4"));

            KeyValuePair<string, string>[] array = new KeyValuePair<string, string>[dict.Count];
            dict.CopyTo(array, 0);
            Assert.IsTrue(array.Contains(new KeyValuePair<string, string>("key1", "value1")));
            Assert.IsTrue(array.Contains(new KeyValuePair<string, string>("key2", "value2")));
            Assert.IsTrue(array.Contains(new KeyValuePair<string, string>("key3", "value3")));
            Assert.IsTrue(array.Contains(new KeyValuePair<string, string>("key4", "value4")));

            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            list.AddRange(dict);

            Assert.AreEqual(4, list.Count);
            Assert.IsTrue(list.Contains(new KeyValuePair<string, string>("key1", "value1")));
            Assert.IsTrue(list.Contains(new KeyValuePair<string, string>("key2", "value2")));
            Assert.IsTrue(list.Contains(new KeyValuePair<string, string>("key3", "value3")));
            Assert.IsTrue(list.Contains(new KeyValuePair<string, string>("key4", "value4")));
            Assert.IsFalse(list.Contains(new KeyValuePair<string, string>("key1", "wrongvalue1")));
            Assert.IsFalse(list.Contains(new KeyValuePair<string, string>("key2", "wrongvalue2")));
            Assert.IsFalse(list.Contains(new KeyValuePair<string, string>("key3", "wrongvalue3")));
            Assert.IsFalse(list.Contains(new KeyValuePair<string, string>("key4", "wrongvalue4")));

            Assert.IsTrue(dict.Remove("key1"));
            Assert.AreEqual(3, dict.Count);
            Assert.IsFalse(dict.Remove("key1"));
            Assert.AreEqual(3, dict.Count);

            Assert.IsTrue(dict.Remove("key4"));
            Assert.IsTrue(dict.Remove("key2"));
            Assert.IsTrue(dict.Remove("key3"));
            Assert.AreEqual(0, dict.Count);

            list.Clear();
            list.AddRange(dict);
            Assert.AreEqual(0, list.Count);

            dict["key1"] = "value1";
            dict["key1"] = "value2";
            dict["key1"] = "value3";
            dict["key2"] = "value1";
            dict["key2"] = "value2";
            dict["key2"] = "value3";
            Assert.AreEqual(2, dict.Count);
            Assert.AreEqual("value3", dict["key1"]);
            Assert.AreEqual("value3", dict["key2"]);

            dict.Clear();
            Assert.AreEqual(0, dict.Count);

            for (int c = 0; c < 100; c++)
            {
                string key = "key" + c;
                string val = "newValue" + c;
                Assert.IsFalse(dict.TryGetValueOrSet(key, out value, val));
                Assert.AreEqual(val, value);
                Assert.IsTrue(dict.TryGetValueOrSet(key, out value, "SHOULDNT_BE_THIS"));
                Assert.AreEqual(val, value);
                Assert.AreEqual(c + 1, dict.Count);
            }

            try
            {
                dict["notexist"].GetHashCode();
                Assert.Fail("Should have thrown a KeyNotFoundException");
            }
            catch (KeyNotFoundException) { }
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryTableExpansion()
        {
            for (int initialSize = 1; initialSize <= 8; initialSize++)
            {
                FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(initialSize);
                Dictionary<string, string> checkDict = new Dictionary<string, string>();

                for (int c = 0; c < 500; c++)
                {
                    dict.Add("key" + c, "value" + c);
                    Assert.AreEqual(c + 1, dict.Count);
                    Assert.IsTrue(dict.ContainsKey("key" + c));

                    checkDict.Clear();
                    foreach (var kvp in dict)
                    {
                        checkDict.Add(kvp.Key, kvp.Value);
                    }

                    for (int b = 0; b <= c; b++)
                    {
                        Assert.IsTrue(checkDict.Contains(new KeyValuePair<string, string>("key" + b, "value" + b)));
                    }
                }
            }
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryRandomEnumerator()
        {
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(100);
            HashSet<string> enumeratedKeys = new HashSet<string>();
            HashSet<string> enumeratedValues = new HashSet<string>();
            IRandom rand = new FastRandom(12223);

            for (int c = 0; c < 100; c++)
            {
                dict.Add("key" + c, "value" + c);
            }

            // Assert that random iteration always begins in a random place
            enumeratedKeys.Clear();
            for (int loop = 0; loop < 50; loop++)
            {
                // Enumerate the first value only and add it to the set
                var enumerator = dict.GetRandomEnumerator(rand);
                Assert.IsTrue(enumerator.MoveNext());
                if (!enumeratedKeys.Contains(enumerator.Current.Key))
                {
                    enumeratedKeys.Add(enumerator.Current.Key);
                }
            }

            // Assert that the set of first-enumerated values is not just the same value over and over
            Assert.IsTrue(enumeratedKeys.Count > 10);

            // Assert that random iteration always enumerates the entire collection
            for (int loop = 0; loop < 20; loop++)
            {
                enumeratedKeys.Clear();
                enumeratedValues.Clear();
                var enumerator = dict.GetRandomEnumerator(rand);
                while (enumerator.MoveNext())
                {
                    Assert.IsFalse(enumeratedKeys.Contains(enumerator.Current.Key));
                    enumeratedKeys.Add(enumerator.Current.Key);
                    Assert.IsFalse(enumeratedValues.Contains(enumerator.Current.Value));
                    enumeratedValues.Add(enumerator.Current.Value);
                }

                Assert.AreEqual(100, enumeratedKeys.Count);
                Assert.AreEqual(100, enumeratedValues.Count);
            }
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryValueEnumerator()
        {
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(100);
            HashSet<string> enumeratedKeys = new HashSet<string>();
            HashSet<string> enumeratedValues = new HashSet<string>();

            for (int c = 0; c < 100; c++)
            {
                dict.Add("key" + c, "value" + c);
            }

            // Assert that iteration always enumerates the entire collection
            for (int loop = 0; loop < 20; loop++)
            {
                enumeratedKeys.Clear();
                enumeratedValues.Clear();
                var enumerator = dict.GetValueEnumerator();
                while (enumerator.MoveNext())
                {
                    Assert.IsFalse(enumeratedKeys.Contains(enumerator.Current.Key));
                    enumeratedKeys.Add(enumerator.Current.Key);
                    Assert.IsFalse(enumeratedValues.Contains(enumerator.Current.Value));
                    enumeratedValues.Add(enumerator.Current.Value);
                }

                Assert.AreEqual(100, enumeratedKeys.Count);
                Assert.AreEqual(100, enumeratedValues.Count);

                enumerator.Reset();
                while (enumerator.MoveNext())
                {
                    Assert.IsTrue(enumeratedKeys.Contains(enumerator.Current.Key));
                    Assert.IsTrue(enumeratedValues.Contains(enumerator.Current.Value));
                }
            }
        }

        [TestMethod]
        public async Task TestFastConcurrentDictionaryAccessDuringTableResize()
        {
            ILogger logger = new ConsoleLogger();
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(1);
            const int NUM_READ_THREADS = 4;
            const int NUM_ITERATIONS = 10000;
            List<Task> threads = new List<Task>(NUM_READ_THREADS + 1);

            using (CancellationTokenSource cancelTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            using (Barrier barrier = new Barrier(1 + NUM_READ_THREADS)) // read threads + 1 write thread
            {
                CancellationToken testAbort = cancelTokenSource.Token;

                // Write thread
                threads.Add(Task.Run(() =>
                {
                    IRandom rand = new FastRandom();
                    for (int iter = 0; iter < NUM_ITERATIONS; iter++)
                    {
                        barrier.SignalAndWait(testAbort);
                        dict[rand.NextInt(0, NUM_ITERATIONS).ToString()] = rand.NextInt(0, 100).ToString();
                    }
                }));

                for (int threadId = 0; threadId < NUM_READ_THREADS; threadId++)
                {
                    // Read thread
                    threads.Add(Task.Run(() =>
                    {
                        IRandom rand = new FastRandom();
                        for (int iter = 0; iter < NUM_ITERATIONS; iter++)
                        {
                            int nextOp = rand.NextInt(0, 100);
                            barrier.SignalAndWait(testAbort);
                            if (nextOp < 10)
                            {
                                // Enumerate (5% chance)
                                foreach (var kvp in dict)
                                {
                                    kvp.Value.GetHashCode();
                                }
                            }
                            else if (nextOp < 15)
                            {
                                // ValueEnumerate (5% chance)
                                var valueEnumerator = dict.GetValueEnumerator();
                                while (valueEnumerator.MoveNext())
                                {
                                    valueEnumerator.Current.Value.GetHashCode();
                                }
                            }
                            else if (nextOp < 20)
                            {
                                // ContainsKey (10% chance)
                                dict.ContainsKey(rand.NextInt(0, NUM_ITERATIONS).ToString());
                            }
                            else if (nextOp < 25)
                            {
                                // TryGetValueOrSet (5% chance)
                                string output;
                                dict.TryGetValueOrSet(rand.NextInt(0, NUM_ITERATIONS).ToString(), out output, rand.NextInt(0, 100).ToString());
                            }
                            else if (nextOp < 30)
                            {
                                // Augment (5% chance)
                                dict.Augment(rand.NextInt(0, NUM_ITERATIONS).ToString(), AugmentStringAppend, StubType.Empty);
                            }
                            else if (nextOp < 60)
                            {
                                // Read (40% chance)
                                string x;
                                dict.TryGetValue(rand.NextInt(0, NUM_ITERATIONS).ToString(), out x);
                            }
                            else
                            {
                                // Write (40% chance)
                                dict[rand.NextInt(0, NUM_ITERATIONS).ToString()] = rand.NextInt(0, 100).ToString();
                            }
                        }
                    }));
                }

                foreach (Task t in threads)
                {
                    await t.ConfigureAwait(false);
                }
            }

            // Assert that the dictionary is consistent afterwards
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            list.AddRange(dict);
            Assert.AreEqual(list.Count, dict.Count);
            foreach (KeyValuePair<string, string> kvp in list)
            {
                Assert.IsTrue(dict.Contains(kvp));
            }

            dict.Clear();
            list.Clear();
            list.AddRange(dict);
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public async Task TestFastConcurrentDictionaryThreadSafety()
        {
            ILogger logger = new ConsoleLogger();
            FastConcurrentDictionary<string, string> dict = new FastConcurrentDictionary<string, string>(1);
            const int NUM_THREADS = 16;
            const int NUM_ITERATIONS = 10000;
            List<Task> threads = new List<Task>(NUM_THREADS);

            using (CancellationTokenSource cancelTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            using (Barrier barrier = new Barrier(NUM_THREADS))
            {
                CancellationToken testAbort = cancelTokenSource.Token;

                for (int threadId = 0; threadId < NUM_THREADS; threadId++)
                {
                    // Read thread
                    threads.Add(Task.Run(() =>
                    {
                        IRandom rand = new FastRandom();
                        for (int iter = 0; iter < NUM_ITERATIONS; iter++)
                        {
                            int nextOp = rand.NextInt(0, 100);
                            barrier.SignalAndWait(testAbort);

                            if (nextOp == 0)
                            {
                                // Clear (1% chance)
                                dict.Clear();
                            }
                            else if (nextOp < 5)
                            {
                                // Enumerate (4% chance)
                                foreach (var kvp in dict)
                                {
                                    kvp.Value.GetHashCode();
                                }
                            }
                            else if (nextOp < 10)
                            {
                                // Remove (5% chance)
                                dict.Remove(rand.NextInt(0, 1000).ToString());
                            }
                            else if (nextOp < 20)
                            {
                                // ContainsKey (5% chance)
                                dict.ContainsKey(rand.NextInt(0, 1000).ToString());
                            }
                            else if (nextOp < 25)
                            {
                                // TryGetValueOrSet (5% chance)
                                string output;
                                dict.TryGetValueOrSet(rand.NextInt(0, 1000).ToString(), out output, rand.NextInt(0, 100).ToString());
                            }
                            else if (nextOp < 30)
                            {
                                // Augment (5% chance)
                                dict.Augment(rand.NextInt(0, 1000).ToString(), AugmentStringAppend, StubType.Empty);
                            }
                            else if (nextOp < 60)
                            {
                                // Read (40% chance)
                                string x;
                                dict.TryGetValue(rand.NextInt(0, 1000).ToString(), out x);
                            }
                            else
                            {
                                // Write (40% chance)
                                dict[rand.NextInt(0, 1000).ToString()] = rand.NextInt(0, 100).ToString();
                            }
                        }
                    }));
                }

                foreach (Task t in threads)
                {
                    await t.ConfigureAwait(false);
                }
            }

            // Assert that the dictionary is consistent afterwards
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            list.AddRange(dict);
            Assert.AreEqual(list.Count, dict.Count);
            foreach (KeyValuePair<string, string> kvp in list)
            {
                Assert.IsTrue(dict.Contains(kvp));
            }

            dict.Clear();
            list.Clear();
            list.AddRange(dict);
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void TestFastConcurrentHashSetBasic()
        {
            FastConcurrentHashSet<string> hashset = new FastConcurrentHashSet<string>(8);
            Assert.AreEqual(0, hashset.Count);
            Assert.IsFalse(hashset.Contains("notexist"));
            Assert.IsFalse(hashset.IsReadOnly);
            hashset.Add("key1");
            Assert.AreEqual(1, hashset.Count);
            Assert.IsFalse(hashset.Contains("notexist"));
            Assert.IsTrue(hashset.Contains("key1"));

            hashset.Add("key2");
            hashset.Add("key3");
            hashset.Add("key4");

            Assert.AreEqual(4, hashset.Count);
            Assert.IsTrue(hashset.Contains("key1"));
            Assert.IsTrue(hashset.Contains("key2"));
            Assert.IsTrue(hashset.Contains("key3"));
            Assert.IsTrue(hashset.Contains("key4"));
            Assert.IsFalse(hashset.Contains("notexist"));

            HashSet<string> keySet = new HashSet<string>(hashset);
            Assert.AreEqual(4, keySet.Count);
            Assert.IsTrue(keySet.Contains("key1"));
            Assert.IsTrue(keySet.Contains("key2"));
            Assert.IsTrue(keySet.Contains("key3"));
            Assert.IsTrue(keySet.Contains("key4"));

            string[] array = new string[hashset.Count];
            hashset.CopyTo(array, 0);
            Assert.IsTrue(array.Contains("key1"));
            Assert.IsTrue(array.Contains("key2"));
            Assert.IsTrue(array.Contains("key3"));
            Assert.IsTrue(array.Contains("key4"));

            List<string> list = new List<string>();
            list.AddRange(hashset);

            Assert.AreEqual(4, list.Count);
            Assert.IsTrue(list.Contains("key1"));
            Assert.IsTrue(list.Contains("key2"));
            Assert.IsTrue(list.Contains("key3"));
            Assert.IsTrue(list.Contains("key4"));

            Assert.IsTrue(hashset.Remove("key1"));
            Assert.AreEqual(3, hashset.Count);
            Assert.IsFalse(hashset.Remove("key1"));
            Assert.AreEqual(3, hashset.Count);

            Assert.IsTrue(hashset.Remove("key4"));
            Assert.IsTrue(hashset.Remove("key2"));
            Assert.IsTrue(hashset.Remove("key3"));
            Assert.AreEqual(0, hashset.Count);

            list.Clear();
            list.AddRange(hashset);
            Assert.AreEqual(0, list.Count);

            hashset.Clear();
            Assert.AreEqual(0, hashset.Count);
        }

        [TestMethod]
        public void TestFastConcurrentHashSetTableExpansion()
        {
            for (int initialSize = 1; initialSize <= 8; initialSize++)
            {
                FastConcurrentHashSet<string> dict = new FastConcurrentHashSet<string>(initialSize);
                HashSet<string> checkDict = new HashSet<string>();

                for (int c = 0; c < 500; c++)
                {
                    dict.Add("key" + c);
                    Assert.AreEqual(c + 1, dict.Count);
                    Assert.IsTrue(dict.Contains("key" + c));

                    checkDict.Clear();
                    foreach (var kvp in dict)
                    {
                        checkDict.Add(kvp);
                    }

                    for (int b = 0; b <= c; b++)
                    {
                        Assert.IsTrue(checkDict.Contains("key" + b));
                    }
                }
            }
        }

        [TestMethod]
        public void TestFastConcurrentHashSetRandomEnumerator()
        {
            FastConcurrentHashSet<string> dict = new FastConcurrentHashSet<string>(100);
            HashSet<string> enumeratedKeys = new HashSet<string>();
            IRandom rand = new FastRandom(12223);

            for (int c = 0; c < 100; c++)
            {
                dict.Add("key" + c);
            }

            // Assert that random iteration always begins in a random place
            enumeratedKeys.Clear();
            for (int loop = 0; loop < 50; loop++)
            {
                // Enumerate the first value only and add it to the set
                var enumerator = dict.GetRandomEnumerator(rand);
                Assert.IsTrue(enumerator.MoveNext());
                if (!enumeratedKeys.Contains(enumerator.Current))
                {
                    enumeratedKeys.Add(enumerator.Current);
                }
            }

            // Assert that the set of first-enumerated values is not just the same value over and over
            Assert.IsTrue(enumeratedKeys.Count > 10);

            // Assert that random iteration always enumerates the entire collection
            for (int loop = 0; loop < 20; loop++)
            {
                enumeratedKeys.Clear();
                var enumerator = dict.GetRandomEnumerator(rand);
                while (enumerator.MoveNext())
                {
                    Assert.IsFalse(enumeratedKeys.Contains(enumerator.Current));
                    enumeratedKeys.Add(enumerator.Current);
                }

                Assert.AreEqual(100, enumeratedKeys.Count);
            }
        }

        [TestMethod]
        public void TestFastConcurrentHashSetValueEnumerator()
        {
            FastConcurrentHashSet<string> dict = new FastConcurrentHashSet<string>(100);
            HashSet<string> enumeratedKeys = new HashSet<string>();

            for (int c = 0; c < 100; c++)
            {
                dict.Add("key" + c);
            }

            // Assert that iteration always enumerates the entire collection
            for (int loop = 0; loop < 20; loop++)
            {
                enumeratedKeys.Clear();
                var enumerator = dict.GetValueEnumerator();
                while (enumerator.MoveNext())
                {
                    Assert.IsFalse(enumeratedKeys.Contains(enumerator.Current));
                    enumeratedKeys.Add(enumerator.Current);
                }

                Assert.AreEqual(100, enumeratedKeys.Count);
                enumerator.Reset();
                while (enumerator.MoveNext())
                {
                    Assert.IsTrue(enumeratedKeys.Contains(enumerator.Current));
                }
            }
        }

        [TestMethod]
        public async Task TestFastConcurrentHashSetThreadSafety()
        {
            ILogger logger = new ConsoleLogger();
            FastConcurrentHashSet<string> set = new FastConcurrentHashSet<string>(1);
            const int NUM_THREADS = 16;
            int threadsFinished = 0;
            using (IThreadPool threadPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestPool", NUM_THREADS, false))
            {
                CancellationTokenSource cancelToken = new CancellationTokenSource();
                using (ManualResetEventSlim startingPistol = new ManualResetEventSlim(false))
                {
                    for (int c = 0; c < NUM_THREADS; c++)
                    {
                        threadPool.EnqueueUserWorkItem(() =>
                        {
                            try
                            {
                                IRandom rand = new FastRandom();
                                startingPistol.Wait();
                                while (!cancelToken.IsCancellationRequested)
                                {
                                    int nextOp = rand.NextInt(0, 100);
                                    if (nextOp == 0)
                                    {
                                        // Clear (1% chance)
                                        set.Clear();
                                    }
                                    else if (nextOp < 5)
                                    {
                                        // Enumerate (4% chance)
                                        foreach (var kvp in set)
                                        {
                                            kvp.GetHashCode();
                                        }
                                    }
                                    else if (nextOp < 10)
                                    {
                                        // Enumerate by value (5% chance)
                                        var valueEnumerator = set.GetValueEnumerator();
                                        while (valueEnumerator.MoveNext())
                                        {
                                            valueEnumerator.Current.GetHashCode();
                                        }
                                    }
                                    else if (nextOp < 20)
                                    {
                                        // Remove (10% chance)
                                        set.Remove(rand.NextInt(0, 1000).ToString());
                                    }
                                    else if (nextOp < 70)
                                    {
                                        // Contains (50% chance)
                                        set.Contains(rand.NextInt(0, 1000).ToString());
                                    }
                                    {
                                        // Add (30% chance)
                                        set.Add(rand.NextInt(0, 1000).ToString());
                                    }
                                }
                            }
                            finally
                            {
                                Interlocked.Increment(ref threadsFinished);
                            }
                        });
                    }

                    startingPistol.Set();
                    cancelToken.CancelAfter(TimeSpan.FromSeconds(5));

                    while (threadsFinished < NUM_THREADS)
                    {
                        await Task.Delay(100);
                    }
                }
            }

            // Assert that the dictionary is consistent afterwards
            List<string> list = new List<string>();
            list.AddRange(set);
            Assert.AreEqual(list.Count, set.Count);
            foreach (string item in list)
            {
                Assert.IsTrue(set.Contains(item));
            }

            set.Clear();
            list.Clear();
            list.AddRange(set);
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryAugmentBasic()
        {
            FastConcurrentDictionary<int, int> dict = new FastConcurrentDictionary<int, int>(8);
            for (int c = 0; c < 10; c++)
            {
                CreateAndAugmentKey(dict, 1);
            }
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryAugmentTailOfList()
        {
            FastConcurrentDictionary<int, int> dict = new FastConcurrentDictionary<int, int>(8);

            // Augmented values should be on the tail of the linked lists of their respective bins
            IRandom rand = new FastRandom(12);
            for (int c = 1000; c < 1020; c++)
            {
                dict[c] = 99;
            }

            for (int iter = 0; iter < 1000; iter++)
            {
                CreateAndAugmentKey(dict, rand.NextInt(20));
            }

            List<int> values = dict.Values.ToList();
            Assert.AreEqual(20, values.Count);
            Assert.IsTrue(values.All((i) => i == 99));
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryAugmentDeleteMiddleOfList()
        {
            FastConcurrentDictionary<int, int> dict = new FastConcurrentDictionary<int, int>(8);

            IRandom rand = new FastRandom(12);
            for (int c = 0; c < 1000; c++)
            {
                dict[c] = c;
            }

            for (int iter = 0; iter < 1000; iter++)
            {
                int keyToModify = (iter + 500) % 1000;
                AugmentationResult<int, int> augResult = dict.Augment(keyToModify, AugmentIntDeleteIfOdd, StubType.Empty);
                Assert.IsTrue(augResult.ValueExistedBefore);
                Assert.AreEqual((keyToModify % 2) == 0, augResult.ValueExistsAfter);
                Assert.AreEqual(keyToModify, augResult.Key);
            }

            Assert.AreEqual(500, dict.Count);
        }

        [TestMethod]
        public void TestFastConcurrentDictionaryAugmentDeleteHeadOfList()
        {
            FastConcurrentDictionary<int, int> dict = new FastConcurrentDictionary<int, int>(8);

            // Augmented values should be on the head of the linked lists of their respective bins
            IRandom rand = new FastRandom(12);
            for (int c = 0; c < 20; c++)
            {
                dict[c] = 1;
            }

            for (int c = 1000; c < 1020; c++)
            {
                dict[c] = 99;
            }

            for (int c = 0; c < 20; c++)
            {
                dict.Augment(c, AugmentIntDelete, StubType.Empty);
            }

            List<int> values = dict.Values.ToList();
            Assert.AreEqual(20, values.Count);
            Assert.IsTrue(values.All((i) => i == 99));
        }

        private static void CreateAndAugmentKey(FastConcurrentDictionary<int, int> dict, int keyToModify)
        {
            AugmentationResult<int, int> augResult;
            augResult = dict.Augment(keyToModify, AugmentIntNoOp, StubType.Empty);
            Assert.IsFalse(augResult.ValueExistedBefore);
            Assert.IsFalse(augResult.ValueExistsAfter);
            Assert.AreEqual(keyToModify, augResult.Key);

            augResult = dict.Augment(keyToModify, AugmentIntSetToOne, StubType.Empty);
            Assert.IsFalse(augResult.ValueExistedBefore);
            Assert.IsTrue(augResult.ValueExistsAfter);
            Assert.AreEqual(1, augResult.AugmentedValue);
            Assert.AreEqual(keyToModify, augResult.Key);

            AugmentKey(dict, keyToModify);

            augResult = dict.Augment(keyToModify, AugmentIntDeleteIfOdd, StubType.Empty);
            Assert.IsTrue(augResult.ValueExistedBefore);
            Assert.IsFalse(augResult.ValueExistsAfter);
            Assert.AreEqual(keyToModify, augResult.Key);

            augResult = dict.Augment(keyToModify, AugmentIntNoOp, StubType.Empty);
            Assert.IsFalse(augResult.ValueExistedBefore);
            Assert.IsFalse(augResult.ValueExistsAfter);
            Assert.AreEqual(keyToModify, augResult.Key);
        }

        private static void AugmentKey(FastConcurrentDictionary<int, int> dict, int keyToModify)
        {
            AugmentationResult<int, int> augResult;
            augResult = dict.Augment(keyToModify, AugmentIntSetToOne, StubType.Empty);
            Assert.IsTrue(augResult.ValueExistedBefore);
            Assert.IsTrue(augResult.ValueExistsAfter);
            Assert.AreEqual(1, augResult.AugmentedValue);
            Assert.AreEqual(keyToModify, augResult.Key);

            augResult = dict.Augment(keyToModify, AugmentIntAddOneToValue, StubType.Empty);
            Assert.IsTrue(augResult.ValueExistedBefore);
            Assert.IsTrue(augResult.ValueExistsAfter);
            Assert.AreEqual(2, augResult.AugmentedValue);
            Assert.AreEqual(keyToModify, augResult.Key);

            augResult = dict.Augment(keyToModify, AugmentIntDeleteIfOdd, StubType.Empty);
            Assert.IsTrue(augResult.ValueExistedBefore);
            Assert.IsTrue(augResult.ValueExistsAfter);
            Assert.AreEqual(2, augResult.AugmentedValue);
            Assert.AreEqual(keyToModify, augResult.Key);

            augResult = dict.Augment(keyToModify, AugmentIntAddOneToValue, StubType.Empty);
            Assert.IsTrue(augResult.ValueExistedBefore);
            Assert.IsTrue(augResult.ValueExistsAfter);
            Assert.AreEqual(3, augResult.AugmentedValue);
            Assert.AreEqual(keyToModify, augResult.Key);
        }

        private static void AugmentIntSetToOne(int key, ref bool exists, ref int value, StubType param)
        {
            exists = true;
            value = 1;
        }

        private static void AugmentIntNoOp(int key, ref bool exists, ref int value, StubType param)
        {
        }

        private static void AugmentIntAddOneToValue(int key, ref bool exists, ref int value, StubType param)
        {
            if (exists)
            {
                value += 1;
            }
        }

        private static void AugmentIntDeleteIfOdd(int key, ref bool exists, ref int value, StubType param)
        {
            if (exists && (value % 2) == 1)
            {
                exists = false;
            }
        }

        private static void AugmentIntDelete(int key, ref bool exists, ref int value, StubType param)
        {
            exists = false;
        }

        private static void AugmentStringAppend(string key, ref bool exists, ref string value, StubType param)
        {
            if (exists)
            {
                value = value + "x";
            }
        }
    }
}
