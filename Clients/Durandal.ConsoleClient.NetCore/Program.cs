using Durandal;
using Durandal.API;
using Durandal.Common.Client;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.Events;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Security.Client;
using Durandal.Common.Security.Login;
using Durandal.Common.Security.Login.Providers;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.Triggers;
using Durandal.Common.Speech.Triggers.Sphinx;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Extensions.BondProtocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.ConsoleClient
{
    public class MainClass
    {
        public static void Main(string[] args)
        {
            Console.Title = "Durandal Client " + SVNVersionInfo.VersionString;

            Console.WriteLine("Select the type of client:");
            Console.WriteLine("   1) Text console client");
            Console.WriteLine("   2) Audio console client");
            Console.WriteLine("   3) Metronome, text queries");
            Console.WriteLine("   4) Metronome, audio queries");
            string i = Console.ReadLine();
            int selection;
            if (!int.TryParse(i, out selection) ||
                selection < 1 ||
                selection > 4)
            {
                Console.WriteLine("Invalid input; selecting text console");
                selection = 1;
            }

            Task programTask = DurandalTaskExtensions.NoOpTask;
            switch (selection)
            {
                case 1:
                    programTask = new TextClient(args).Run();
                    break;
                case 2:
                    programTask = new AudioClient(args).Run();
                    break;
                case 3:
                    programTask = new MetronomeClient(args, false).Run();
                    break;
                case 4:
                    programTask = new MetronomeClient(args, true).Run();
                    break;
            }

            programTask.Await();
        }
    }
}
