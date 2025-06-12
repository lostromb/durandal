using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.ServiceMgmt
{
    /// <summary>
    /// Static debug class that is called from every object finalizer in the runtime.
    /// The reasoning behind this is that any object which ends up in the finalization
    /// queue was not actually properly disposed. A debugger can look at the <see cref="LiveObjectsCounter"/> 
    /// to see what type of objects these are and track them.
    /// </summary>
    public static class DebugMemoryLeakTracer
    {
        /// <summary>
        /// Global counter tracking how many disposable objects are currently allocated by type - useful in long-term debugging to find what types occupy the most memory
        /// </summary>        
        public static readonly Counter<Type> LiveObjectsCounter = new Counter<Type>();

        /// <summary>
        /// Dictionary of weak references, used to carry over each object's creation stack trace to its disposal to backtrace where leaks originated
        /// </summary>
        public static readonly Dictionary<WeakReference, string> ActiveDisposableObjects = new Dictionary<WeakReference, string>(new WeakRefEqualityComparer());

        [Conditional("DEBUG")]
        public static void TraceDisposableItemCreated(IDisposable obj)
        {
            lock (ActiveDisposableObjects)
            {
                WeakReference reference = new WeakReference(obj, true);
                if (!ActiveDisposableObjects.ContainsKey(reference))
                {
                    string stackTrace = "Unknown";
#if NETFRAMEWORK
                    stackTrace = ExtractConstructorCallerStackFrame(new System.Diagnostics.StackTrace().ToString(), obj.GetType());
#endif
                    ActiveDisposableObjects.Add(reference, stackTrace);
                    LiveObjectsCounter.Increment(obj.GetType());
                }
                else
                {
#if NETFRAMEWORK
                    Console.WriteLine("Misconfigured code: " + obj.GetType().Name + " registered twice with the memory leak tracer");
#endif
                    Debug.WriteLine("Misconfigured code: " + obj.GetType().Name + " registered twice with the memory leak tracer");
                }
            }
        }

        [Conditional("DEBUG")]
        public static void TraceDisposableItemDisposed(object obj, bool disposing)
        {
            lock (ActiveDisposableObjects)
            {
                string stackTrace;
                WeakReference reference = new WeakReference(obj, true);
                if (ActiveDisposableObjects.TryGetValue(reference, out stackTrace))
                {
                    if (!disposing)
                    {
#if NETFRAMEWORK
                        Console.WriteLine("Memory leak detected: " + obj.GetType().Name + " created in " + stackTrace + " was never disposed");
#endif
                        Debug.WriteLine("Memory leak detected: " + obj.GetType().Name + " created in " + stackTrace + " was never disposed");
                    }

                    ActiveDisposableObjects.Remove(reference);
                    LiveObjectsCounter.Decrement(obj.GetType());
                }
                else
                {
#if NETFRAMEWORK
                    Console.WriteLine("Misconfigured code: " + obj.GetType().Name + " did not register with the memory leak tracer");
#endif
                    Debug.WriteLine("Misconfigured code: " + obj.GetType().Name + " did not register with the memory leak tracer");
                }
            }
        }

        /// <summary>
        /// Parses a clr stack frame and returns the line of the trace which is one frame above the constructor of an object of the given type.
        /// This tries to account for things like private constructors 
        /// </summary>
        /// <param name="stackTrace"></param>
        /// <param name="objectBeingCreated"></param>
        /// <returns></returns>
        private static string ExtractConstructorCallerStackFrame(string stackTrace, Type objectBeingCreated)
        {
            string objectTypeName = objectBeingCreated.Name;

            string traceLine = "Unknown";
            int startIdx = 0;
            while (startIdx < stackTrace.Length)
            {
                startIdx = stackTrace.IndexOf('\n', startIdx);
                if (startIdx < 0)
                {
                    break;
                }

                startIdx += 7; // trim the "   at " prefix

                int endIdx = stackTrace.IndexOf('\n', startIdx) - 1;
                if (endIdx < 0)
                {
                    endIdx = stackTrace.Length;
                }

                traceLine = stackTrace.Substring(startIdx, endIdx - startIdx);
                if (!traceLine.Contains(objectTypeName))
                {
                    break;
                }
            }

            return traceLine;
        }

        private class WeakRefEqualityComparer : IEqualityComparer<WeakReference>
        {
            public bool Equals(WeakReference x, WeakReference y)
            {
                if (x.IsAlive && y.IsAlive)
                {
                    return object.ReferenceEquals(x.Target, y.Target);
                }
                else
                {
                    return false;
                }
            }

            public int GetHashCode(WeakReference obj)
            {
                if (obj.IsAlive)
                {
                    return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Target);
                }
                else
                {
                    return 0;
                }
            }
        }
    }
}