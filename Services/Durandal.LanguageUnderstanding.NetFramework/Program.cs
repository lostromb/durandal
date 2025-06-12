using System;
using Durandal.API;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;

namespace Durandal.LanguageUnderstanding
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string debugString = string.Empty;
#if DEBUG
            debugString = " (DEBUG)";
#endif
            Console.Title = string.Format("Durandal.LanguageUnderstanding.NetFramework {0}{1}", SVNVersionInfo.VersionString, debugString);
            LogoUtil.PrintLogo("LanguageUnderstanding.NetFramework", Console.Out);
            LanguageUnderstandingMain.RunLUService(args).Await();
        }
    }
}
