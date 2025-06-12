
namespace Durandal
{
    using Durandal.API;
    using Durandal.Common.Utils;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.Ontology;
    using Durandal.Common.IO;
    using Durandal.Common.Tasks;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// All entry points into an individual plugin are stored as delegates
    /// matching this pattern. This allows for configurable plugin continuations
    /// on multiturn queries, which helps organize the code better.
    /// </summary>
    /// <param name="input">The Language Understanding engine result</param>
    /// <param name="services">A set of helper functions and environment parameters usable by plugins</param>
    /// <returns>A PluginResult struct containing the plugin's return value to report
    /// to the conversation engine</returns>
    public delegate Task<PluginResult> PluginContinuation(QueryWithContext input, IPluginServices services);

    /// <summary>
    /// The main superclass for all types that are able to respond to dialog queries.
    /// </summary>
    public abstract class DurandalPlugin : IDisposable
    {
        private static readonly Regex ID_VALIDATOR = new Regex("^[a-zA-Z0-9_\\-]+$");

        /// <summary>
        /// The plugin ID. Every plugin is associated with a unique ID, which is used to identify it
        /// and connect it with language models, LG, views, etc.
        /// By convention, the plugin ID is always lowercase. It may contain letters, numbers, - and _ only
        /// </summary>
        private readonly string _pluginId = "null";

        /// <summary>
        /// The LU domain. Every plugin is tied to an LU domain.
        /// </summary>
        private readonly string _luDomain = "null";

        /// <summary>
        /// Used to lock access to the ConversationTree and AnswerInformation singleton values
        /// </summary>
        private ReaderWriterLockSlim _singletonLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        /// <summary>
        /// A shared, internal conversation tree that is reused by all callers of this plugin
        /// </summary>
        private IConversationTree _convoTreeSingleton = null;

        /// <summary>
        /// This plugin's cached AnswerInfo, to prevent the data from being recreated over and over
        /// </summary>
        private PluginInformation _answerInfoSingleton = null;

        private int _disposed = 0;

        /// <summary>
        /// Creates a new plugin and registers it with a domain. Most subclasses will simply
        /// have a parameterless constructor invoked using PluginName() : base("pluginId").
        /// This ensures the domain name is fixed at compile time.
        /// With this constructor, the LU domain will be the same as the plugin ID.
        /// </summary>
        /// <param name="pluginId">The globally unique ID of this plugin</param>
        protected DurandalPlugin(string pluginId) : this(pluginId, pluginId) { }

        /// <summary>
        /// Creates a new plugin and registers it with a domain. Most subclasses will simply
        /// have a parameterless constructor invoked using PluginName() : base("pluginId").
        /// This ensures the domain name is fixed at compile time
        /// </summary>
        /// <param name="pluginId">The globally unique ID of this plugin</param>
        /// <param name="luDomain">The language understanding domain that this plugin responds to</param>
        protected DurandalPlugin(string pluginId, string luDomain)
        {
            if (!ID_VALIDATOR.IsMatch(pluginId))
            {
                throw new ArgumentException("The plugin ID \"" + pluginId + "\" is invalid. A plugin ID must be composed of letters, numbers, underscore, and hyphen. (By convention, it should also be lowercase)");
            }

            if (luDomain != null && !ID_VALIDATOR.IsMatch(luDomain))
            {
                throw new ArgumentException("The LU domain \"" + pluginId + "\" is invalid. A domain name must be composed of letters, numbers, underscore, and hyphen. (By convention, it should also be lowercase)");
            }

            _pluginId = pluginId;
            _luDomain = luDomain ?? pluginId;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~DurandalPlugin()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// The default public constructor is not accessible
        /// </summary>
        private DurandalPlugin() { }

        /// <summary>
        /// The unique ID of this plugin
        /// This value must be set at compile-time
        /// </summary>
        public string PluginId
        {
            get
            {
                return _pluginId;
            }
        }

        public string LUDomain
        {
            get
            {
                return _luDomain;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _singletonLock.Dispose();
            }
        }

#region Core Virtual Functions

        /// <summary>
        /// The main entry point into a plugin service. This function is called by the
        /// Conversational Understanding module following a successful recognition and
        /// classification event.
        /// 
        /// The ResponseCode in the returned PluginResult should be used as follows:
        /// - Return "Success" if the plugin believes that it is the target of this request,
        /// and it is capable of handling the entire conversation.
        /// 
        /// - Return "Skip" only if the plugin does not believe it was intended to be fired
        /// (for example, if a search plugin was fired but no query was actually tagged).
        /// Returning a "skip" response code will silently pass along control to the next
        /// most likely plugin in the classification stack. If all installed plugins decline
        /// to answer in this way, the entire utterance is thrown out as side speech.
        /// You may also use "skip" for non-serious errors that should be silently ignored.
        /// However, if a plugin "skips" while it is engaged in a multi-turn conversation,
        /// bad things will happen
        /// 
        /// - Return "Failure" only if the plugin failed to execute in a serious and unexpected
        /// way. Returning a failure error code will signal an audio/visual response to the user
        /// that the service itself is experiencing unexpected problems.
        /// </summary>
        /// <param name="queryWithContext">The complete input query, client context, and session context information</param>
        /// <param name="services">Auxilary services that are provided to each plugin, including object storage</param>
        /// <returns></returns>
        public virtual Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            return Task.FromResult(new PluginResult(Result.Failure)
            {
                ErrorMessage = "Plugin " + nameof(Execute) + "() method is not implemented",
            });
        }

