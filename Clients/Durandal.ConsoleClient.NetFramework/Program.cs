using Durandal.API;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.ConsoleClient
{
    public class Program
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
