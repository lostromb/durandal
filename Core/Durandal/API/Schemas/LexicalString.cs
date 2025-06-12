using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.API
{
    /// <summary>
    /// Represents a string that has a dual written and spoken form. The distinction is usually for things
    /// such as "2002" (written) and "twenty O two" (spoken), when it's important to preserve the spoken
    /// (sometimes called the non-normalized form) for things like lexical comparison.
    /// </summary>
    public class LexicalString : IEquatable<LexicalString>
    {
        private string _written;
        private string _spoken;

        private int _cachedHashCode = 0;

        /// <summary>
        /// The written (sometimes called normalized form) of this string, as it would appear when written.
        /// </summary>
        [JsonProperty("W")] // to save serialization space, shorten the property names
        public string WrittenForm
        {
            get
            {
                return _written;
            }
            set
            {
                _written = value;
                UpdateHashCode();
            }
        }

        /// <summary>
        /// The spoken (sometimes called non-normalized form) representing the spoken or lexical form. In other
        /// words, this represents how the string sounds.
        /// </summary>
        [JsonProperty("S")]
        public string SpokenForm
        {
            get
            {
                return _spoken;
            }
            set
            {
                _spoken = value;
                UpdateHashCode();
            }
        }

        public LexicalString(string writtenForm, string spokenForm = null)
        {
            _written = writtenForm;
            _spoken = spokenForm;
            UpdateHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            return Equals(obj as LexicalString);
        }
        
        public bool Equals(LexicalString other)
        {
            return string.Equals(other.WrittenForm, WrittenForm, StringComparison.Ordinal) &&
                string.Equals(other.SpokenForm, SpokenForm, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return _cachedHashCode;
        }

        private void UpdateHashCode()
        {
            _cachedHashCode = 0;
            if (WrittenForm != null)
            {
                _cachedHashCode ^= WrittenForm.GetHashCode();
            }

            if (SpokenForm != null)
            {
                _cachedHashCode = (_cachedHashCode << 4) ^ SpokenForm.GetHashCode();
            }
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(SpokenForm))
            {
                return WrittenForm;
            }
            else
            {
                return WrittenForm + "/" + SpokenForm;
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            if (WrittenForm == null)
            {
                if (SpokenForm == null)
                {
                    // Both null
                    writer.Write((byte)0);
                }
                else
                {
                    // Spoken only
                    writer.Write((byte)2);
                    writer.Write(SpokenForm);
                }
            }
            else
            {
                if (SpokenForm == null)
                {
                    // Written only
                    writer.Write((byte)1);
                    writer.Write(WrittenForm);
                }
                else
                {
                    // Both non-null
                    writer.Write((byte)3);
                    writer.Write(WrittenForm);
                    writer.Write(SpokenForm);
                }
            }
        }

        public static LexicalString Deserialize(BinaryReader reader)
        {
            LexicalString returnVal = new LexicalString(null, null);
            byte flag = reader.ReadByte();
            if ((flag & 0x1) != 0)
            {
                returnVal.WrittenForm = reader.ReadString();
            }
            if ((flag & 0x2) != 0)
            {
                returnVal.SpokenForm = reader.ReadString();
            }

            return returnVal;
        }
    }
}