        /// <summary>
        /// This function can allow a little more control for contextual plugins by allowing them to calculate a "tenative" result
        /// and return its confidence. The input of this function is the query, and the output is a structured object representing
        /// this plugin's confidence that it is the proper plugin to handle this query. The "canonical" use case is for a query like
        /// "Play Polygon Sun" which triggers a "Music" and a "Game" plugin. Each plugin will have this chance to evaluate "Okay, have
        /// I heard of a song that matches this title? A game?". The Music plugin, for example, might conclude that no song matches and
        /// return a result with BoostingOption.Suppress, which cedes control of the execution to the Game plugin.
        /// 
        /// As a general practice, plugins which have a "catch-all" behavior, such as a web search or Wikipedia knowledge search, should
        /// not use boosting at all, and should instead always return BoostingOption.NoChange to allow other more specialized plugins to take priority.
        /// If multiple plugins return BoostingOption.Boost for a query, a disambiguation page may be shown which prompts the user
        /// based on the task details that are supplied by this function's return value.
        /// </summary>
        /// <param name="queryWithContext">The query that is about to be sent to dialog engine, with its accompanying context</param>
        /// <param name="services">Common plugin services. Not all services are guaranteed to be provided in this code path!
        /// However, you _can_ rely on SessionStore to store the results of triggering, and you can
        /// retrieve those again at process time so you do not have to perform redundant work</param>
        /// <returns>A result object describing the action that this plugin would perform, and its confidence of being correct. By default,
        /// this returns null to indicate "ignore all triggering"</returns>
        public virtual Task<TriggerResult> Trigger(QueryWithContext queryWithContext, IPluginServices services)
        {
            return Task.FromResult<TriggerResult>(null);
        }

        /// <summary>
        /// Event that is triggered when the plugin is loaded into memory. This may happen multiple
        /// times over the program's lifespan, depending on how the core plugins provider handles
        /// hot-swapping of plugin domains.
        /// Note that SessionStore and IDataStore are not available at load time because they require an active conversation. However,
        /// you should be able to load plugin configuration and external files from the plugindata directory
        /// </summary>
        public virtual Task OnLoad(IPluginServices services)
        {
            return DurandalTaskExtensions.NoOpTask;
        }

        /// <summary>
        /// Event that is triggered when the plugin is unloaded from memory. This is
        /// not necessarily the same as program exit time.
        /// </summary>
        public virtual Task OnUnload(IPluginServices services)
        {
            return DurandalTaskExtensions.NoOpTask;
        }

        /// <summary>
        /// Signals that another plugin intends to call into this one using a cross-domain request.
        /// This plugin is going to be the one to decide whether to honor that request. If not, this
        /// method returns null. If so, this method should return a list of CrossDomainSlot items
        /// representing slot values that would need to be filled in order to satisfy a request
        /// with the given intent. If the calling plugin can provide the necessary slots, then execution
        /// will continue as if this plugin was the one to execute all along. Conversation status is reset
        /// during a cross-domain request, so the target intent must be a valid conversation-starting intent
        /// (based on this plugin's conversation tree).
        /// </summary>
        /// <param name="targetIntent">The intent within this domain that the other plugin wishes to call into</param>
        /// <returns>A set of request parameters describing the slots that would be required to fulfill the request,
        /// or null if this plugin does not wish to honor the exchange</returns>
        public virtual Task<CrossDomainRequestData> CrossDomainRequest(string targetIntent)
        {
            return null;
        }

