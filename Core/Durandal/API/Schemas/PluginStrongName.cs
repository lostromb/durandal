using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public class PluginStrongName
    {
        public string PluginId { get; private set; }
        public int MajorVersion { get; private set; }
        public int MinorVersion { get; private set; }

        [JsonIgnore]
        public Version Version
        {
            get
            {
                return new Version(MajorVersion, MinorVersion);
            }
        }

        public PluginStrongName(string pluginId, int majorVersion, int minorVersion)
        {
            PluginId = pluginId;
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
        }

        // override object.Equals
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            PluginStrongName other = (PluginStrongName)obj;
            return MajorVersion == other.MajorVersion &&
                MinorVersion == other.MinorVersion &&
                string.Equals(PluginId, other.PluginId);
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return PluginId.GetHashCode() +
                (MajorVersion * 18434) +
                (MinorVersion * 89323421);
        }

        public override string ToString()
        {
            return string.Format("{0} v{1}.{2}", PluginId, MajorVersion, MinorVersion);
        }
    }
}
