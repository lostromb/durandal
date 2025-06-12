using Durandal.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.Utils
{
    public static class LogoUtil
    {
        public static void PrintLogo(string serviceName, TextWriter textWriter)
        {
            textWriter.WriteLine();
            textWriter.WriteLine("              MMNNNNNNM                 ");
            textWriter.WriteLine("         .NNNNNNNNNNNNNNNNN.            ");
            textWriter.WriteLine("        NNNNNNNNNNNNNNNNNNNNN.          ");
            textWriter.WriteLine("      NNNNNNNNNNNNNNNNNNNNNNNNN.        ");
            textWriter.WriteLine("     NNNNNNNNNNNNNNNNM+======+NN        ");
            textWriter.WriteLine("    NNNNNNNNNNNNNNN+=.        .==       ");
            textWriter.WriteLine("   NNNNNNNNNNNNNND+         MNNNI       ");
            textWriter.WriteLine("  NNNNNNNNNNNNNN=       .NNNNNNNNNN     ");
            textWriter.WriteLine(" .NNNNNNNNNNNNNN       .NNNNNNNNNNNNM   ");
            textWriter.WriteLine("  NNNNNNNNNNNNN=       NNNNNNNNNNNNNN.  ");
            textWriter.WriteLine(" .NNNNNNNNNNNNN.       NNNNNNNNNNNNNNN  ");
            textWriter.WriteLine(" .NNNNNNNNNNNNN.       NNNNNNNNNNNNNNN  ");
            textWriter.WriteLine("  NNNNNNNNNNNNN        NNNNNNNNNNNNNN+  ");
            textWriter.WriteLine("  NNNNNNNNNNNNNN       +NNNNNNNNNNNNN   ");
            textWriter.WriteLine("  +NNNNNNNNNNNNNN      .=NNNNNNNNNN=    ");
            textWriter.WriteLine("   =NNNNNNNNNNNNNN        ==NMNNN==     ");
            textWriter.WriteLine("    INNNNNNNNNNNNNNN          . .       ");
            textWriter.WriteLine("     =MNNNNNNNNNNNNNNNN+   .?NM=        ");
            textWriter.WriteLine("      =MNNNNNNNNNNNNNNNNNNNNN++         ");
            textWriter.WriteLine("       .=?NNNNNNNNNNNNNNNNN=+           ");
            textWriter.WriteLine("         .===NNNNNNNNNNN===.            ");
            textWriter.WriteLine("              ========~.                ");
            textWriter.WriteLine();
            textWriter.WriteLine("       Durandal {0} v{1}", serviceName, SVNVersionInfo.VersionString);
            textWriter.WriteLine();
        }
    }
}
