namespace Durandal.Service
{
    using Durandal.API;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using System;

    public class Program
    {
        public static void Main(string[] args)
        {
            string debugString = string.Empty;
#if DEBUG
            debugString = " (DEBUG)";
#endif
            Console.Title = string.Format("Durandal.DialogEngine.NetFramework {0}{1}", SVNVersionInfo.VersionString, debugString);
            LogoUtil.PrintLogo("DialogEngine.NetFramework", Console.Out);
            BigDialogMess.RunDialogService(args).Await();
        }
    }
}
