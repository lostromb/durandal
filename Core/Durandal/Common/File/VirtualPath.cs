using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.File
{
    /// <summary>
    /// Identifies a resource (a file, container, or file-like entity) that can be accessed via an IFileSystem.
    /// All resource names should be in a standard file path form with no trailing slash: example "\directory\subdir\file.dat" or just "file.dat".
    /// The path separator for virtual paths is always normalized to \.
    /// </summary>
    [JsonConverter(typeof(JsonConverter_Local))]
    public class VirtualPath
    {
        private const string WILDCARD = "*";
        private static readonly char[] PATH_TRIM_CHARS = new char[] { '\\', '/' };
        public const char PATH_SEPARATOR_CHAR = '\\';
        public const string PATH_SEPARATOR_STR = "\\";

        /// <summary>
        /// Decide whether to use case-sensitive comparison for paths on the currently running OS
        /// </summary>
        private static readonly StringComparison PATH_COMPARISON_FUNC = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        public static string PathSeparator => PATH_SEPARATOR_STR;

        private string _name;

        /// <summary>
        /// Creates a new resource identifier. Valid paths look like this:
        /// "file1.dat"
        /// "dir\file1.dat"
        /// "\dir\subdir"
        /// "\dir\subdir\anotherdir\file"
        /// "\"
        /// </summary>
        /// <param name="val">The path to the resource</param>
        public VirtualPath(string val)
        {
            _name = NormalizePath(val);
        }
        
        /// <summary>
        /// The full path and file name of this resource, including extension
        /// e.g. "\dir1\dir2\file.ext"
        /// </summary>
        public string FullName
        {
            get
            {
                return _name;
            }

            set
            {
                _name = NormalizePath(value);
            }
        }

        /// <summary>
        /// The name of this file or container, with extension if present
        /// e.g. "file.ts"
        /// </summary>
        public string Name
        {
            get
            {
                if (_name.Contains(PATH_SEPARATOR_STR))
                {
                    return _name.Substring(_name.LastIndexOf(PATH_SEPARATOR_CHAR) + 1);
                }
                //if (_name.Contains("/"))
                //    return _name.Substring(_name.LastIndexOf('/') + 1);
                return _name;
            }
        }

        /// <summary>
        /// The name of this file without its extension
        /// e.g. "file"
        /// </summary>
        public string NameWithoutExtension
        {
            get
            {
                string name = Name;
                int i = name.LastIndexOf('.');
                if (i > 0)
                {
                    return name.Substring(0, i);
                }

                return name;
            }
        }

        /// <summary>
        /// The file extension including the dot, or empty string if there is no extension
        /// e.g. ".dat"
        /// </summary>
        public string Extension
        {
            get
            {
                if (_name.Contains(".") &&
                    (!_name.Contains(PATH_SEPARATOR_STR) || _name.LastIndexOf(PATH_SEPARATOR_CHAR) < _name.LastIndexOf('.')))
                {
                    return _name.Substring(_name.LastIndexOf('.'));
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the virtual path of the container for this object.
        /// e.g. "\dir1\dir2\file.dat" => "\dir1\dir2", "\dir1\dir2" => "\dir1"
        /// </summary>
        public VirtualPath Container
        {
            get
            {
                if (_name.Length > 1 && _name.IndexOf(PATH_SEPARATOR_CHAR, 1) > 0)
                {
                    return new VirtualPath(_name.Substring(0, _name.LastIndexOf(PATH_SEPARATOR_CHAR)));
                }
                //if (_name.Contains("/"))
                //{
                //    return _name.Substring(0, _name.LastIndexOf('/'));
                //}

                return Root;
            }
        }

        /// <summary>
        /// Returns true if this name is equal to "\", which is the root path
        /// </summary>
        public bool IsRoot
        {
            get
            {
                return string.Equals(_name, PATH_SEPARATOR_STR);
            }
        }

        /// <summary>
        /// Returns the path that represents the root of any virtual filesystem
        /// </summary>
        public static VirtualPath Root
        {
            get
            {
                return new VirtualPath(PATH_SEPARATOR_STR);
            }
        }

        /// <summary>
        /// Raw concatenation of virtual paths. You can append extra characters onto a file or directory name, or specify new subdirectories by appending "/dir...".
        /// If you explicitly want to append a new subdirectory it is safer to use Combine() instead.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static VirtualPath operator +(VirtualPath left, string right)
        {
            if (string.Equals(right, "\\") || string.Equals(right, "/"))
            {
                throw new ArgumentException("Appending only a / to a VirtualPath is a no-op so it's probably not doing what you think it's doing");
            }

            // Detect if this current path is the root path
            if (left.IsRoot)
            {
                return new VirtualPath(right);
            }
            else
            {
                return new VirtualPath(left.FullName + right);
            }
        }

        /// <summary>
        /// Behaves similar to Path.Combine(). Appends the specified path fragment as a sub-resource of the current directory.
        /// For example, "/dir".Combine("file.dat") => "/dir/file.dat"
        /// </summary>
        /// <param name="toAdd"></param>
        /// <returns></returns>
        public VirtualPath Combine(string toAdd)
        {
            // Detect if this current path is the root path
            toAdd = toAdd.TrimStart(PATH_TRIM_CHARS);

            if (this.IsRoot)
            {
                return new VirtualPath(toAdd);
            }
            else
            {
                return new VirtualPath(this.FullName + PATH_SEPARATOR_CHAR + toAdd);
            }
        }

        /// <summary>
        /// Returns true if this current path is contained within a smaller base path, or if the two paths are equal.
        /// </summary>
        /// <param name="basePath">The base path to check for.</param>
        /// <returns>True if this path is equal to or a subset of the given base path.</returns>
        public bool IsSubsetOf(VirtualPath basePath)
        {
            return this.FullName.Length >= basePath.FullName.Length &&
                    this.FullName.AsSpan().Slice(0, basePath.FullName.Length).Equals(basePath.FullName.AsSpan(), PATH_COMPARISON_FUNC);
        }
        
        public override string ToString()
        {
            return FullName;
        }
        
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            VirtualPath other = obj as VirtualPath;

            // On Windows paths are case-insensitive. However, we are going to adhere to the stricter behavior and say that virtual paths ARE case-sensitive.
            return string.Equals(_name, other._name, StringComparison.Ordinal);
        }
        
        public override int GetHashCode()
        {
            return _name.GetHashCode();
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A virtual path cannot be a null or empty string");
            }
            //if (path.StartsWith(".") || path.EndsWith("."))
            //{
            //    throw new ArgumentException("A resource identifier cannot begin or end with a period: " + path);
            //}
            if (path.Contains(WILDCARD))
            {
                throw new ArgumentException("A virtual path should not use wildcards: " + path);
            }

            string formattedPath = path.Replace('/', '\\');
            if (!string.Equals(PATH_SEPARATOR_STR, formattedPath))
            {
                formattedPath = formattedPath.TrimEnd(PATH_SEPARATOR_CHAR);
            }
            if (!formattedPath.StartsWith(PATH_SEPARATOR_STR))
            {
                formattedPath = PATH_SEPARATOR_STR + formattedPath;
            }
            
            if (path.Contains("\\\\"))
            {
                throw new ArgumentException("A resource identifier cannot contain an empty path fragment (\"\\\\\"): " + path);
            }

            return formattedPath;
        }

        private class JsonConverter_Local : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(VirtualPath) == objectType;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }
                else if (reader.TokenType == JsonToken.String)
                {
                    string nextString = reader.Value as string;
                    return new VirtualPath(nextString);
                }
                else
                {
                    throw new JsonSerializationException("Could not parse DimensionSet from json");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                }
                else
                {
                    VirtualPath castObject = (VirtualPath)value;
                    if (castObject == null)
                    {
                        writer.WriteNull();
                    }
                    else
                    {
                        writer.WriteValue(castObject.FullName);
                    }
                }
            }
        }
    }
}
