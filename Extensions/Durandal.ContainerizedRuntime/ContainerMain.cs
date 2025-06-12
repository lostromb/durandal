using Durandal.Common.Remoting;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.ContainerizedRuntime
{
    /// <summary>
    /// Program which is intended to run as a standalone plugin container.
    /// The command line argument is ContainerInitializationParameters, serialized to json, and then converted to a single base64 string,
    /// </summary>
    public static class ContainerMain
    {
        public static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("This .exe is designed to support process-level isolation of dialog plugins. Don't run it directly.");
                Environment.Exit(-1);
                return;
            }

            // Configure global runtime environment
            System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
            ServicePointManager.Expect100Continue = false;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DefaultRealTimeProvider.HighPrecisionWaitProvider = new Win32HighPrecisionWaitProvider();
            }

            byte[] bytes = Convert.FromBase64String(args[0]);
            string jsonString = Encoding.UTF8.GetString(bytes);
            ProcessContainerGuestInitializationParams initializationParameters = JsonConvert.DeserializeObject<ProcessContainerGuestInitializationParams>(jsonString);
            Console.Title = "Durandal Container " + initializationParameters.ContainerName;
            using (ProcessContainerGuest guest = new ProcessContainerGuest())
            {
                guest.Run(initializationParameters).Await();
            }
        }
    }
}
