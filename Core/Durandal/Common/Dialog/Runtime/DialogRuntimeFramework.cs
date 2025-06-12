using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Dialog.Runtime
{
    /// <summary>
    /// Specifies names for common runtime types e.g. "netframework" or "netcore"
    /// </summary>
    public static class DialogRuntimeFramework
    {
        /// <summary>
        /// Specifies a runtime or a code package that runs on .Net Framework 4.7.2 or above
        /// </summary>
        public static string RUNTIME_NETFRAMEWORK = "netframework";

        /// <summary>
        /// Specifies a runtime or a code package that runs on .Net Core 3.1 or above
        /// </summary>
        public static string RUNTIME_NETCORE = "netcore";

        /// <summary>
        /// Implies .net standard 2.0 or below. Cannot be used as an
        /// actual runtime itself, this is just to declare that a
        /// particular package is agnostic to .net core or .net framework
        /// and can run in either.
        /// </summary>
        public static string RUNTIME_PORTABLE = "portable";
    }
}
