namespace Durandal.Common.Config.Annotation
{
    using System;

    /// <summary>
    /// Annotation which provides a "Type=" field, use internally by the system for consistency
    /// </summary>
    public class TypeAnnotation : ConfigAnnotation
    {
        private ConfigValueType _valueType;
        
        public TypeAnnotation() : base("Type")
        {
        }

        public ConfigValueType ValueType
        {
            get
            {
                return _valueType;
            }
        }

        public TypeAnnotation(ConfigValueType type)
            : base("Type")
        {
            _valueType = type;
        }

        public override bool ParseValue(string inputValue)
        {
            return Enum.TryParse(inputValue, out _valueType);
        }

        public override string GetStringValue()
        {
            return _valueType.ToString();
        }
    }
}
