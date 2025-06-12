using Durandal.Common.File;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// Represents information about an installed containerized runtime, usually found in the "/runtimes/____" directory
    /// and used for container versioning
    /// </summary>
    public class ContainerRuntimeInformation
    {
        /// <summary>
        /// Whether this runtime is a development version, meaning it was compiled on the local machine.
        /// </summary>
        public bool IsDevelopmentVersion { get; private set; }

        /// <summary>
        /// The version number of the main Durandal library, for non-dev versions.
        /// </summary>
        public Version RuntimeVersion { get; private set; }

        /// <summary>
        /// The framework used by this runtime, e.g. "netframework" or "netcore".
        /// </summary>
        public string RuntimeFramework { get; private set; }

        /// <summary>
        /// The folder path of this runtime relative to the root environment directory.
        /// </summary>
        public VirtualPath FolderPath { get; private set; }

        /// <summary>
        /// Parses container runtime information from the name of a runtime folder, e.g. "16.0.1-netcore".
        /// </summary>
        /// <param name="folderName">The name of the runtime folder (with no root file paths or anything).</param>
        /// <returns>A parsed runtime information object.</returns>
        public static ContainerRuntimeInformation Parse(string folderName)
        {
            folderName.AssertNonNullOrEmpty(nameof(folderName));

            int split = folderName.IndexOf('-');
            if (split <= 0)
            {
                throw new FormatException("Can't parse runtime folder name " + folderName + ": No hyphen separator found");
            }

            string versionString = folderName.Substring(0, split);
            string frameworkString = folderName.Substring(split + 1);

            if (string.IsNullOrEmpty(frameworkString))
            {
                throw new FormatException("Can't parse runtime folder name " + folderName + ": No framework string");
            }

            if (string.Equals(versionString, "dev", StringComparison.OrdinalIgnoreCase))
            {
                return new ContainerRuntimeInformation()
                {
                    IsDevelopmentVersion = true,
                    RuntimeVersion = new Version(0, 0),
                    FolderPath = new VirtualPath("\\runtimes\\" + folderName),
                    RuntimeFramework = frameworkString
                };
            }
            else
            {
                Version v;
                if (!Version.TryParse(versionString, out v))
                {
                    throw new FormatException("Can't parse runtime folder name " + folderName + ": Cannot parse version string " + versionString);
                }

                return new ContainerRuntimeInformation()
                {
                    IsDevelopmentVersion = false,
                    RuntimeVersion = v,
                    FolderPath = new VirtualPath("\\runtimes\\" + folderName),
                    RuntimeFramework = frameworkString
                };
            }
        }
    }
}
