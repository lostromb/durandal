namespace Durandal.Common.Config
{
    /// <summary>
    /// Specifies the type of a configuration value. This lets us do the proper
    /// typecasting once we've retrieved the key from the config.
    /// </summary>
    public enum ConfigValueType
    {
        String,
        Int,
        Float,
        Binary,
        Bool,
        StringList,
        StringDictionary,
        TimeSpan
    }
}
