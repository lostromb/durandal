using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// A utility class to pre-run JIT on assemblies in the background
    /// </summary>
    public static class Jitter
    {
        /// <summary>
        /// Runs JIT preparation on all methods of the given assembly, and all referenced assemblies. This runs in the background of the program
        /// on a separate thread.
        /// </summary>
        /// <param name="logger">A logger</param>
        /// <param name="assembly">The base assembly to JIT</param>
        /// <param name="initialDelay">An initial delay to wait before actually executing</param>
        /// <returns>An async task to potentially be waited on</returns>
        public static Task JitAssembly(ILogger logger, Assembly assembly, TimeSpan initialDelay = default(TimeSpan))
        {
            Task returnVal = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(async () =>
                {
                    if (initialDelay != default(TimeSpan))
                    {
                        await Task.Delay(initialDelay).ConfigureAwait(false);
                    }

                    logger.Log("Beginning to run background JIT on assembly \"" + assembly.GetName() + "\" and all its references...");
                    JisAssembliesRecursive(logger, assembly, new HashSet<Assembly>());
                    logger.Log("Background JIT process is completed");
                });

            return returnVal;
        }

        /// <summary>
        /// Recurses through referenced assemblies and runs JIT on all methods
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="assembly"></param>
        /// <param name="loadedAssemblies"></param>
        private static void JisAssembliesRecursive(ILogger logger, Assembly assembly, HashSet<Assembly> loadedAssemblies)
        {
            bool alreadyLoaded = !loadedAssemblies.Add(assembly);
            if (alreadyLoaded)
            {
                return;
            }

            logger.Log("Running JIT on " + assembly.GetName() + "...", LogLevel.Vrb);
            PreJitMethods(assembly, logger);
           
            foreach (AssemblyName curAssemblyName in assembly.GetReferencedAssemblies())
            {
                Assembly nextAssembly = Assembly.Load(curAssemblyName);
#if NETFRAMEWORK
                if (nextAssembly.GlobalAssemblyCache)
                {
                    continue;
                }
#endif

                JisAssembliesRecursive(logger, nextAssembly, loadedAssemblies);
            }
        }

        /// <summary>
        /// Runs JIT on all methods of a given assembly. Note that generic methods cannot be prepared beforehand, so
        /// this can't be completely thorough. Also it may throw noisy error messages when trying to access security-critical methods.
        /// </summary>
        /// <param name="assembly">The assembly to examine</param>
        /// <param name="logger">A logger for the operation</param>
        public static void PreJitMethods(Assembly assembly, ILogger logger)
        {
            Type[] types = assembly.GetTypes();
            foreach (Type curType in types)
            {
                MethodInfo[] methods = curType.GetMethods(
                        BindingFlags.DeclaredOnly |
                        BindingFlags.NonPublic |
                        BindingFlags.Public |
                        BindingFlags.Instance |
                        BindingFlags.Static);

                foreach (MethodInfo curMethod in methods)
                {
                    if (curMethod.IsAbstract ||
                        curMethod.ContainsGenericParameters ||
                        curMethod.Attributes.HasFlag(MethodAttributes.PinvokeImpl))
                    {
                        continue;
                    }

                    try
                    {
                        RuntimeHelpers.PrepareMethod(curMethod.MethodHandle);
                    }
                    catch (PlatformNotSupportedException) { } // mismatch in different .Net core features, etc.
                    catch (TypeAccessException) { } // usually thrown because of security boundary issues
                    catch (MissingMethodException) { } // happens on reference assemblies or strange handles to native methods
                    catch (Exception e)
                    {
                        logger.Log(e, LogLevel.Wrn);
                        // Log and swallow exceptions - this can happen because of assembly load failures when the jitter tries to cross assembly boundaries
                    }
                }
            }
        }
    }
}
