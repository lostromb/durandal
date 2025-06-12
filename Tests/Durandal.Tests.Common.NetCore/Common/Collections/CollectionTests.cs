namespace Durandal.Tests.Common.Collections
{
    using Durandal.Common.Collections;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class CollectionTests
    {
        [TestMethod]
        public void TestMultichannelQueue()
        {
            MultichannelQueue<string, string> queue = new MultichannelQueue<string, string>();
            Assert.AreEqual(0, queue.Count);
            queue.Enqueue("key1", "value1");
            Assert.AreEqual(1, queue.Count);
            queue.Enqueue("key1", "value2");
            Assert.AreEqual(1, queue.Count);
            queue.Enqueue("key2", "value3");
            Assert.AreEqual(2, queue.Count);
            queue.Enqueue("key3", "value4");
            Assert.AreEqual(3, queue.Count);

            string k;
            string v;
            Assert.IsTrue(queue.TryDequeue(out k, out v));
            Assert.AreEqual("key1", k);
            Assert.AreEqual("value2", v);
            Assert.AreEqual(2, queue.Count);
            Assert.IsTrue(queue.TryDequeue(out k, out v));
            Assert.AreEqual("key2", k);
            Assert.AreEqual("value3", v);
            Assert.AreEqual(1, queue.Count);
            Assert.IsTrue(queue.TryDequeue(out k, out v));
            Assert.AreEqual("key3", k);
            Assert.AreEqual("value4", v);
            Assert.AreEqual(0, queue.Count);
            Assert.IsFalse(queue.TryDequeue(out k, out v));
        }

        

        [TestMethod]
        public void TestDeque()
        {
            Deque<int> deque = new Deque<int>();

            for (int c = 0; c < 100; c++)
            {
                deque.AddToFront(c);
            }

            for (int c = 0; c < 100; c++)
            {
                Assert.AreEqual(c, deque.RemoveFromBack());
            }

            for (int c = 0; c < 50; c++)
            {
                deque.AddToBack(c);
            }

            for (int c = 0; c < 50; c++)
            {
                Assert.AreEqual(c, deque.RemoveFromFront());
            }

            deque.AddToFront(3);
            deque.AddToBack(2);
            deque.AddToFront(4);
            deque.AddToBack(1);
            deque.AddToFront(5);
            Assert.AreEqual(5, deque.Count);
            Assert.AreEqual(1, deque.PeekBack());
            Assert.AreEqual(5, deque.PeekFront());
            Assert.AreEqual(5, deque.Count);
            Assert.AreEqual(1, deque.PeekBack());
            Assert.AreEqual(5, deque.PeekFront());

            Assert.AreEqual(1, deque.RemoveFromBack());
            Assert.AreEqual(4, deque.Count);
            Assert.AreEqual(2, deque.PeekBack());
            Assert.AreEqual(5, deque.PeekFront());

            Assert.AreEqual(2, deque.RemoveFromBack());
            Assert.AreEqual(3, deque.Count);
            Assert.AreEqual(3, deque.PeekBack());
            Assert.AreEqual(5, deque.PeekFront());

            Assert.AreEqual(3, deque.RemoveFromBack());
            Assert.AreEqual(2, deque.Count);
            Assert.AreEqual(4, deque.PeekBack());
            Assert.AreEqual(5, deque.PeekFront());

            Assert.AreEqual(5, deque.RemoveFromFront());
            Assert.AreEqual(1, deque.Count);
            Assert.AreEqual(4, deque.PeekBack());
            Assert.AreEqual(4, deque.PeekFront());

            Assert.AreEqual(4, deque.RemoveFromFront());
            Assert.AreEqual(0, deque.Count);
        }

        [TestMethod]
        public void TestSmallDictionaryBasic()
        {
            SmallDictionary<string, string> dict = new SmallDictionary<string, string>(8);
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

            keySet = new HashSet<string>(((IReadOnlyDictionary<string, string>)dict).Keys);
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

            valueSet = new HashSet<string>(((IReadOnlyDictionary<string, string>)dict).Values);
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

            dict.Clear();
            Assert.AreEqual(0, dict.Count);

            IEnumerator<KeyValuePair<string, string>> enumerator = dict.GetEnumerator();
            TestAssert.ExceptionThrown<IndexOutOfRangeException>(() => { var z = enumerator.Current; });
            Assert.IsFalse(enumerator.MoveNext());

            dict["key10"] = "value10";
            dict["key11"] = "value11";
            Assert.AreEqual("value10", dict["key10"]);
            Assert.AreEqual("value11", dict["key11"]);
            TestAssert.ExceptionThrown<KeyNotFoundException>(() => { string s = dict["notexist"]; });
            TestAssert.ExceptionThrown<ArgumentException>(() => dict.Add("key10", "value10"));

            Assert.IsTrue(dict.ContainsValue("value10"));
            Assert.IsTrue(dict.ContainsValue("value11"));
            Assert.IsFalse(dict.ContainsValue("notexist"));
            Assert.IsTrue(dict.Remove(new KeyValuePair<string, string>("key10", "value10")));
            Assert.IsFalse(dict.Remove(new KeyValuePair<string, string>("key10", "value10")));
        }

        [TestMethod]
        public void TestSmallDictionaryEnumerables()
        {
            IDictionary<string, string> dict = new SmallDictionary<string, string>();
            dict["key1"] = "value1";
            dict["key2"] = "value2";

            IEnumerator<KeyValuePair<string, string>> enumerator = dict.GetEnumerator();
            TestAssert.ExceptionThrown<IndexOutOfRangeException>(() => { var z = enumerator.Current; });
            TestAssert.ExceptionThrown<IndexOutOfRangeException>(() => { var z = ((System.Collections.IEnumerator)enumerator).Current; });
            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual("key1", enumerator.Current.Key);
            Assert.AreEqual("value1", enumerator.Current.Value);
            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual(new KeyValuePair<string, string>("key2", "value2"), ((System.Collections.IEnumerator)enumerator).Current);
            Assert.IsFalse(enumerator.MoveNext());
            enumerator.Reset();
            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual("key1", enumerator.Current.Key);
            Assert.AreEqual("value1", enumerator.Current.Value);
            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual("key2", enumerator.Current.Key);
            Assert.AreEqual("value2", enumerator.Current.Value);
            Assert.IsFalse(enumerator.MoveNext());

            IEnumerator<string> keysEnumerator = ((IReadOnlyDictionary<string, string>)dict).Keys.GetEnumerator();
            TestAssert.ExceptionThrown<IndexOutOfRangeException>(() => { var z = keysEnumerator.Current; });
            TestAssert.ExceptionThrown<IndexOutOfRangeException>(() => { var z = ((System.Collections.IEnumerator)keysEnumerator).Current; });
            Assert.IsTrue(keysEnumerator.MoveNext());
            Assert.AreEqual("key1", keysEnumerator.Current);
            Assert.IsTrue(keysEnumerator.MoveNext());
            Assert.AreEqual("key2", ((System.Collections.IEnumerator)keysEnumerator).Current);
            Assert.IsFalse(keysEnumerator.MoveNext());
            keysEnumerator.Reset();
            Assert.IsTrue(keysEnumerator.MoveNext());
            Assert.AreEqual("key1", keysEnumerator.Current);
            Assert.IsTrue(keysEnumerator.MoveNext());
            Assert.AreEqual("key2", keysEnumerator.Current);
            Assert.IsFalse(keysEnumerator.MoveNext());

            IEnumerator<string> valueEnumerator = ((IReadOnlyDictionary<string, string>)dict).Values.GetEnumerator();
            TestAssert.ExceptionThrown<IndexOutOfRangeException>(() => { var z = valueEnumerator.Current; });
            TestAssert.ExceptionThrown<IndexOutOfRangeException>(() => { var z = ((System.Collections.IEnumerator)valueEnumerator).Current; });
            Assert.IsTrue(valueEnumerator.MoveNext());
            Assert.AreEqual("value1", valueEnumerator.Current);
            Assert.IsTrue(valueEnumerator.MoveNext());
            Assert.AreEqual("value2", ((System.Collections.IEnumerator)valueEnumerator).Current);
            Assert.IsFalse(valueEnumerator.MoveNext());
            valueEnumerator.Reset();
            Assert.IsTrue(valueEnumerator.MoveNext());
            Assert.AreEqual("value1", valueEnumerator.Current);
            Assert.IsTrue(valueEnumerator.MoveNext());
            Assert.AreEqual("value2", valueEnumerator.Current);
            Assert.IsFalse(valueEnumerator.MoveNext());

            ICollection<string> keys = dict.Keys;
            Assert.AreEqual(2, keys.Count);
            Assert.IsTrue(keys.IsReadOnly);
            Assert.IsTrue(keys.Contains("key1"));
            Assert.IsFalse(keys.Contains("notexist"));
            string[] array = new string[4];
            keys.CopyTo(array, 1);
            Assert.AreEqual("key1", array[1]);
            Assert.AreEqual("key2", array[2]);
            TestAssert.ExceptionThrown<NotSupportedException>(() => keys.Add("test"));
            TestAssert.ExceptionThrown<NotSupportedException>(() => keys.Clear());
            TestAssert.ExceptionThrown<NotSupportedException>(() => keys.Remove("test"));

            ICollection<string> values = dict.Values;
            Assert.AreEqual(2, values.Count);
            Assert.IsTrue(values.IsReadOnly);
            Assert.IsTrue(values.Contains("value1"));
            Assert.IsFalse(values.Contains("notexist"));
            values.CopyTo(array, 1);
            Assert.AreEqual("value1", array[1]);
            Assert.AreEqual("value2", array[2]);
            TestAssert.ExceptionThrown<NotSupportedException>(() => values.Add("test"));
            TestAssert.ExceptionThrown<NotSupportedException>(() => values.Clear());
            TestAssert.ExceptionThrown<NotSupportedException>(() => values.Remove("test"));
        }

        [TestMethod]
        public void TestSmallDictionaryZeroInitialSize()
        {
            SmallDictionary<string, string> dict = new SmallDictionary<string, string>(0);
            Assert.AreEqual(0, dict.Count);
            Assert.IsFalse(dict.ContainsKey("notexist"));
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, string>("notexist", "value1")));
            TestAssert.ExceptionThrown<KeyNotFoundException>(() => { string s = dict["notexist"]; });
            dict.Add("key1", "value1");
            dict.Add("key2", "value2");
            dict["key3"] = "value3";
            dict["key4"] = "value4";
            dict["key5"] = "value5";

            Assert.AreEqual(5, dict.Count);
            Assert.IsTrue(dict.ContainsKey("key1"));
            Assert.IsTrue(dict.ContainsKey("key2"));
            Assert.IsTrue(dict.ContainsKey("key3"));
            Assert.IsTrue(dict.ContainsKey("key4"));
            Assert.IsTrue(dict.ContainsKey("key5"));
        }

        [TestMethod]
        public void TestSmallDictionaryCaseInsensitive()
        {
            SmallDictionary<string, string> dict = new SmallDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dict.Add("key1", "value1");
            dict.Add("KEY2", "value2");

            string val;
            Assert.AreEqual(2, dict.Count);
            Assert.IsTrue(dict.ContainsKey("key1"));
            Assert.IsTrue(dict.ContainsKey("Key1"));
            Assert.IsTrue(dict.ContainsKey("KEY1"));
            Assert.IsTrue(dict.TryGetValue("key1", out val));
            Assert.IsTrue(dict.TryGetValue("KEY1", out val));
            Assert.IsTrue(dict.ContainsKey("key2"));
            Assert.IsTrue(dict.ContainsKey("Key2"));
            Assert.IsTrue(dict.ContainsKey("KEY2"));
            Assert.IsTrue(dict.TryGetValue("key2", out val));
            Assert.IsTrue(dict.TryGetValue("KEY2", out val));
            Assert.IsFalse(dict.TryGetValue("notexist", out val));
            Assert.IsTrue(dict.Remove("KEY1"));
            Assert.IsTrue(dict.Remove("KEY2"));
            Assert.IsFalse(dict.TryGetValue("key1", out val));
            Assert.IsFalse(dict.TryGetValue("key2", out val));
        }

        [TestMethod]
        public void TestSmallDictionaryValidContructors()
        {
            Dictionary<string, string> existingDict = new Dictionary<string, string>();
            existingDict["key"] = "value";

            new SmallDictionary<string, string>();
            new SmallDictionary<string, string>(0);
            new SmallDictionary<string, string>(200);
            new SmallDictionary<string, string>((IEnumerable<KeyValuePair<string, string>>)existingDict);
            new SmallDictionary<string, string>((IReadOnlyCollection<KeyValuePair<string, string>>)existingDict);
            new SmallDictionary<string, string>(StringComparer.Ordinal);
            new SmallDictionary<string, string>(StringComparer.Ordinal, 0);
            new SmallDictionary<string, string>(StringComparer.Ordinal, 6);
            new SmallDictionary<string, string>(StringComparer.Ordinal, (IEnumerable<KeyValuePair<string, string>>)existingDict);
            new SmallDictionary<string, string>(StringComparer.Ordinal, (IReadOnlyCollection<KeyValuePair<string, string>>)existingDict);
            new SmallDictionary<string, string>(StringComparer.Ordinal, (IReadOnlyCollection<KeyValuePair<string, string>>)existingDict);
            new SmallDictionary<string, string>(StringComparer.Ordinal, (IEnumerable<KeyValuePair<string, string>>)existingDict, 10);
            new SmallDictionary<string, string>(StringComparer.Ordinal, (IReadOnlyCollection<KeyValuePair<string, string>>)existingDict, 10);
            new SmallDictionary<string, string>(StringComparer.Ordinal, (IReadOnlyCollection<KeyValuePair<string, string>>)existingDict, 10);
        }

        [TestMethod]
        public void TestSmallDictionaryInvalidContructor()
        {
            TestAssert.ExceptionThrown<ArgumentOutOfRangeException>(() =>
            {
                new SmallDictionary<string, string>(-1);
            });

            TestAssert.ExceptionThrown<ArgumentNullException>(() =>
            {
                IEnumerable<KeyValuePair<string, string>> collection = null;
                new SmallDictionary<string, string>(collection);
            });

            TestAssert.ExceptionThrown<ArgumentNullException>(() =>
            {
                IDictionary<string, string> collection = null;
                new SmallDictionary<string, string>(collection);
            });

            TestAssert.ExceptionThrown<ArgumentNullException>(() =>
            {
                IReadOnlyCollection<KeyValuePair<string, string>> collection = null;
                new SmallDictionary<string, string>(collection);
            });
        }

        [TestMethod]
        public void TestSmallDictionaryNullKeys()
        {
            SmallDictionary<string, string> dict = new SmallDictionary<string, string>();
            dict["key"] = "value";

            TestAssert.ExceptionThrown<ArgumentNullException>(() =>
            {
                dict.Add(null, null);
            });

            TestAssert.ExceptionThrown<ArgumentNullException>(() =>
            {
                dict.Add(null, "something");
            });

            TestAssert.ExceptionThrown<ArgumentNullException>(() =>
            {
                dict.Add(new KeyValuePair<string, string>(null, null));
            });

            TestAssert.ExceptionThrown<ArgumentNullException>(() =>
            {
                dict.Add(new KeyValuePair<string, string>(null, "something"));
            });

            TestAssert.ExceptionThrown<ArgumentNullException>(() =>
            {
                dict[null] = null;
            });

            TestAssert.ExceptionThrown<ArgumentNullException>(() =>
            {
                dict[null] = "something";
            });
        }

        [TestMethod]
        public async Task TestConcurrentQueue()
        {
            const int NUM_TEST_THREADS = 16;
            const int TEST_ITERATIONS = 1000;
            ILogger logger = new ConsoleLogger();

            Durandal.Common.Collections.ConcurrentQueue<int> queue = new Durandal.Common.Collections.ConcurrentQueue<int>();
            using (IThreadPool threadPool = new CustomThreadPool(logger.Clone("ThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, threadCount: NUM_TEST_THREADS))
            {
                int iterator = 0;
                List<int> dequeuedNumbers = new List<int>();
                Barrier clock = new Barrier(NUM_TEST_THREADS + 1);
                for (int thread = 0; thread < NUM_TEST_THREADS; thread++)
                {
                    threadPool.EnqueueUserWorkItem(() =>
                    {
                        List<int> threadLocalDequeuedNumbers = new List<int>();
                        int dequeuedValue;
                        for (int iter = 0; iter < TEST_ITERATIONS; iter++)
                        {
                            clock.SignalAndWait();
                            for (int z = 0; z < 10; z++)
                            {
                                queue.Enqueue(-1);
                                queue.TryDequeue(out dequeuedValue);
                            }

                            queue.Clear();
                            clock.SignalAndWait();
                            queue.Enqueue(Interlocked.Increment(ref iterator) - 1);
                            clock.SignalAndWait();
                            if (queue.TryDequeue(out dequeuedValue))
                            {
                                threadLocalDequeuedNumbers.Add(dequeuedValue);
                            }

                            if (queue.TryDequeue(out dequeuedValue))
                            {
                                threadLocalDequeuedNumbers.Add(dequeuedValue);
                            }

                            lock (dequeuedNumbers)
                            {
                                dequeuedNumbers.AddRange(threadLocalDequeuedNumbers);
                            }

                            threadLocalDequeuedNumbers.Clear();
                        }

                        clock.SignalAndWait();
                    });
                }

                for (int iter = 0; iter < TEST_ITERATIONS; iter++)
                {
                    clock.SignalAndWait();
                    clock.SignalAndWait();
                    clock.SignalAndWait();
                }

                clock.SignalAndWait();
                await threadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                Assert.AreEqual(0, queue.ApproximateCount);

                dequeuedNumbers.Sort();
                int expectedNumbers = TEST_ITERATIONS * NUM_TEST_THREADS;
                Assert.AreEqual(expectedNumbers, dequeuedNumbers.Count);
                for (int c = 0; c < expectedNumbers; c++)
                {
                    Assert.AreEqual(dequeuedNumbers[c], c);
                }
            }
        }

        [TestMethod]
        public void TestStackBufferBasic()
        {
            IRandom rand = new FastRandom();
            byte[] input = new byte[100];
            rand.NextBytes(input);
            byte[] output = new byte[100];
            using (StackBuffer stream = new StackBuffer())
            {
                Assert.AreEqual(0, stream.Available);
                Assert.AreEqual(0, stream.Read(output, 0, 100));

                for (int readIdx = 0; readIdx < 100; readIdx += 10)
                {
                    stream.Write(input, readIdx, 10);
                    Assert.AreEqual(10, stream.Available);
                    Assert.AreEqual(10, stream.Read(output, readIdx, 100));
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(input, readIdx, output, readIdx, 10));
                }
            }
        }

        [TestMethod]
        public void TestStackBufferReadMultipleBlocksAtOnce()
        {
            IRandom rand = new FastRandom();
            byte[] input = new byte[BufferPool<byte>.DEFAULT_BUFFER_SIZE];
            rand.NextBytes(input);
            byte[] output = new byte[5 * BufferPool<byte>.DEFAULT_BUFFER_SIZE];
            using (StackBuffer stream = new StackBuffer())
            {
                for (int test = 1; test < 5; test++)
                {
                    for (int loop = 0; loop < test; loop++)
                    {
                        stream.Write(input, 0, input.Length);
                    }

                    Assert.AreEqual(test * input.Length, stream.Available);
                    Assert.AreEqual(test * input.Length, stream.Read(output, 0, test * input.Length));

                    for (int loop = 0; loop < test; loop++)
                    {
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(input, 0, output, loop * input.Length, input.Length));
                    }
                }
            }
        }

        [TestMethod]
        public void TestStackBufferWriteMultipleBlocksAtOnce()
        {
            IRandom rand = new FastRandom();
            byte[] input = new byte[5 * BufferPool<byte>.DEFAULT_BUFFER_SIZE];
            rand.NextBytes(input);
            byte[] output = new byte[BufferPool<byte>.DEFAULT_BUFFER_SIZE];
            using (StackBuffer stream = new StackBuffer())
            {
                for (int test = 1; test < 5; test++)
                {
                    stream.Write(input, 0, test * output.Length);
                    Assert.AreEqual(test * output.Length, stream.Available);

                    for (int loop = 0; loop < test; loop++)
                    {
                        Assert.AreEqual(output.Length, stream.Read(output, 0, output.Length));
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(input, loop * output.Length, output, 0, output.Length));
                    }
                }
            }
        }

        [TestMethod]
        public void TestStackBufferReadWriteRandom()
        {
            IRandom rand = new FastRandom();
            byte[] input = new byte[20 * BufferPool<byte>.DEFAULT_BUFFER_SIZE];
            byte[] output = new byte[20 * BufferPool<byte>.DEFAULT_BUFFER_SIZE];

            for (int loop = 0; loop < 20; loop++)
            {
                rand.NextBytes(input);

                using (StackBuffer stream = new StackBuffer())
                {
                    int readIdx = 0;

                    // First, fill the buffer with the entire input set
                    stream.Write(input, 0, input.Length);
                    Assert.AreEqual(input.Length, stream.Available);
                    while (readIdx < output.Length)
                    {
                        // Read a random amount
                        int readSize = Math.Min(input.Length - readIdx, rand.NextInt(2, 4096));
                        if (readSize > 0)
                        {
                            readIdx += stream.Read(output, readIdx, readSize);
                        }

                        if (readSize > 2)
                        {
                            // And then put back a random, smaller amount
                            int writeSize = rand.NextInt(0, readSize / 2);
                            if (writeSize > 0)
                            {
                                stream.Write(output, readIdx - writeSize, writeSize);
                                readIdx -= writeSize;
                            }
                        }
                    }

                    Assert.IsTrue(ArrayExtensions.ArrayEquals(input, 0, output, 0, input.Length));
                }
            }
        }

        [TestMethod]
        public void TestStackBufferInvalidArguments()
        {
            byte[] output = new byte[100];
            using (StackBuffer stream = new StackBuffer())
            {
                try
                {
                    stream.Read(null, 0, 1);
                    Assert.Fail("Should have thrown an ArgumentNullException");
                }
                catch (ArgumentNullException) { }

                try
                {
                    stream.Write(null, 0, 1);
                    Assert.Fail("Should have thrown an ArgumentNullException");
                }
                catch (ArgumentNullException) { }

                try
                {
                    stream.Read(output, -1, 1);
                    Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    stream.Read(output, 0, -1);
                    Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    stream.Write(output, -1, 1);
                    Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    stream.Write(output, 0, 0);
                    Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }
    }
}
