using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.Audio;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.LG.Statistical;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.TTS;
using Durandal.Common.File;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.NLP;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Cache;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Dialog
{
    /// <summary>
    /// The collection of parameters and objects needed to instantiate a dialog processing engine.
    /// </summary>
    public class DialogEngineParameters
    {
        /// <summary>
        /// A configuration object for this engine
        /// </summary>
        public DialogConfiguration Configuration { get; set; }
        
        /// <summary>
        /// A class which loads, manages, and executes plugins within the dialog engine. There is a degree of separation between
        /// the plugin provider and the dialog engine because the plugin provider may actually load the plugin objects in a sandbox,
        /// a separate app domain, or they may even live on a remote machine. So the interface for this class only operates by
        /// exchanging plugin strong names and loaded plugin information.
        /// </summary>
        public WeakPointer<IDurandalPluginProvider> PluginProvider { get; set; }

        /// <summary>
        /// A master logger to be used in the dialog engine
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// A mechanism for storing conversation states
        /// </summary>
        public WeakPointer<IConversationStateCache> ConversationStateCache { get; set; }

        /// <summary>
        /// A mechanism for storing cached dialog actions
        /// </summary>
        public WeakPointer<ICache<DialogAction>> DialogActionCache { get; set; }

        /// <summary>
        /// A mechanism for storing cached web data
        /// </summary>
        public WeakPointer<ICache<CachedWebData>> WebDataCache { get; set; }

        /// <summary>
        /// A mechanism for storing and retrieving user profiles
        /// </summary>
        public IUserProfileStorage UserProfileStorage { get; set; }

        public IRealTimeProvider RealTime { get; set; }

        /// <summary>
        /// The name to use for the "common" domain
        /// </summary>
        public string CommonDomainName { get; set; }

        /// <summary>
        /// The name to use for the "side_speech" domain
        /// </summary>
        public string SideSpeechDomainName { get; set; }

        /// <summary>
        /// Initializes the default dialog engine parameters with a given config and plugin provider
        /// </summary>
        /// <param name="config"></param>
        /// <param name="pluginProvider"></param>
        public DialogEngineParameters(DialogConfiguration config, WeakPointer<IDurandalPluginProvider> pluginProvider)
        {
            Configuration = config;
            PluginProvider = pluginProvider;
            Logger = NullLogger.Singleton;
#pragma warning disable CA2000 // Dispose objects before losing scope
            ConversationStateCache = new WeakPointer<IConversationStateCache>(new InMemoryConversationStateCache());
            DialogActionCache = new WeakPointer<ICache<DialogAction>>(new InMemoryCache<DialogAction>());
            WebDataCache = new WeakPointer<ICache<CachedWebData>>(new InMemoryCache<CachedWebData>());
#pragma warning restore CA2000 // Dispose objects before losing scope
            UserProfileStorage = new InMemoryProfileStorage();
            CommonDomainName = DialogConstants.COMMON_DOMAIN;
            SideSpeechDomainName = DialogConstants.SIDE_SPEECH_DOMAIN;
            RealTime = DefaultRealTimeProvider.Singleton;
        }

        public DialogEngineParameters Clone()
        {
            return new DialogEngineParameters(Configuration, PluginProvider)
            {
                Logger = this.Logger,
                ConversationStateCache = this.ConversationStateCache,
                UserProfileStorage = this.UserProfileStorage,
                CommonDomainName = this.CommonDomainName,
                SideSpeechDomainName = this.SideSpeechDomainName,
                DialogActionCache = this.DialogActionCache,
                WebDataCache = this.WebDataCache,
                RealTime = this.RealTime,
            };
        }
    }
}
