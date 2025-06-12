namespace Durandal.Common.Client.Actions
{
    /// <summary>
    /// Base interface for JSON action schemas. This just ensures that they have a common field called Name
    /// </summary>
    public interface IJsonClientAction
    {
        /// <summary>
        /// The name of this action, as a unique plain string
        /// </summary>
        string Name { get; }
    }
}
