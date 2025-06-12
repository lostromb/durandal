using Stromberg.Config;
using Stromberg.Logger;
using Stromberg.Net;
using Stromberg.Utils.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DialogPuppet
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ILogger logger = new ConsoleLogger("Main");
            FileResourceManager localResourceManager = new FileResourceManager(logger);
            Configuration config = new IniFileConfiguration(new NullLogger(), new ResourceName("PuppetServer_config"), localResourceManager, true);
            
            HttpServer server = new PuppetServer(config.GetInt("listenPort"), logger, config);
            server.StartServer("Dialog Puppet Server");
            while (server.IsRunning())
            {
                Thread.Sleep(1000);
            }
        }
    }
}
