using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog
{
    public enum PluginFrameworkLevel
    {
        /// <summary>
        /// .Net Standard 1.0, as well as PCL profile 259
        /// </summary>
        NetStandard10 = 0,

        /// <summary>
        /// .Net Standard 1.1
        /// </summary>
        NetStandard11 = 100,
        
        /// <summary>
        /// .Net Standard 1.2, as well as all PCL profiles
        /// </summary>
        NetStandard12 = 200,

        /// <summary>
        /// .Net Standard 1.3
        /// </summary>
        NetStandard13 = 300,

        /// <summary>
        /// .Net Standard 1.4
        /// </summary>
        NetStandard14 = 400,

        /// <summary>
        /// .Net Standard 1.5
        /// </summary>
        NetStandard15 = 500,

        /// <summary>
        /// .Net Standard 1.6
        /// </summary>
        NetStandard16 = 600,

        /// <summary>
        /// Any desktop .Net framework library (e.g. net4.5)
        /// </summary>
        NetFull = 2000,
    }
}
