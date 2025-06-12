using Durandal.Common.Logger;
using Durandal.Common.Utils.NativePlatform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Durandal.Common.Utils
{
    public static class AssemblyReflector
    {
        /// <summary>
        /// Applies all <see cref="IAccelerator"/> instances found within the given assembly.
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static void ApplyAccelerators(Assembly assembly, ILogger logger)
        {
            GetAccelerators(assembly, logger, (accel, l) => accel.Apply(l));
        }

        /// <summary>
        /// Unapplies all <see cref="IAccelerator"/> instances found within the given assembly.
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static void UnapplyAccelerators(Assembly assembly, ILogger logger)
        {
            GetAccelerators(assembly, logger, (accel, l) => accel.Unapply(l));
        }

        private static void GetAccelerators(Assembly assembly, ILogger logger, Action<IAccelerator, ILogger> delegateToRun)
        {
            TypeInfo expectedBaseType = typeof(IAccelerator).GetTypeInfo();

            foreach (Type t in assembly.ExportedTypes)
            {
                TypeInfo typeInfo = t.GetTypeInfo();
                if (!typeInfo.IsClass || typeInfo.IsAbstract || typeInfo.IsNested || !typeInfo.IsPublic || !expectedBaseType.IsAssignableFrom(typeInfo))
                {
                    continue;
                }

                logger.Log($"Found accelerator {t.FullName} inside {assembly.FullName}", LogLevel.Vrb);
                if (!typeInfo.DeclaredConstructors.Any((s) => s.GetParameters().Length == 0))
                {
                    logger.Log($"Could not apply accelerator {t.FullName} because it does not have a parameterless constructor", LogLevel.Err);
                    continue;
                }

                IAccelerator createdAccelerator = Activator.CreateInstance(t) as IAccelerator;
                delegateToRun(createdAccelerator, logger);
            }
        }

        /// <summary>
        /// Inspects an assembly and returns a list of instantiated instances of all classes
        /// that implement the specified type in that assembly.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assembly"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static IList<T> LoadFromAssembly<T>(Assembly assembly, ILogger logger) where T : class, new()
        {
            List<T> returnVal = new List<T>();
            Type expectedBaseType = typeof(T);

            try
            {
                IEnumerable<Type> allExportedTypes = assembly.ExportedTypes;
                foreach (Type t in allExportedTypes)
                {
                    TypeInfo typeInfo = t.GetTypeInfo();
                    if (!typeInfo.IsClass || typeInfo.IsAbstract || typeInfo.IsNested || !typeInfo.IsPublic)
                    {
                        continue;
                    }

                    // Zoom to the highest type of the inheritance tree (to support abstract and inherited classes)
                    Type rootType = t;
                    while (typeInfo.BaseType != null && typeInfo.BaseType.FullName != null && !typeInfo.BaseType.FullName.Equals("System.Object"))
                    {
                        rootType = typeInfo.BaseType;
                        typeInfo = rootType.GetTypeInfo();
                    }

                    if (rootType == expectedBaseType)
                    {
                        logger.Log("Found exported type " + t.FullName + " inside " + assembly.FullName, LogLevel.Vrb);
                        T reflectedObject = null;
                        try
                        {
                            reflectedObject = Activator.CreateInstance(t) as T;
                        }
                        catch (TargetInvocationException e)
                        {
                            logger.Log("A dll invocation exception occurred while loading type \"" + t.FullName + "\"", LogLevel.Err);
                            logger.Log(e, LogLevel.Err);
                        }

                        if (reflectedObject != null)
                        {
                            returnVal.Add(reflectedObject);
                        }
                    }
                }

                return returnVal;
            }
            catch (BadImageFormatException dllException)
            {
                logger.Log("The assembly " + assembly.FullName + " could not be loaded as a valid DLL!", LogLevel.Err);
                logger.Log(dllException, LogLevel.Err);
            }
            catch (Exception e) // TODO remove blanket catch statement?
            {
                logger.Log("An error occurred while loading types from " + assembly.FullName, LogLevel.Err);
                logger.Log(e, LogLevel.Err);
            }

            return new List<T>();
        }
    }
}
