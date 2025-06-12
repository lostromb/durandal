namespace Durandal.Common.Config.Annotation
{
    /// <summary>
    /// Annotation which provides a "Category=" field
    /// </summary>
    public class CategoryAnnotation : ConfigAnnotation
    {
        private string _category;

        public CategoryAnnotation()
            : base("Category")
        {
        }

        public string Description
        {
            get
            {
                return GetStringValue();
            }
        }

        public CategoryAnnotation(string category)
            : base("Category")
        {
            _category = category;
        }

        public override bool ParseValue(string inputValue)
        {
            _category = inputValue;
            return true;
        }

        public override string GetStringValue()
        {
            return _category;
        }
    }
}
