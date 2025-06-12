namespace Durandal.Common.Config.Annotation
{
    public class DefaultValueAnnotation : ConfigAnnotation
    {
        private string _value;

        public DefaultValueAnnotation()
            : base("Default")
        {
            _value = string.Empty;
        }

        public DefaultValueAnnotation(string value)
            : base("Default")
        {
            _value = value;
        }

        public override bool ParseValue(string inputValue)
        {
            _value = inputValue;
            return true;
        }

        public override string GetStringValue()
        {
            return _value;
        }
    }
}