        /// <summary>
        /// Called while this plugin is reaching out to another plugin via a DomainScope.External conversation
        /// tree node. For example, if this plugin desired to transition into the Weather domain, the runtime
        /// would first call CrossDomainRequest on the weather plugin, and return a set of CrossDomainSlot values.
        /// Then, the runtime will call this method on the current plugin. It is this plugin's job to provide
        /// valid slot values that conform to the CrossDomainSlots that were requested. Afterwards, control over
        /// the conversation is passed on to the external plugin, and multiturn continues as normal in the new domain.
        /// </summary>
        /// <param name="context">A container for the request context, including target domain+intent and the slots that are requested</param>
        /// <param name="pluginServices">Plugin services containing turn history, session context, entity context, etc.</param>
        /// <returns>A list of filled slot values that will be passed to the external plugin, to be used as inputs
        /// for the next turn of the conversation. If null, the request will be canceled and the conversation will end.</returns>
        public virtual Task<CrossDomainResponseData> CrossDomainResponse(CrossDomainContext context, IPluginServices pluginServices)
        {
            return null;
        }

        /// <summary>
        /// Builds a representation of this plugin's possible conversations choices, expressed as a simple graph.
        /// Return null to use the default tree. The tree does not necessarily need to be a fully-spanned graph;
        /// more complicated forms such as forests and state machines are possible.
        /// </summary>
        /// <returns>The conversation tree for this plugin.</returns>
        protected virtual IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            return null;
        }

        /// <summary>
        /// Builds a structure that gives information about this plugin. This includes a description,
        /// an icon, version info, and sample queries. This information is used by the package installer
        /// and the Reflection domain to give info about installed plugins
        /// </summary>
        /// <returns></returns>
        protected virtual PluginInformation GetInformation(IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            return new PluginInformation()
            {
                InternalName = "Unknown plugin",
                MajorVersion = 0,
                MinorVersion = 0,
                LocalizedInfo = new Dictionary<string, LocalizedInformation>(),
                IconPngData = default(ArraySegment<Byte>)
            };
        }

        /// <summary>
        /// Returns a strong name based on this plugin's internal ID and version number. Used primarily to manage multiple versions of a single plugin. Don't touch this
        /// </summary>
        /// <returns></returns>
        public PluginStrongName GetStrongName()
        {
            PluginInformation info = _answerInfoSingleton ?? GetInformation(NullFileSystem.Singleton, VirtualPath.Root);
            return new PluginStrongName(PluginId, info.MajorVersion, info.MinorVersion);
        }

#endregion

#region Auxilary Interface Functions

        /// <summary>
        /// Returns a singleton conversation tree that is associated with this plugin. Don't touch this
        /// </summary>
        /// <returns></returns>
        public IConversationTree GetConversationTreeSingleton(IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            _singletonLock.EnterReadLock();
            try
            {
                if (_convoTreeSingleton != null)
                {
                    return _convoTreeSingleton;
                }
            }
            finally
            {
                _singletonLock.ExitReadLock();
            }

            _singletonLock.EnterWriteLock();
            try
            {
                if (_convoTreeSingleton == null)
                {
                    _convoTreeSingleton = BuildConversationTree(new ConversationTree(LUDomain), pluginFileSystem, pluginDataDirectory);
                }

                return _convoTreeSingleton;
            }
            finally
            {
                _singletonLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Returns a singleton answer information object that is associated with this plugin. The value
        /// is cached so it does not have to be recreated for every dialog call. Don't touch this
        /// </summary>
        /// <returns></returns>
        public PluginInformation GetPluginInformationSingleton(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            _singletonLock.EnterUpgradeableReadLock();
            try
            {
                if (_answerInfoSingleton == null)
                {
                    _singletonLock.EnterWriteLock();
                    _answerInfoSingleton = GetInformation(new ReadOnlyFileSystem(pluginDataManager), pluginDataDirectory);

                    // If no icon was given in code, try looking for a default icon in the data directory
                    if (pluginDataManager != null && pluginDataDirectory != null && 
                        (_answerInfoSingleton.IconPngData == null || _answerInfoSingleton.IconPngData.Count == 0))
                    {
                        VirtualPath defaultIconFile = pluginDataDirectory.Combine("icon.png");
                        if (pluginDataManager.Exists(defaultIconFile))
                        {
                            using (RecyclableMemoryStream targetStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                            {
                                using (Stream sourceStream = pluginDataManager.OpenStream(defaultIconFile, FileOpenMode.Open, FileAccessMode.Read))
                                {
                                    sourceStream.CopyTo(targetStream);
                                }

                                _answerInfoSingleton.IconPngData = new ArraySegment<byte>(targetStream.ToArray());
                            }
                        }
                    }

                    _singletonLock.ExitWriteLock();
                }
                
                return _answerInfoSingleton;
            }
            finally
            {
                _singletonLock.ExitUpgradeableReadLock();
            }
        }

#endregion
    }
}
