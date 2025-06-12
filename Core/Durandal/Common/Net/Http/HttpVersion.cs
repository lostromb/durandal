using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Represents a structured HTTP protocol version, including the current 1.0 / 1.1 / 2.0 version as well as
    /// unspecified future revisions.
    /// </summary>
    public class HttpVersion : IEquatable<HttpVersion>, IComparable<HttpVersion>
    {
        /// <summary>
        /// The static, constant value for HTTP/1.0
        /// </summary>
        public static readonly HttpVersion HTTP_1_0 = new HttpVersion(1, 0);

        /// <summary>
        /// The static, constant value for HTTP/1.1
        /// </summary>
        public static readonly HttpVersion HTTP_1_1 = new HttpVersion(1, 1);

        /// <summary>
        /// The static, constant value for HTTP/2.0
        /// </summary>
        public static readonly HttpVersion HTTP_2_0 = new HttpVersion(2, 0);

        /// <summary>
        /// Creates a new HttpVersion.
        /// </summary>
        /// <param name="major">The major version</param>
        /// <param name="minor">The minor version</param>
        public HttpVersion(int major, int minor)
        {
            Major = major;
            Minor = minor;
            AsVersion = new Version(major, minor);
            ProtocolString = string.Format("HTTP/{0}.{1}", major, minor);
        }

        /// <summary>
        /// The major HTTP version.
        /// </summary>
        public int Major { get; private set; }

        /// <summary>
        /// The minor HTTP version.
        /// </summary>
        public int Minor { get; private set; }

        /// <summary>
        /// Gets this object as a System.Version
        /// </summary>
        public Version AsVersion { get; private set; }

        /// <summary>
        /// Gets this object as it appears on the wire, e.g. "HTTP/1.1"
        /// </summary>
        public string ProtocolString { get; private set; }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return Equals(obj as HttpVersion);
        }

        public bool Equals(HttpVersion other)
        {
            return other != null &&
                other.Major == Major &&
                other.Minor == Minor;
        }

        public override int GetHashCode()
        {
            return Major.GetHashCode() ^ (Minor.GetHashCode() << 4);
        }

        public override string ToString()
        {
            return ProtocolString;
        }

        /// <summary>
        /// Parses an HttpVersion object from a pair of chars representing major / minor version integers
        /// </summary>
        /// <param name="major">The major version e.g. '1'</param>
        /// <param name="minor">The minor version e.g. '1'</param>
        /// <returns></returns>
        public static HttpVersion ParseHttpVersion(char major, char minor)
        {
            if (major == '1' && minor == '1')
            {
                return HTTP_1_1;
            }
            else if (major == '2' && minor == '0')
            {
                return HTTP_2_0;
            }
            else if (major == '1' && minor == '0')
            {
                return HTTP_1_0;
            }
            else if (major >= '0' && major <= '9' && minor >= '0' && minor <= '9')
            {
                // HTTP 2.1?
                return new HttpVersion(major - '0', minor - '0');
            }
            else
            {
                throw new FormatException("Non-numeric chars found when parsing HTTP major/minor version");
            }
        }

        /// <summary>
        /// Constructs an HttpVersion object from a pair of major/minor version integers
        /// </summary>
        /// <param name="major">The major version integer</param>
        /// <param name="minor">The minor version integer</param>
        /// <returns>The parsed version</returns>
        public static HttpVersion ParseHttpVersion(int major, int minor)
        {
            if (major == 1 && minor == 1)
            {
                return HTTP_1_1;
            }
            else if (major == 2 && minor == 0)
            {
                return HTTP_2_0;
            }
            else if (major == 1 && minor == 0)
            {
                return HTTP_1_0;
            }
            else
            {
                // HTTP 2.1?
                return new HttpVersion(major, minor);
            }
        }

        /// <summary>
        /// Constructs an HttpVersion object from a string representing major and minor version integers
        /// </summary>
        /// <param name="majorString">The major version string e.g. "1"</param>
        /// <param name="minorString">The minor version string e.g. "1"</param>
        /// <returns>The parsed version</returns>
        public static HttpVersion ParseHttpVersion(string majorString, string minorString)
        {
            if (majorString == "1" && minorString == "1")
            {
                return HTTP_1_1;
            }
            else if (majorString == "2" && minorString == "0")
            {
                return HTTP_2_0;
            }
            else if (majorString == "1" && minorString == "0")
            {
                return HTTP_1_0;
            }
            else
            {
                int major, minor;
                if (!int.TryParse(majorString, NumberStyles.Integer, CultureInfo.InvariantCulture, out major) ||
                    !int.TryParse(minorString, NumberStyles.Integer, CultureInfo.InvariantCulture, out minor))
                {
                    throw new FormatException("Non-numeric chars found when parsing HTTP major/minor version");
                }

                // HTTP 2.1?
                return new HttpVersion(major, minor);
            }
        }

        /// <summary>
        /// Parses an HttpVersion object from an HTTP version string, e.g. "HTTP/1.1"
        /// </summary>
        /// <param name="httpVersionString">The full HTTP version string</param>
        /// <returns>The parsed version</returns>
        public static HttpVersion ParseHttpVersion(string httpVersionString)
        {
            if (httpVersionString.Length != 8)
            {
                throw new FormatException("Cannot parse HTTP version string \"" + httpVersionString + "\": Invalid length");
            }

            if (!StringUtils.SubstringEquals("HTTP/", httpVersionString, 0, StringComparison.Ordinal))
            {
                throw new FormatException("Cannot parse HTTP version string \"" + httpVersionString + "\": Does not begin with HTTP/");
            }

            if (!char.IsDigit(httpVersionString[5]) || !char.IsDigit(httpVersionString[7]))
            {
                throw new FormatException("Cannot parse HTTP version string \"" + httpVersionString + "\": Cannot find version digits");
            }

            return ParseHttpVersion(httpVersionString[5], httpVersionString[7]);
        }

        /// <summary>
        /// Constructs an HttpVersion object from an existing Version struct
        /// </summary>
        /// <param name="v">The version to convert</param>
        /// <returns>The parsed version</returns>
        public static HttpVersion FromVersion(Version v)
        {
            if (v.Major == 1)
            {
                if (v.Minor == 1)
                {
                    return HTTP_1_1;
                }
                else if (v.Minor == 0)
                {
                    return HTTP_1_0;
                }
            }
            else if (v.Major == 2 && v.Minor == 0)
            {
                return HTTP_2_0;
            }

            return new HttpVersion(v.Major, v.Minor);
        }

        public int CompareTo(HttpVersion other)
        {
            other.AssertNonNull(nameof(other));
            int returnVal = Major.CompareTo(other.Major);
            if (returnVal == 0)
            {
                returnVal = Minor.CompareTo(other.Minor);
            }

            return returnVal;
        }

        public static bool operator <(HttpVersion a, HttpVersion b)
        {
            return a.CompareTo(b) < 0;
        }

        public static bool operator >(HttpVersion a, HttpVersion b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool operator <=(HttpVersion a, HttpVersion b)
        {
            return a.CompareTo(b) <= 0;
        }

        public static bool operator >=(HttpVersion a, HttpVersion b)
        {
            return a.CompareTo(b) >= 0;
        }

        public static bool operator ==(HttpVersion a, HttpVersion b)
        {
            return Equals(a, b);
        }

        public static bool operator !=(HttpVersion a, HttpVersion b)
        {
            return !Equals(a, b);
        }
    }
}
