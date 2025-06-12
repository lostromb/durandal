using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.API
{
    public static class SVNVersionInfo
    {
        public const int MajorVersion = 22;
        public const int MinorVersion = 0;
        public const string Revision = "4669";
        public const string BuildDate = "2025/02/26 14:59:39";
        public const string AssemblyVersion = "22.0.4669.0";
        
        public static string VersionString
        {
            get
            {
                return string.Format("{0}.{1}-{2}", MajorVersion, MinorVersion, Revision);
            }
        }
    }
}
