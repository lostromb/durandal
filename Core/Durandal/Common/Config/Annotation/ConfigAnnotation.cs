namespace Durandal.Common.Config.Annotation
{
    /// <summary>
    /// Represents a bit of metadata that is attached to the configuration object. Inside of
    /// an .ini file this would appear as a tag like [Sealed] or [StringValue|Hello]
    /// </summary>
    public abstract class ConfigAnnotation
    {
        private string _typeName;
       
        /// <summary>
        /// In instances of a subclass, this method is used to interpret the _value_ of a tag. So for a tag
        /// like "[StringValue|Test]", the string "Test" is used as input to ParseValue()
        /// </summary>
        /// <param name="inputValue">The value portion of this annotation's tag</param>
        /// <returns>True if parsing succeeded</returns>
        public abstract bool ParseValue(string inputValue);

        /// <summary>
        /// Returns this tag's value (not its name) as a plain string. This might require some kind of serialization to compress it into a single-line format
        /// </summary>
        /// <returns></returns>
        public abstract string GetStringValue();

        /// <summary>
        /// Creates a new annotation tag.
        /// </summary>
        /// <param name="typeName">The name of this tag</param>
        public ConfigAnnotation(string typeName)
        {
            _typeName = typeName;
        }

        /// <summary>
        /// Returns the name of this tag, which is its type
        /// </summary>
        /// <returns></returns>
        public string GetTypeName()
        {
            return _typeName;
        }

        public override string ToString()
        {
            string stringValue = GetStringValue();
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                // Value-less [Tag] format 
                return string.Format("[{0}]", GetTypeName());
            }
            else
            {
                // Standard [Key|Value] format
                return string.Format("[{0}|{1}]", GetTypeName(), stringValue);
            }
        }
    }
}
