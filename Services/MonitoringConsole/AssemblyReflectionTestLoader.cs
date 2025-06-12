using Durandal.Common.Logger;
using Durandal.Common.Monitoring;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using Durandal.MonitorConsole.Monitors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MonitorConsole
{
    public class AssemblyReflectionTestLoader
    {
        public async Task Load(IList<IServiceMonitor> monitors, ILogger logger)
        {
            await DurandalTaskExtensions.NoOpTask;
            Type expectedBaseType = typeof(IServiceMonitor);
            Assembly assembly = Assembly.GetExecutingAssembly();

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

                    bool implementsInterface = false;
                    foreach (Type implementedIface in typeInfo.ImplementedInterfaces)
                    {
                        if (implementedIface == expectedBaseType)
                        {
                            implementsInterface = true;
                            break;
                        }
                    }

                    if (!implementsInterface)
                    {
                        continue;
                    }

                    logger.Log("Found exported type " + t.FullName + " inside " + assembly.FullName, LogLevel.Vrb);
                    IServiceMonitor reflectedObject = null;
                    if (t.GetConstructor(Type.EmptyTypes) != null)
                    {
                        try
                        {
                            reflectedObject = Activator.CreateInstance(t) as IServiceMonitor;
                        }
                        catch (TargetInvocationException e)
                        {
                            logger.Log("A dll invocation exception occurred while loading type \"" + t.FullName + "\"", LogLevel.Err);
                            logger.Log(e, LogLevel.Err);
                        }
                    }
                    else
                    {
                        logger.Log("Cannot invoke default constructor for type \"" + t.FullName + "\"", LogLevel.Err);
                    }

                    if (reflectedObject != null)
                    {
                        monitors.Add(reflectedObject);
                    }
                }
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
        }
    }
}
