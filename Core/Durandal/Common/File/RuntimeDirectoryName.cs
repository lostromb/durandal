using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.File
{
    /// <summary>
    /// A static list of common directory names used throughout the dialog/LU runtimes.
    /// </summary>
    public static class RuntimeDirectoryName
    {
        /// <summary>
        /// The root directory name for training template data
        /// </summary>
        public static readonly string TRAINING_DIR = "training";

        /// <summary>
        /// The root directory name for validation template data
        /// </summary>
        public static readonly string VALIDATION_DIR = "validation";

        /// <summary>
        /// The root directory name for cache data
        /// </summary>
        public static readonly string CACHE_DIR = "cache";

        /// <summary>
        /// The root directory name for trained models
        /// </summary>
        public static readonly string MODEL_DIR = "models";

        /// <summary>
        /// The root directory name for model-specific configuration
        /// </summary>
        public static readonly string MODELCONFIG_DIR = "modelconfig";

        /// <summary>
        /// The root directory name for canonicalization files
        /// </summary>
        public static readonly string CANONICAL_DIR = "canonical";

        /// <summary>
        /// The root directory name for view data
        /// </summary>
        public static readonly string VIEW_DIR = "views";

        /// <summary>
        /// The root directory name for functional validation test definitions
        /// </summary>
        public static readonly string FVT_DIR = "fvt";

        /// <summary>
        /// The root directory name for language generation (LG) templates
        /// </summary>
        public static readonly string LG_DIR = "lg";

        /// <summary>
        /// The root directory name for answer plugin configuration files
        /// </summary>
        public static readonly string PLUGINCONFIG_DIR = "pluginconfig";

        /// <summary>
        /// The root directory name for LU training catalogs
        /// </summary>
        public static readonly string CATALOG_DIR = "catalogs";

        /// <summary>
        /// The root directory name for miscellaneous data (used by LU and Dialog)
        /// </summary>
        public static readonly string MISCDATA_DIR = "data";

        /// <summary>
        /// The root directory name for plugin-specific data
        /// </summary>
        public static readonly string PLUGINDATA_DIR = "plugindata";

        /// <summary>
        /// The root directory name for external binary programs
        /// </summary>
        public static readonly string EXT_DIR = "ext";

        /// <summary>
        /// The root directory name for plugins dll files
        /// </summary>
        public static readonly string PLUGIN_DIR = "plugins";

        /// <summary>
        /// The root directory name for packaged plugins
        /// </summary>
        public static readonly string PACKAGE_DIR = "packages";

        /// <summary>
        /// The root directory name for executable container runtimes
        /// </summary>
        public static readonly string RUNTIMES_DIR = "runtimes";
    }
}
