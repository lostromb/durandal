namespace Durandal.API
{
    public enum DomainScope
    {
        /// <summary>
        /// Conversation transition using an utterance in the same domain as the plugin
        /// </summary>
        Local,

        /// <summary>
        /// Conversation transition using an utterance in the common domain
        /// </summary>
        Common,

        /// <summary>
        /// Conversation transition using an utterance in the current domain, but which transitions to a conversation in another domain
        /// </summary>
        External,

        /// <summary>
        ///  Conversation transition using an utterance in the common domain, but which transitions to a conversation in another domain
        /// </summary>
        CommonExternal
    }
}
