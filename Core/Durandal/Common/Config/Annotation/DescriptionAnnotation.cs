namespace Durandal.Common.Config.Annotation
{
    /// <summary>
    /// Annotation which provides a "Description=" field
    /// </summary>
    public class DescriptionAnnotation : ConfigAnnotation
    {
        private string _desc;

        public DescriptionAnnotation()
            : base("Description")
        {
        }

        public string Description
        {
            get
            {
                return GetStringValue();
            }
        }

        public DescriptionAnnotation(string description)
            : base("Description")
        {
            _desc = description;
        }

        public override bool ParseValue(string inputValue)
        {
            _desc = inputValue;
            return true;
        }

        public override string GetStringValue()
        {
            return _desc;
        }
    }
}
