namespace Durandal.Common.Config.Annotation
{
    /// <summary>
    /// Annotation which provides a "[GUI]" tag, indicating that this parameter should be controllable from the main user interface
    /// </summary>
    public class GUIAnnotation : ConfigAnnotation
    {
        public GUIAnnotation()
            : base("GUI")
        {
        }

        public override bool ParseValue(string inputValue)
        {
            return true;
        }

        public override string GetStringValue()
        {
            return string.Empty;
        }
    }
}
