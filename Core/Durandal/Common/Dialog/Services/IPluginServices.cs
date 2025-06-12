using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.File;
using Durandal.Common.LG;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Ontology;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Time;

namespace Durandal.Common.Dialog.Services
{
    /// <summary>
    /// The collection of services provided to each Durandal plugin by the runtime.
    /// This includes things like loggers, plugin config, access to the local filesystem,
    /// access to session and context data, and helpers for OAuth, web data, and cached dialog actions.
    /// </summary>
    public interface IPluginServices
    {
        /// <summary>
        /// The read-only virtual filesystem to use when accessing local plugin data and view files
        /// </summary>
        IFileSystem FileSystem { get; }

        /// <summary>
        /// This is a persistent cache for data that is unique to this user and shared between all domains.
        /// This might contain things like the user's name and profile information, or global access tokens
        /// that can be used to connect to a user's online accounts.
        /// Generally it is read-only for all plugins except for reflection (internal)
        /// </summary>
        InMemoryDataStore GlobalUserProfile { get; }

        /// <summary>
        /// A factory for HTTP clients
        /// </summary>
        IHttpClientFactory HttpClientFactory { get; }

        /// <summary>
        /// The language generation engine provided to this plugin based on data in the lg directory
        /// </summary>
        ILGEngine LanguageGenerator { get; }

        /// <summary>
        /// Retrieves the local Configuration object that is local to the currently running plugin.
        /// This configuration allows individual plugins to store loose values
        /// in a persistent way. The values are global to all users of the plugin.
        /// </summary>
        IConfiguration PluginConfiguration { get; }

        /// <summary>
        /// This is a persistent cache for data for this particular user. It is isolated by userId + domain,
        /// so each user within each domain will have a unique profile. It is normally used to store things like
        /// user preferences for a plugin, or additional context that should be stored between conversations
        /// </summary>
        InMemoryDataStore LocalUserProfile { get; }

        /// <summary>
        /// The tracing logger provided by the framework to this plugin
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// Gets the directory that stores this plugin's global misc data files (dictionaries, databases, etc).
        /// This usually corresponds to /dialogRoot/plugindata/(DOMAIN)
        /// </summary>
        VirtualPath PluginDataDirectory { get; }

        /// <summary>
        /// Gets the directory that stores this plugin's view data (html, css, image resources).
        /// This usually corresponds to /dialogRoot/views/(DOMAIN)
        /// </summary>
        VirtualPath PluginViewDirectory { get; }

        /// <summary>
        /// This is a TEMPORARY cache for storing data that is local to a single conversation.
        /// It is used to persist structs in between queries without having to recreate them each time.
        /// Its data types must be native C# structs because they could require serialization into
        /// some kind of persistence mechanism
        /// </summary>
        InMemoryDataStore SessionStore { get; }

        /// <summary>
        /// The current traceID of this query. This is the same one that is already specified in the logger
        /// </summary>
        Guid? TraceId { get; }

        /// <summary>
        /// A speech recognition engine provided by the framework
        /// </summary>
        ISpeechRecognizerFactory SpeechRecoEngine { get; }

        /// <summary>
        /// A text-to-speech synthesizer provided by the framework
        /// </summary>
        ISpeechSynth TTSEngine { get; }

        /// <summary>
        /// A list of contextual entities, typically coming from LU slot resolution, but potentially other sources as well.
        /// For example, if LU tags "restaurants in Seattle" then it might also include "Seattle" as a fully resolved relational entity inside this collection.
        /// </summary>
        IList<ContextualEntity> ContextualEntities { get; }

        /// <summary>
        /// The context that is used when resolving contextual entities.
        /// </summary>
        KnowledgeContext EntityContext { get; }

        /// <summary>
        /// Used to fetch and inject entities to the global conversation history. This history is user-specific and
        /// shared between all plugins that have recently executed.
        /// </summary>
        IEntityHistory EntityHistory { get; }

        IEntityResolver EntityResolver { get; }

        /// <summary>
        /// Step 1 of the Authorize flow. Accepts an OAuth config, and returns a URI that should be sent to a client
        /// to start the authorization process for that config (the returned URL will link to the 3rd-party auth supplier's page)
        /// </summary>
        /// <param name="authConfig">The OAuth parameters</param>
        /// <param name="userId">The durandal userID of the user making the request</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An auth URI to be invoked on the client that will start the 3rd-party login process</returns>
        Task<Uri> CreateOAuthUri(OAuthConfig authConfig, string userId, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Stashes a bit of data into a temporary (client-visible) cache and returns the URL
        /// </summary>
        /// <param name="data">The raw data to store</param>
        /// <param name="mimeType">The mimetype to tell whoever requests this data from the cache</param>
        /// <param name="lifetime">The desired amount of time to keep this resource in the cache (minimum 5 seconds)</param>
        /// <returns>A URL that an external client can use to fetch the stored data</returns>
        string CreateTemporaryWebResource(ArraySegment<byte> data, string mimeType, TimeSpan? lifetime = null);

        /// <summary>
        /// Deletes any existing auth token issued for the specified user with the specified config
        /// </summary>
        /// <param name="oauthConfig"></param>
        /// <param name="userId"></param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task</returns>
        Task DeleteOAuthToken(OAuthConfig oauthConfig, string userId, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Registers a single dialog action which can be invoked by the user at some point in the future, and returns its ID.
        /// This is used primarily for the ExecuteDelayedDialogAction client action which can invoke an action from the client
        /// </summary>
        /// <param name="action">The action to register</param>
        /// <param name="lifeTime">The time that the action will remain valid in the cache, or null for indefinite</param>
        /// <returns>The  ID of the registered dialog action</returns>
        string RegisterDialogAction(DialogAction action, TimeSpan? lifeTime = null);

        /// <summary>
        /// Registers a URL that, if invoked from the client, will trigger the corresponding dialog action.
        /// You can use this to create links on an HTML page that could carry on stages of a conversation.
        /// </summary>
        /// <param name="action">The action to register</param>
        /// <param name="clientId">The client ID that this action is valid for</param>
        /// <param name="lifeTime">The time that the action will remain valid in the cache, or null for indefinite</param>
        /// <returns>The relative url of the registered action</returns>
        string RegisterDialogActionUrl(DialogAction action, string clientId, TimeSpan? lifeTime = null);

        /// <summary>
        /// Attempts to retrieve an auth token for the given user and configuration name
        /// </summary>
        /// <param name="oauthConfig">The OAuth configuration that was originally used to get this token</param>
        /// <param name="userId">The durandal userID of the user making the request</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The auth token object, or null if the auth token is expired or otherwise invalid</returns>
        Task<OAuthToken> TryGetOAuthToken(OAuthConfig oauthConfig, string userId, CancellationToken cancelToken, IRealTimeProvider realTime);
    }
}