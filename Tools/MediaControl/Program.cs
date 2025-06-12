using Durandal.Common.Logger;
using Durandal.MediaProtocol;
using Durandal.Common.Utils.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.API.Utils;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.Utils.IO;

namespace MediaControl
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ILogger logger = new ConsoleLogger();
            IResourceManager localDataManager = new FileResourceManager(logger.Clone("LocalFiles"), null);
            DirectoryInfo musicLibrary = new DirectoryInfo(@"S:\Music");
            DirectoryInfo cacheDir = new DirectoryInfo(@"C:\Code\Durandal\Tools\MediaControl\bin");
            NLPTools nlTools = new NLPTools();
            nlTools.Pronouncer = new EnglishPronouncer(new ResourceName("cmu-pronounce-ipa.dict"), new ResourceName("pron.cache"), logger.Clone("Pronouncer"), localDataManager);
            nlTools.WordBreaker = new EnglishWholeWordBreaker();
            nlTools.FeaturizationWordBreaker = new EnglishWordBreaker();
            WinampController winamp = new WinampController(logger.Clone("WinampController"), musicLibrary, cacheDir, nlTools);

            while (true)
            {
                string query = Console.ReadLine();

                MediaControlRequest request = new MediaControlRequest()
                {
                    Commands = new List<MediaCommand>()
                    {
                        new ClearPlaylistMediaCommand(),
                        new EnqueueMediaCommand()
                        {
                            Artist = query
                        },
                        new StopMediaCommand()
                    }
                };

                MediaControlResponse response = winamp.Process(request).Await();
            }

            //MediaControlServer server = new MediaControlServer(1111, logger.Clone("MediaControlServer"), winamp);
            //server.StartServer("MediaControlHttp");
            //while (server.Running)
            //{
            //    Thread.Sleep(1000);
            //}
        }
    }
}
