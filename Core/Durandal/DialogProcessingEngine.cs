
namespace Durandal
{
    using Durandal.Common.Cache;
    using Durandal.API;
        using Durandal.Common.Audio;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.LG;
    using Durandal.Common.LG.Statistical;
    using Durandal.Common.LG.Template;
    using Durandal.Common.Logger;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP;
    using Durandal.Common.NLP.Feature;
    using Durandal.Common.Ontology;
    using Durandal.Common.Security.OAuth;
    using Durandal.Common.Speech;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Utils;
    using Durandal.Common.Collections;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Config.Accessors;
    using Durandal.Common.IO;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.ServiceMgmt;

    public class DialogProcessingEngine : IDisposable
    {
        private readonly DialogConfiguration _dialogConfig;
        private readonly IDictionary<PluginStrongName, LoadedPluginInformation> _allPlugins;
        private readonly object _pluginsLock = new object(); // FIXME this lock is used _really_ hackishly. It should probably be a SemaphoreSlim and use async try-finally pattern
        private readonly WeakPointer<IConversationStateCache> _conversationStateCache;
        private readonly IUserProfileStorage _userProfiles;
        private readonly WeakPointer<ICache<DialogAction>> _dialogActionCache;
        private readonly WeakPointer<ICache<CachedWebData>> _webDataCache;
        private readonly WeakPointer<IDurandalPluginProvider> _pluginProvider;
        private readonly ILogger _logger;
        private readonly string _commonDomainName;
        private readonly string _sideSpeechDomainName;
        private readonly IConfigValue<int> _maxConversationTurnLength;
        private int _disposed = 0;

        public DialogProcessingEngine(DialogEngineParameters dialogParams)
        {
            _dialogConfig = dialogParams.Configuration;
            _pluginProvider = dialogParams.PluginProvider;
            _logger = dialogParams.Logger;
            _dialogActionCache = dialogParams.DialogActionCache;
            _webDataCache = dialogParams.WebDataCache;
            _commonDomainName = dialogParams.CommonDomainName;
            if (string.IsNullOrEmpty(_commonDomainName))
            {
                _logger.Log("The provided common domain name is null or empty; using default value instead", LogLevel.Wrn);
                _commonDomainName = DialogConstants.COMMON_DOMAIN;
            }

            _sideSpeechDomainName = dialogParams.SideSpeechDomainName;
            if (string.IsNullOrEmpty(_sideSpeechDomainName))
            {
                _logger.Log("The provided side speech domain name is null or empty; using default value instead", LogLevel.Wrn);
                _sideSpeechDomainName = DialogConstants.SIDE_SPEECH_DOMAIN;
            }

            _allPlugins = new Dictionary<PluginStrongName, LoadedPluginInformation>();
            _conversationStateCache = dialogParams.ConversationStateCache;
            _userProfiles = dialogParams.UserProfileStorage;
            _maxConversationTurnLength = dialogParams.Configuration.MaxConversationHistoryLengthAccessor(_logger);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~DialogProcessingEngine()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Loads the plugin with the specified plugin ID. If multiple versions of that plugin exist, all of them will be loaded at once.
        /// </summary>
        /// <param name="pluginIdToLoad"></param>
        /// <param name="realTime">Real time definition, used for unit tests</param>
        /// <returns></returns>
        public async Task LoadPlugin(
            string pluginIdToLoad,
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            bool anyLoaded = false;

            foreach (PluginStrongName resolvedPluginName in await _pluginProvider.Value.GetAllAvailablePlugins(realTime).ConfigureAwait(false))
            {
                if (string.Equals(pluginIdToLoad, resolvedPluginName.PluginId, StringComparison.OrdinalIgnoreCase))
                {
                    await LoadPlugin(resolvedPluginName, realTime).ConfigureAwait(false);
                    anyLoaded = true;
                }
            }

            if (!anyLoaded)
            {
                _logger.Log("No plugin with ID \"" + pluginIdToLoad + "\" is available to load", LogLevel.Wrn);
            }
        }

        /// <summary>
        /// Loads the plugins with the specified plugin IDs. If multiple versions of a single plugin exist, all of them will be loaded at once.
        /// </summary>
        /// <param name="pluginIdsToLoad"></param>
        /// <param name="realTime">Real time definition, used for unit tests</param>
        /// <returns></returns>
        public async Task LoadPlugins(
            IEnumerable<string> pluginIdsToLoad,
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            foreach (string pluginId in pluginIdsToLoad)
            {
                await LoadPlugin(pluginId, realTime).ConfigureAwait(false);
            }
        }

        public async Task LoadPlugins(
            IEnumerable<PluginStrongName> pluginIdsToLoad,
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            foreach (PluginStrongName pluginId in pluginIdsToLoad)
            {
                await LoadPlugin(pluginId, realTime).ConfigureAwait(false);
            }
        }

        public async Task LoadPlugin(PluginStrongName pluginIdToLoad, IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            if (pluginIdToLoad == null)
            {
                _logger.Log("Attempted to load a null plugin!", LogLevel.Wrn);
                return;
            }

            _logger.Log("Loading plugin \"" + pluginIdToLoad + "\" into dialog engine...", LogLevel.Std);

            lock (_pluginsLock)
            {
                if (_allPlugins.ContainsKey(pluginIdToLoad))
                {
                    _logger.Log("Multiple plugins have been registered with the ID \"" + pluginIdToLoad.ToString() + "\"", LogLevel.Err);
                }
            }

            LoadedPluginInformation loadedPluginInfo = await _pluginProvider.Value.LoadPlugin(pluginIdToLoad, _logger, realTime).ConfigureAwait(false);
            if (loadedPluginInfo != null)
            {
                lock (_pluginsLock)
                {
                    _allPlugins[pluginIdToLoad] = loadedPluginInfo;
                }

                _logger.Log("Plugin \"" + loadedPluginInfo.PluginStrongName.ToString() + "\" has been registered with LU domain \"" + loadedPluginInfo.LUDomain + "\"");
                OnPluginRegistered(pluginIdToLoad); // Fire a load event to any listeners
            }
            else
            {
                _logger.Log("Plugin \"" + pluginIdToLoad.ToString() + "\" failed to load", LogLevel.Err);
            }
        }

        public Task UnloadAllPlugins(IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            _logger.Log("Unloading ALL plugins from dialog engine...", LogLevel.Wrn);
            Monitor.Enter(_pluginsLock);
            ISet<PluginStrongName> allKeys = new HashSet<PluginStrongName>(_allPlugins.Keys);
            Monitor.Exit(_pluginsLock);
            return UnloadPlugins(allKeys, realTime);
        }

        public async Task UnloadPlugins(
            IEnumerable<PluginStrongName> pluginIds,
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            foreach (PluginStrongName pluginId in pluginIds)
            {
                await UnloadPlugin(pluginId, realTime).ConfigureAwait(false);
            }
        }

        public async Task UnloadPlugin(string pluginIdToUnload, IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            _logger.Log("Unloading all versions of plugin \"" + pluginIdToUnload + "\" from dialog engine...", LogLevel.Std);

            List<PluginStrongName> strongNamesToUnload = new List<PluginStrongName>();
            Monitor.Enter(_pluginsLock);
            foreach (PluginStrongName strongName in _allPlugins.Keys)
            {
                if (string.Equals(strongName.PluginId, pluginIdToUnload))
                {
                    strongNamesToUnload.Add(strongName);
                }
            }

            Monitor.Exit(_pluginsLock);

            foreach (PluginStrongName strongName in strongNamesToUnload)
            {
                await UnloadPlugin(strongName, realTime).ConfigureAwait(false);
            }
        }

        public async Task UnloadPlugin(PluginStrongName pluginToUnload, IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            _logger.Log("Unloading plugin \"" + pluginToUnload.ToString() + "\" from dialog engine...", LogLevel.Std);
            Monitor.Enter(_pluginsLock);
            // Does this domain even exist?
            LoadedPluginInformation toUnregister;
            if (_allPlugins.TryGetValue(pluginToUnload, out toUnregister))
            {
                Monitor.Exit(_pluginsLock);

                // Be very careful that we do not hold the monitor while this await happens
                if (await _pluginProvider.Value.UnloadPlugin(toUnregister.PluginStrongName, _logger, realTime).ConfigureAwait(false))
                {
                    Monitor.Enter(_pluginsLock);
                    _allPlugins.Remove(pluginToUnload);
                    Monitor.Exit(_pluginsLock);
                    _logger.Log("Plugin \"" + pluginToUnload.ToString() + " has been unloaded", LogLevel.Vrb);
                }
            }
            else
            {
                Monitor.Exit(_pluginsLock);
                _logger.Log("No plugin is registered with ID \"" + pluginToUnload + "\"; cannot unload", LogLevel.Wrn);
            }
        }

        /// <summary>
        /// Sets the loaded plugins to a specific set of domains
        /// </summary>
        /// <param name="pluginIdsToKeep">The plugin Ids to keep loaded</param>
        /// <param name="realTime">Real time definition, used for unit tests</param>
        /// <param name="reloadAll">If true, reload even the plugins that are kept</param>
        public async Task SetLoadedPlugins(
            IEnumerable<PluginStrongName> pluginIdsToKeep,
            IRealTimeProvider realTime = null,
            bool reloadAll = false)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            HashSet<PluginStrongName> pluginsToLoad = new HashSet<PluginStrongName>();
            HashSet<PluginStrongName> pluginsToRemove = new HashSet<PluginStrongName>();

            lock (_pluginsLock)
            {
                foreach (PluginStrongName pluginId in _allPlugins.Keys)
                {
                    pluginsToRemove.Add(pluginId);
                }

                foreach (PluginStrongName pluginId in pluginIdsToKeep)
                {
                    if (!_allPlugins.ContainsKey(pluginId) || reloadAll)
                    {
                        pluginsToLoad.Add(pluginId);
                    }
                    if (pluginsToRemove.Contains(pluginId) && !reloadAll)
                    {
                        pluginsToRemove.Remove(pluginId);
                    }
                }
            }

            await UnloadPlugins(pluginsToRemove, realTime).ConfigureAwait(false);
            await LoadPlugins(pluginsToLoad, realTime).ConfigureAwait(false);
        }

        public ISet<PluginStrongName> GetLoadedPlugins()
        {
            lock (_pluginsLock)
            {
                ISet<PluginStrongName> returnVal = new HashSet<PluginStrongName>(_allPlugins.Keys);
                return returnVal;
            }
        }

        public ISet<string> GetLoadedPluginDomains()
        {
            lock (_pluginsLock)
            {
                HashSet<string> returnVal = new HashSet<string>();
                foreach (var plugin in _allPlugins.Values)
                {
                    if (!returnVal.Contains(plugin.LUDomain))
                    {
                        returnVal.Add(plugin.LUDomain);
                    }
                }

                return returnVal;
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
                _maxConversationTurnLength?.Dispose();
            }
        }

        private static readonly Regex PluginIdVersionMatcher = new Regex("(.+?) (\\d)\\.(\\d)");

        /// <summary>
        /// Retrieves from the loaded plugin set a static view data file.
        /// </summary>
        /// <param name="pluginId">The ID of the plugin to fetch the view file for, potentially with version number, e.g. "common" or "common 1.5"</param>
        /// <param name="path">The relative path of the file after the /views/plugin_id" prefix. Example "/page.css", "/resources/icon.png"</param>
        /// <param name="ifModifiedSince">If the client is using cache control, this is the datetime value that the client's cache was last updated</param>
        /// <param name="traceLogger">A tracing logger</param>
        /// <param name="realTime">Real time definition, used for unit tests</param>
        /// <returns>Cached web data containing payload, mime type, and cache validity info if found, otherwise null</returns>
        public async Task<CachedWebData> FetchPluginViewData(
            string pluginId, 
            string path,
            DateTimeOffset? ifModifiedSince,
            ILogger traceLogger,
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            PluginStrongName targetPlugin = null;

            // See if the plugin ID specifies a version
            Match regexMatch = PluginIdVersionMatcher.Match(pluginId);
            if (regexMatch.Success)
            {
                targetPlugin = new PluginStrongName(regexMatch.Groups[1].Value, int.Parse(regexMatch.Groups[2].Value), int.Parse(regexMatch.Groups[3].Value));
            }
            else
            {
                // Find the highest version of plugin with the specified ID, then dispatch the view request there
                Version highestVersion = new Version(0, 0);
                lock (_pluginsLock)
                {
                    foreach (PluginStrongName id in _allPlugins.Keys)
                    {
                        if (string.Equals(pluginId, id.PluginId) &&
                            id.Version > highestVersion)
                        {
                            highestVersion = id.Version;
                        }
                    }
                }

                targetPlugin = new PluginStrongName(pluginId, highestVersion.Major, highestVersion.Minor);
            }

            if (targetPlugin == null)
            {
                // HTTP 404 - plugin doesn't exist
                return null;
            }

            return await _pluginProvider.Value.FetchPluginViewData(targetPlugin, path, ifModifiedSince, traceLogger, realTime).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves the pair of user profiles for this user
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <param name="pluginId">The current pluginId to be called into (to index the domain-specific profile)</param>
        /// <param name="globalProfile">Any existing global profile which can be reused</param>
        /// <param name="globalHistory"></param>
        /// <param name="queryLogger">The logger for this query</param>
        /// <returns></returns>
        private async Task<UserProfileCollection> RetrieveUserProfiles(string userId, string pluginId, InMemoryDataStore globalProfile, InMemoryEntityHistory globalHistory, ILogger queryLogger)
        {
            if (globalProfile == null)
            {
                // Get both profiles
                RetrieveResult<UserProfileCollection> profile = await _userProfiles.GetProfiles(
                    UserProfileType.PluginLocal | UserProfileType.PluginGlobal | UserProfileType.EntityHistoryGlobal,
                    userId,
                    pluginId,
                    queryLogger).ConfigureAwait(false);

                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Store_UserProfileRead, profile.LatencyMs), LogLevel.Ins);

                if (profile.Success)
                {
                    if (profile.Result.LocalProfile == null)
                    {
                        profile.Result.LocalProfile = new InMemoryDataStore();
                    }
                    if (profile.Result.GlobalProfile == null)
                    {
                        profile.Result.GlobalProfile = new InMemoryDataStore();
                    }
                    if (profile.Result.EntityHistory == null)
                    {
                        profile.Result.EntityHistory = new InMemoryEntityHistory();
                    }

                    return profile.Result;
                }
                else
                {
                    return new UserProfileCollection(new InMemoryDataStore(), new InMemoryDataStore(), new InMemoryEntityHistory());
                }
            }
            else
            {
                // Get only the local profile
                RetrieveResult<UserProfileCollection> localProfile = await _userProfiles.GetProfiles(
                    UserProfileType.PluginLocal,
                    userId,
                    pluginId,
                    queryLogger).ConfigureAwait(false);

                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Store_UserProfileRead, localProfile.LatencyMs), LogLevel.Ins);

                if (localProfile.Success)
                {
                    if (localProfile.Result.LocalProfile == null)
                    {
                        localProfile.Result.LocalProfile = new InMemoryDataStore();
                    }

                    return new UserProfileCollection(localProfile.Result.LocalProfile, globalProfile, globalHistory);
                }
                else
                {
                    return new UserProfileCollection(new InMemoryDataStore(), globalProfile, globalHistory);
                }
            }
        }

#region Various RecoResult / RankedHypothesis helpers

        /// <summary>
        /// Synthesizes a "common/noreco" result that could trigger a retry handler
        /// </summary>
        /// <returns></returns>
        private RankedHypothesis CreateNoRecoResult(string textInput, SpeechRecognitionResult speechInput)
        {
            string rawUtterance = string.Empty;
            if (!string.IsNullOrEmpty(textInput))
            {
                rawUtterance = textInput;
            }
            else if (speechInput != null && speechInput.RecognizedPhrases != null && speechInput.RecognizedPhrases.Count > 0)
            {
                if (speechInput.RecognizedPhrases[0].InverseTextNormalizationResults != null && speechInput.RecognizedPhrases[0].InverseTextNormalizationResults.Count > 0)
                {
                    rawUtterance = speechInput.RecognizedPhrases[0].InverseTextNormalizationResults[0];
                }
                else
                {
                    rawUtterance = speechInput.RecognizedPhrases[0].DisplayText;
                }
            }
            
            RecoResult newResult = new RecoResult()
                {
                    Confidence = 1.0f,
                    Domain = _commonDomainName,
                    Intent = DialogConstants.NORECO_INTENT,
                    Utterance = new Sentence(rawUtterance)
                };
            newResult.TagHyps.Add(new TaggedData()
                {
                    Confidence = 1.0f,
                    Utterance = rawUtterance
                });
            RankedHypothesis rankedHyp = new RankedHypothesis(newResult);
            rankedHyp.DialogPriority = DialogConstants.DIALOG_PRIORITY_INTERNAL;
            return rankedHyp;
        }

        /// <summary>
        /// Inspects a set of reco results to see if "common/noreco" was returned by LU
        /// </summary>
        /// <param name="results"></param>
        /// <returns></returns>
        private bool IsNoReco(IList<RankedHypothesis> results)
        {
            return results.Count == 1 && IsNoReco(results[0].Result);
        }

        /// <summary>
        /// Inspects a reco result to see if "common/noreco" was returned by LU
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private bool IsNoReco(RecoResult result)
        {
            return result.Domain.Equals(_commonDomainName) &&
                result.Intent.Equals(DialogConstants.NORECO_INTENT);
        }

        /// <summary>
        /// Returns true if one of the reco results in the set equals common/side_speech
        /// </summary>
        /// <param name="results"></param>
        /// <returns></returns>
        private bool ContainsSideSpeech(List<RankedHypothesis> results)
        {
            foreach (RankedHypothesis r in results)
            {
                if (r.Result.Domain.Equals(_commonDomainName) && r.Result.Intent.Equals(DialogConstants.SIDE_SPEECH_INTENT))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// This method exists primarily to arbitrate two RecoResults that were triggered by regexes.
        /// In this case, they'll both have 1.0 confidence. In this case, favor the one inside the
        /// current conversation domain (only applies in multiturn)
        /// </summary>
        /// <param name="results"></param>
        /// <param name="state"></param>
        private static void ArbitrateIdenticalConfidences(ref List<RankedHypothesis> results, ConversationState state)
        {
            if (state == null || string.IsNullOrEmpty(state.CurrentPluginDomain))
            {
                return;
            }

            RankedHypothesis[] resultsArray = results.ToArray();
            bool bubble = results.Count > 1;
            while (bubble)
            {
                bubble = false;
                for (int c = 0; c < resultsArray.Length - 1; c++)
                {
                    RankedHypothesis left = resultsArray[c];
                    RankedHypothesis right = resultsArray[c + 1];
                    // Find if two hypotheses have identical confidences
                    if (left.ActualLuConfidence == right.ActualLuConfidence &&
                        !left.Result.Domain.Equals(state.CurrentPluginDomain) &&
                        right.Result.Domain.Equals(state.CurrentPluginDomain))
                    {
                        // If so, prefer the one that matches the previous turn domain
                        resultsArray[c] = right;
                        resultsArray[c + 1] = left;
                        bubble = true;
                    }
                }
            }
            results = new List<RankedHypothesis>(resultsArray);
        }

        /// <summary>
        /// Returns true if one of the reco results in the set equals common/noreco
        /// </summary>
        /// <param name="results"></param>
        /// <returns></returns>
        private bool ContainsNoReco(List<RankedHypothesis> results)
        {
            foreach (RankedHypothesis r in results)
            {
                if (r.Result.Domain.Equals(_commonDomainName) && r.Result.Intent.Equals(DialogConstants.NORECO_INTENT))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Appends a common/side_speech result with 0 confidence to the list of reco results.
        /// This is done so that plugins who choose to explicitly consume side speech on multiturn will always have it trigger.
        /// </summary>
        /// <param name="results"></param>
        /// <returns>True if the results were modified</returns>
        private bool AddSideSpeechIfNotPresent(ref List<RankedHypothesis> results)
        {
            if (results.Count == 0 || IsNoReco(results))
            {
                return false;
            }

            bool containsSideSpeech = ContainsSideSpeech(results);

            Sentence rawUtterance = results[0].Result.Utterance;

            // Create a new recoresult if side speech was not found
            if (rawUtterance != null && !containsSideSpeech)
            {
                TaggedData newTaggedText = new TaggedData()
                {
                     Confidence = 0.0f,
                     Utterance = rawUtterance.OriginalText
                };
                RecoResult newResult = new RecoResult()
                {
                    Confidence = 0.0f,
                    Domain = _commonDomainName,
                    Intent = DialogConstants.SIDE_SPEECH_INTENT,
                    Utterance = rawUtterance
                };
                newResult.TagHyps.Add(newTaggedText);
                RankedHypothesis rankedHyp = new RankedHypothesis(newResult);
                rankedHyp.DialogPriority = DialogConstants.DIALOG_PRIORITY_INTERNAL;
                results.Add(rankedHyp);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Appends a common/noreco result to the end of a list of reco results.
        /// This is done so that plugins who choose to explicitly consume noreco on multiturn will always have it trigger.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="textInput"></param>
        /// <param name="speechInput"></param>
        /// <returns>True if the results were modified</returns>
        private bool AddNoRecoIfNotPresent(ref List<RankedHypothesis> results, string textInput, SpeechRecognitionResult speechInput)
        {
            // Create a new recoresult if one was not found
            if (!ContainsNoReco(results))
            {
                results.Add(CreateNoRecoResult(textInput, speechInput));
                return true;
            }

            return false;
        }

        /// <summary>
        /// This ensures that side speech does not go above a certain threshold (default 0.8), to prevent it from taking
        /// over more legitimate domains, particularly in cases where a 2nd turn can consume side speech and another intent.
        /// Side speech, by definition, is never meant to be "high confidence", so we should defer to other intents
        /// </summary>
        /// <param name="results"></param>
        /// <param name="maxConfidence"></param>
        private void CapSideSpeechConfidence(ref List<RankedHypothesis> results, float maxConfidence)
        {
            foreach (RankedHypothesis recoResult in results)
            {
                if (recoResult.Result.Domain.Equals(_commonDomainName) &&
                    recoResult.Result.Intent.Equals(DialogConstants.SIDE_SPEECH_INTENT))
                {
                    recoResult.CapConfidence(maxConfidence);
                }
            }
        }

        /// <summary>
        /// If there is an existing side_speech hypothesis that has an actual LU confidence that is higher than its
        /// "effective" (capped) LU confidence, create a clone of the hyp with the "side_speech_highconf" intent.
        /// This is used by plugins which want to decisively consume side_speech on first turn without
        /// allowing other plugins to tenatively barge in. It's usually done to support chit-chat.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="maxConfidence"></param>
        private void CreateHighConfSideSpeechHyp(ref List<RankedHypothesis> results, float maxConfidence)
        {
            RankedHypothesis existingSideSpeech = null;
            foreach (RankedHypothesis recoResult in results)
            {
                if (recoResult.Result.Domain.Equals(_commonDomainName) &&
                    recoResult.Result.Intent.Equals(DialogConstants.SIDE_SPEECH_INTENT))
                {
                    existingSideSpeech = recoResult;
                }
            }

            if (existingSideSpeech != null && existingSideSpeech.DialogPriority == DialogConstants.DIALOG_PRIORITY_NORMAL && existingSideSpeech.ActualLuConfidence > maxConfidence)
            {
                RecoResult highConfHyp = existingSideSpeech.Result.Clone();
                highConfHyp.Intent = DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT;
                RankedHypothesis rankedHyp = new RankedHypothesis(highConfHyp);
                rankedHyp.DialogPriority = existingSideSpeech.DialogPriority;
                results.Add(rankedHyp);
            }
        }

        /*private static bool AddErrorIfNotPresent(ref List<RankedHypothesis> results)
        {
            if (IsNoReco(results))
            {
                Sentence errorMessage = new Sentence("Unknown dialog error");
                
                TaggedData fakeTaggedData = new TaggedData();
                fakeTaggedData.Confidence = 1.0f;
                fakeTaggedData.Utterance = errorMessage;
                fakeTaggedData.Slots = new List<SlotValue>();
                fakeTaggedData.Annotations = new Dictionary<string, string>();
                
                RecoResult errorRR = new RecoResult();
                errorRR.Confidence = 1.0f;
                errorRR.Domain = DialogConstants.REFLECTION_DOMAIN;
                errorRR.Intent = "error";
                errorRR.Utterance = errorMessage;
                errorRR.TagHyps = new List<TaggedData>();
                errorRR.TagHyps.Add(fakeTaggedData);

                RankedHypothesis hyp = new RankedHypothesis(errorRR);
                hyp.DialogPriority = -1;
                results.Add(hyp);

                return true;
            }

            return false;
        }*/

        /// <summary>
        /// Converts an individual plugin response into a higher-level DialogEngineResponse (final dialog engine result)
        /// </summary>
        /// <param name="pluginResult"></param>
        /// <param name="triggeredLuResult"></param>
        /// <param name="queryLogger"></param>
        /// <param name="domain"></param>
        /// <param name="intent"></param>
        /// <param name="locale"></param>
        /// <param name="isRetrying"></param>
        /// <param name="executedPlugin"></param>
        /// <returns></returns>
        private DialogEngineResponse BuildCompleteResponseFromPluginResult(
            PluginResult pluginResult,
            RecoResult triggeredLuResult,
            ILogger queryLogger,
            string domain,
            string intent,
            LanguageCode locale,
            bool isRetrying,
            PluginStrongName executedPlugin)
        {
            DialogEngineResponse dialogEngineResult = new DialogEngineResponse();

            dialogEngineResult.NextTurnBehavior = pluginResult.MultiTurnResult;
            dialogEngineResult.IsRetrying = isRetrying;

            if (!string.IsNullOrWhiteSpace(pluginResult.ResponseSsml))
            {
                // If it's just a plaintext string, try and detect that and fix it
                // Also try and normalize variations in the <speak> tag
                dialogEngineResult.SpokenSsml = SpeechUtils.NormalizeSsml(pluginResult.ResponseSsml, queryLogger);
            }

            if (!string.IsNullOrWhiteSpace(pluginResult.ResponseText))
            {
                dialogEngineResult.DisplayedText = pluginResult.ResponseText;
            }

            dialogEngineResult.ResponseCode = Result.Success;
            if (string.IsNullOrEmpty(pluginResult.ResponseUrl))
            {
                dialogEngineResult.ActionURL = string.Empty;
                dialogEngineResult.UrlScope = UrlScope.Local;
            }
            else
            {
                dialogEngineResult.ActionURL = pluginResult.ResponseUrl;
                // Plugins can return internal URLs (if they return, say, a dialog action redirect) so make sure we catch that
                dialogEngineResult.UrlScope = pluginResult.ResponseUrl.StartsWith("/") ? UrlScope.Local : UrlScope.External;
            }

            dialogEngineResult.PresentationHtml = pluginResult.ResponseHtml;
            dialogEngineResult.ResponseAudio = pluginResult.ResponseAudio;
            dialogEngineResult.SelectedRecoResult = triggeredLuResult.Clone();
            // This line is intended to allow "side_speech" domain to overwrite "common" in the returned reco result
            // Note that this has the side effect of making all common intents to be viewed as being part of the specific domain, like "myplugin/confirm".
            // Is this a bad thing?
            dialogEngineResult.SelectedRecoResult.Domain = domain;
            dialogEngineResult.SelectedRecoResult = StripFatRecoResult(dialogEngineResult.SelectedRecoResult);
            
            dialogEngineResult.ResponseData = new Dictionary<string, string>();
            dialogEngineResult.ClientAction = pluginResult.ClientAction;
            dialogEngineResult.ExecutedPlugin = executedPlugin;
            dialogEngineResult.PluginResponsePrivacyClass = DataPrivacyClassification.SystemMetadata;
            if (pluginResult.ResponseCode != Result.Failure)
            {
                // only apply the response privacy classification if the plugin didn't report a failure (in other words, error messages are considered non-PII. Note that this doesn't affect messages logged internally by the plugin)
                dialogEngineResult.PluginResponsePrivacyClass = pluginResult.ResponsePrivacyClassification;
            }

            // Pass along plugin data
            if (dialogEngineResult.ResponseData != null)
            {
                foreach (KeyValuePair<string, string> responseDataItem in pluginResult.ResponseData)
                {
                    dialogEngineResult.ResponseData.Add(responseDataItem.Key, responseDataItem.Value);
                }
            }

            // Pass along augmented query, if present
            if (pluginResult.AugmentedQuery != null)
            {
                // Convert it from TaggedData back to a string
                dialogEngineResult.AugmentedQuery = pluginResult.AugmentedQuery;
            }
            else if (triggeredLuResult.TagHyps.Count > 0)
            {
                // Or by default, use the SR hypothesis that was actually chosen by DE, since it's sometimes different from the client anyway
                dialogEngineResult.AugmentedQuery = triggeredLuResult.Utterance.OriginalText;
            }

            if (pluginResult.SuggestedQueries != null)
            {
                dialogEngineResult.SuggestedQueries = pluginResult.SuggestedQueries;
            }

            if (pluginResult.TriggerKeywords != null && pluginResult.TriggerKeywords.Count > 0)
            {
                dialogEngineResult.TriggerKeywords = pluginResult.TriggerKeywords;
            }

            return dialogEngineResult;
        }

        /// <summary>
        /// Strip unnecessary data from a reco result; mostly used to reduce our response data size
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static RecoResult StripFatRecoResult(RecoResult result)
        {
            // probably not necessary but just for safety, in cases references to this object
            // are still hooked up somewhere in the session or context objects
            RecoResult returnVal = result.Clone();

            foreach (TaggedData tagHyp in returnVal.TagHyps)
            {
                if (tagHyp.Annotations != null)
                {
                    Dictionary<string, string> newDict = new Dictionary<string, string>();
                    foreach (string annot in tagHyp.Annotations.Keys)
                    {
                        string annoVal = tagHyp.Annotations[annot];
                        annoVal = annoVal.Substring(0, Math.Min(annoVal.Length, 16));
                        newDict[annot] = annoVal;
                    }

                    tagHyp.Annotations = newDict;
                }

                foreach (SlotValue slot in tagHyp.Slots)
                {
                    if (slot.Annotations != null)
                    {
                        Dictionary<string, string> newDict = new Dictionary<string, string>();
                        foreach (string annot in slot.Annotations.Keys)
                        {
                            if (!string.Equals(annot, SlotPropertyName.StartIndex) &&
                                !string.Equals(annot, SlotPropertyName.StringLength))
                            {
                                string annoVal = slot.Annotations[annot];
                                //annoVal = annoVal.Substring(0, Math.Min(annoVal.Length, 16));
                                newDict[annot] = annoVal;
                            }
                        }

                        slot.Annotations = newDict;
                    }
                }
            }

            // Remove word boundary info from the returned utterance
            if (returnVal.Utterance != null)
            {
                if (returnVal.Utterance.Indices != null)
                {
                    returnVal.Utterance.Indices.Clear();
                }
                if (returnVal.Utterance.NonTokens != null)
                {
                    returnVal.Utterance.NonTokens.Clear();
                }
                if (returnVal.Utterance.Words != null)
                {
                    returnVal.Utterance.Words.Clear();
                }
            }

            return returnVal;
        }

#endregion

#region Plugin invocation interface

        private async Task<DialogProcessingResponse> CallSinglePlugin(
            LoadedPluginInformation plugin,
            RecoResult luInfo,
            ClientContext clientContext,
            ConversationState state,
            ClientAuthenticationLevel authLevel,
            InputMethod inputSource,
            AudioData audio,
            ILogger queryLogger,
            InMemoryDataStore triggerTimeConversationStore,
            UserProfileCollection userProfiles,
            int? bargeInTime,
            QueryFlags requestFlags,
            SpeechRecognitionResult speechRecoResults,
            KnowledgeContext inputEntityContext,
            IList<ContextualEntity> contextualEntities,
            IDictionary<string, string> requestData,
            IRealTimeProvider realTime)
        {
            QueryWithContext pluginInput = new QueryWithContext();
            pluginInput.Understanding = luInfo;
            pluginInput.TurnNum = state.TurnNum;
            pluginInput.PastTurns = state.PreviousConversationTurns;
            pluginInput.ClientContext = clientContext;
            pluginInput.AuthenticationLevel = authLevel;
            pluginInput.AuthScope = ExtractAuthScopeFromLevel(authLevel);
            pluginInput.Source = inputSource;
            pluginInput.InputAudio = audio; // Fixme: Make sure that this audio is in PCM format
            pluginInput.RetryCount = 0;
            pluginInput.BargeInTimeMs = bargeInTime;
            pluginInput.RequestFlags = requestFlags;
            pluginInput.OriginalSpeechInput = speechRecoResults;
            pluginInput.RequestData = requestData ?? new Dictionary<string, string>();
            DialogProcessingResponse executionResponse;

            // Get this plugin object store and IPluginServices
            InMemoryDataStore sessionStore = state.SessionStore;

            // Merge trigger time session data into the actual session store
            if (triggerTimeConversationStore != null)
            {
                queryLogger.Log("Merging trigger-time session data into actual session...");
                foreach (var item in triggerTimeConversationStore.GetAllObjects())
                {
                    if (!sessionStore.ContainsKey(item.Key)) // trigger session objects can't override existing session objects, but that should never happen anyways
                    {
                        sessionStore.Put(item.Key, item.Value);
                        queryLogger.Log("Merged " + item.Key, LogLevel.Vrb);
                    }
                }
            }

            PluginStrongName pluginStrongName = plugin.PluginStrongName;
            PluginInformation pluginInfo = plugin.PluginInfo;
            queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Invoking {0} with input {1}/{2}", pluginStrongName.PluginId, plugin.LUDomain, luInfo.Intent);
            queryLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Plugin information is {0} {1}.{2} by {3}", pluginInfo.InternalName, pluginInfo.MajorVersion, pluginInfo.MinorVersion, pluginInfo.Creator);

            bool isRetrying = IsNoReco(luInfo) && state.ConversationTree != null &&
                                          state.ConversationTree.GetRetryHandlerName(state.CurrentNode) != null;
            bool canConsumeNoReco = state.ConversationTree != null &&
                state.ConversationTree.TransitionExists(state.CurrentNode, _commonDomainName, DialogConstants.NORECO_INTENT);

            // Note that "canConsumeNoReco" will be true whether the plugin explicitly has a noreco edge or whether
            // it has a retry continuation enabled
            if ((!IsNoReco(luInfo) || canConsumeNoReco) && !isRetrying)
            {
                // Is this a special reflection call?
                // If so, add private system data to the object store
                if (luInfo.Domain.Equals(DialogConstants.REFLECTION_DOMAIN))
                {
                    AddReflectionInfoToSessionStore(sessionStore);
                }

                // Handle first-turn entry points into the side speech domain if it specifies non-default continuations
                string effectiveDomain = luInfo.Domain;
                if (plugin.LUDomain.Equals(_sideSpeechDomainName) &&
                    (luInfo.Intent.Equals(DialogConstants.SIDE_SPEECH_INTENT) || luInfo.Intent.Equals(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT)))
                {
                    effectiveDomain = _sideSpeechDomainName;
                }

                // Find the entry point to call in the program
                string nextTurnContinuation = state.GetNextTurnContinuation(effectiveDomain, luInfo.Intent);

                ValueStopwatch pluginTimer = ValueStopwatch.StartNew();
                /////// Launch the plugin! /////
                executionResponse = await _pluginProvider.Value.LaunchPlugin(
                    plugin.PluginStrongName,
                    nextTurnContinuation,
                    isRetrying,
                    pluginInput,
                    queryLogger,
                    sessionStore,
                    userProfiles,
                    inputEntityContext,
                    contextualEntities,
                    realTime).ConfigureAwait(false);
                ////////////////////////////////
                pluginTimer.Stop();

                if (executionResponse == null ||
                    executionResponse.PluginOutput == null)
                {
                    await _conversationStateCache.Value.ClearBothStates(clientContext.UserId, clientContext.ClientId, queryLogger.Clone("SessionStore"), true).ConfigureAwait(false);
                    queryLogger.Log("Cleared conversation state", LogLevel.Std);

                    queryLogger.Log("Plugin result from \"" + plugin.PluginId + "\" is null!", LogLevel.Err);
                    executionResponse = new DialogProcessingResponse(
                        new PluginResult(Result.Failure)
                        {
                            ErrorMessage =
                            "Plugin result from \"" + plugin.PluginId + "\" is null!"
                        },
                        isRetrying);
                }

                queryLogger.Log(plugin.PluginId + " finished with " + executionResponse.PluginOutput.ResponseCode.ToString(), LogLevel.Vrb);

                if (!string.IsNullOrEmpty(executionResponse.PluginOutput.ErrorMessage))
                {
                    queryLogger.Log(plugin.PluginId + " returned an error message: " + executionResponse.PluginOutput.ErrorMessage, LogLevel.Err);
                }

                // Update the session store in the conversation state with what came back from the executor
                if (executionResponse.UpdatedSessionStore != null)
                {
                    state.UpdateSessionStore(executionResponse.UpdatedSessionStore);
                }

                queryLogger.Log(
                    CommonInstrumentation.GenerateInstancedLatencyEntry(
                        CommonInstrumentation.Key_Latency_Plugin_Execute,
                        string.Format("{0}-{1}", plugin.PluginId, pluginInput.Understanding.Intent),
                        ref pluginTimer),
                    LogLevel.Ins,
                    privacyClass: DataPrivacyClassification.SystemMetadata);
                queryLogger.Log(CommonInstrumentation.GenerateObjectEntry("Dialog.ExecutedPlugins", new[]
                    {
                        new ExecutedPluginInfoForInstrumentation()
                        {
                            Domain = plugin.LUDomain,
                            PrimaryIntent = pluginInput.Understanding.Intent,
                            PluginId = plugin.PluginId,
                            Version = pluginInfo.MajorVersion + "." + pluginInfo.MinorVersion,
                            Result = executionResponse.PluginOutput.ResponseCode.ToString(),
                            ErrorMessage = executionResponse.PluginOutput.ErrorMessage
                        }
                    }),
                    LogLevel.Ins,
                    privacyClass: DataPrivacyClassification.SystemMetadata);
            }
            else
            {
                // Handle "noreco" intents, which means we're retrying
                string nextTurnContinuation = state.GetRetryContinuation();

                // Is there an existing continuation? If so, jump into it.
                // Otherwise, just use Execute
                if (nextTurnContinuation == null)
                {
                    // FIXME Is this a failure case? It indicates that we sent the common/noreco intent to a plugin that has no state and therefore can't accept that intent.
                    executionResponse = new DialogProcessingResponse(new PluginResult(Result.Skip), isRetrying);
                }
                else
                {
                    queryLogger.Log("Using the retry entry point " + nextTurnContinuation, LogLevel.Vrb);
                    queryLogger.Log("Retry count is " + (state.RetryNum + 1), LogLevel.Vrb);
                    pluginInput.RetryCount = state.RetryNum + 1;

                    ValueStopwatch pluginTimer = ValueStopwatch.StartNew();
                    /////// Launch the plugin! (Retry) /////
                    executionResponse = await _pluginProvider.Value.LaunchPlugin(
                        plugin.PluginStrongName,
                        nextTurnContinuation,
                        isRetrying,
                        pluginInput,
                        queryLogger,
                        sessionStore,
                        userProfiles,
                        inputEntityContext,
                        contextualEntities,
                        realTime).ConfigureAwait(false);
                    ////////////////////////////////
                    pluginTimer.Stop();

                    if (executionResponse == null ||
                        executionResponse.PluginOutput == null)
                    {
                        await _conversationStateCache.Value.ClearBothStates(clientContext.UserId, clientContext.ClientId, queryLogger.Clone("SessionStore"), true).ConfigureAwait(false);
                        queryLogger.Log("Cleared conversation state", LogLevel.Std);

                        queryLogger.Log("Plugin retry result from \"" + plugin.PluginId + "\" is null!", LogLevel.Err);
                        executionResponse = new DialogProcessingResponse(
                            new PluginResult(Result.Failure)
                            {
                                ErrorMessage = "Plugin retry result from \"" + plugin.PluginId + "\" is null!"
                            },
                            isRetrying);
                    }
                    // Did the retry continuation return "fail"?
                    // This means "end the conversation with a failure code".
                    // FIXME Is this logic obsolete???
                    else if (executionResponse.PluginOutput.ResponseCode.Equals(Result.Failure))
                    {
                        await _conversationStateCache.Value.ClearBothStates(clientContext.UserId, clientContext.ClientId, queryLogger.Clone("SessionStore"), true).ConfigureAwait(false);
                        queryLogger.Log("Cleared conversation state", LogLevel.Std);

                        executionResponse.PluginOutput.MultiTurnResult = MultiTurnBehavior.None;
                    }

                    // Did the retry continuation return "skip"?
                    // This means "end the conversation gracefully".
                    else if (executionResponse.PluginOutput.ResponseCode.Equals(Result.Skip))
                    {
                        await _conversationStateCache.Value.ClearBothStates(clientContext.UserId, clientContext.ClientId, queryLogger.Clone("SessionStore"), true).ConfigureAwait(false);
                        queryLogger.Log("Cleared conversation state", LogLevel.Std);

                        executionResponse.PluginOutput.ResponseCode = Result.Success;
                        executionResponse.PluginOutput.MultiTurnResult = MultiTurnBehavior.None;
                    }
                    else if (executionResponse.UpdatedSessionStore != null)
                    {
                        // Update the session store in the conversation state with what came back from the executor
                        state.UpdateSessionStore(executionResponse.UpdatedSessionStore);
                    }

                    queryLogger.Log(
                        CommonInstrumentation.GenerateInstancedLatencyEntry(
                            CommonInstrumentation.Key_Latency_Plugin_Execute,
                            string.Format("{0}-{1}", plugin.PluginId, pluginInput.Understanding.Intent),
                            ref pluginTimer),
                        LogLevel.Ins);
                    queryLogger.Log(CommonInstrumentation.GenerateObjectEntry("Dialog.ExecutedPlugins", new[]
                    {
                        new ExecutedPluginInfoForInstrumentation()
                        {
                            Domain = plugin.LUDomain,
                            PrimaryIntent = pluginInput.Understanding.Intent,
                            PluginId = plugin.PluginId,
                            Version = pluginInfo.MajorVersion + "." + pluginInfo.MinorVersion,
                            Result = executionResponse.PluginOutput.ResponseCode.ToString(),
                            ErrorMessage = executionResponse.PluginOutput.ErrorMessage
                        }
                    }),
                    LogLevel.Ins,
                    privacyClass: DataPrivacyClassification.SystemMetadata);
                }
            }

            return executionResponse;
        }

        private class ExecutedPluginInfoForInstrumentation
        {
            public string Domain { get; set; }
            public string PrimaryIntent { get; set; }
            public string PluginId { get; set; }
            public string Version { get; set; }
            public string Result { get; set; }
            public string ErrorMessage { get; set; }
        }
        
        private static ClientAuthenticationScope ExtractAuthScopeFromLevel(ClientAuthenticationLevel level)
        {
            ClientAuthenticationScope returnVal = ClientAuthenticationScope.None;

            if (level.HasFlag(ClientAuthenticationLevel.ClientAuthorized) ||
                level.HasFlag(ClientAuthenticationLevel.ClientUnauthorized) ||
                level.HasFlag(ClientAuthenticationLevel.ClientUnknown) ||
                level.HasFlag(ClientAuthenticationLevel.ClientUnverified))
            {
                returnVal |= ClientAuthenticationScope.Client;
            }

            if (level.HasFlag(ClientAuthenticationLevel.UserAuthorized) ||
                level.HasFlag(ClientAuthenticationLevel.UserUnauthorized) ||
                level.HasFlag(ClientAuthenticationLevel.UserUnknown) ||
                level.HasFlag(ClientAuthenticationLevel.UserUnverified))
            {
                returnVal |= ClientAuthenticationScope.User;
            }

            return returnVal;
        }

#endregion
        
        // FIXME we should really store reflection info inside transient request data, not the session store
        private void AddReflectionInfoToSessionStore(InMemoryDataStore sessionStore)
        {
            if (!sessionStore.ContainsKey("pluginInformation"))
            {
                Dictionary<string, PluginInformation> allPluginInfo = new Dictionary<string, PluginInformation>();
                Dictionary<string, Version> highestPluginVersions = new Dictionary<string, Version>();

                // Deduplicate multiple versions of the same plugin by finding only the highest version of each one
                lock (_pluginsLock)
                {
                    foreach (PluginStrongName loadedPluginName in _allPlugins.Keys)
                    {
                        if (!highestPluginVersions.ContainsKey(loadedPluginName.PluginId) ||
                            highestPluginVersions[loadedPluginName.PluginId] < loadedPluginName.Version)
                        {
                            highestPluginVersions[loadedPluginName.PluginId] = loadedPluginName.Version;
                        }
                    }

                    foreach (var uniquePluginInfo in highestPluginVersions)
                    {
                        PluginStrongName loadedPluginName = new PluginStrongName(uniquePluginInfo.Key, uniquePluginInfo.Value.Major, uniquePluginInfo.Value.Minor);
                        LoadedPluginInformation plugin = _allPlugins[loadedPluginName];

                        PluginInformation info = plugin.PluginInfo;
                        if (info != null)
                        {
                            allPluginInfo[loadedPluginName.PluginId] = info;
                        }
                        else
                        {
                            allPluginInfo[loadedPluginName.PluginId] = new PluginInformation();
                        }
                    }
                }

                string serializedInfo = JsonConvert.SerializeObject(allPluginInfo);
                sessionStore.Put("pluginInformation", serializedInfo);
            }
        }

        /// <summary>
        /// Calls the Trigger() method for all relevant plugins, and uses those values to rerank the responses before dialog execution
        /// </summary>
        /// <param name="results"></param>
        /// <param name="queryLogger"></param>
        /// <param name="clientContext"></param>
        /// <param name="authLevel"></param>
        /// <param name="inputSource"></param>
        /// <param name="queryAudio"></param>
        /// <param name="globalUserProfile"></param>
        /// <param name="globalUserHistory"></param>
        /// <param name="inputEntityContext"></param>
        /// <param name="contextualEntities"></param>
        /// <param name="requestData"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private async Task<TriggerResults> ProcessTriggerValues(
            List<RankedHypothesis> results,
            ILogger queryLogger,
            ClientContext clientContext,
            ClientAuthenticationLevel authLevel,
            InputMethod inputSource,
            AudioData queryAudio,
            InMemoryDataStore globalUserProfile,
            InMemoryEntityHistory globalUserHistory,
            KnowledgeContext inputEntityContext,
            IList<ContextualEntity> contextualEntities,
            IDictionary<string, string> requestData,
            IRealTimeProvider realTime)
        {
            TriggerResults returnVal = new TriggerResults();
            List<TriggerTaskClosure> triggeringClosures = new List<TriggerTaskClosure>();

            ValueStopwatch triggerTimer = ValueStopwatch.StartNew();

            foreach (RankedHypothesis rankedHyp in results)
            {
                RecoResult result = rankedHyp.Result;
                rankedHyp.DialogPriority = DialogConstants.DIALOG_PRIORITY_NORMAL;

                // Find the appropriate plugin that matches this reco result
                if (result.Domain.Equals(_commonDomainName) ||
                    result.Domain.Equals(_sideSpeechDomainName))
                {
                    continue;
                }

                string luDomain = result.Domain;
                string luIntent = result.Intent;

                // TODO: Process cross-domain handling and all that jazz
                // TODO are there versioning considerations to think about here? Seems like we should use the versions that
                // match our current session stack (if any) for triggering purposes, rather than defaulting to the latest version
                PluginStrongName targetPluginStrongName = GetPluginForLUDomain(luDomain);

                lock (_pluginsLock)
                {
                    if (targetPluginStrongName == null ||
                        !_allPlugins.ContainsKey(targetPluginStrongName))
                    {
                        continue;
                    }
                }

                // What we do here is create a set of blank session stores for each domain,
                // and then keep them in local dictionary variable.
                // If values are written to them, and the domain actually ends up triggering, the
                // values that were written at trigger time will be merged into the actual session state.
                // We can't just "retrieve the session store" at this point because at trigger time,
                // the conversation hasn't even started yet.
                InMemoryDataStore objectStore = new InMemoryDataStore();
                UserProfileCollection userProfiles = await RetrieveUserProfiles(
                    clientContext.UserId,
                    targetPluginStrongName.PluginId,
                    globalUserProfile,
                    globalUserHistory,
                    queryLogger).ConfigureAwait(false);
                
                // If configured, make the global domain read-only to every domain except a set of trusted ones (usually the reflection domain)
                IList<string> pluginIdsThatCanEditGlobalProfile = _dialogConfig.AllowedGlobalProfileEditors;
                if (pluginIdsThatCanEditGlobalProfile != null)
                {
                    // we need to dig a little to find the hidden setter for ReadOnly
                    if (userProfiles.GlobalProfile is InMemoryDataStore)
                    {
                        ((InMemoryDataStore)userProfiles.GlobalProfile).IsReadOnly = true;
                    }

                    foreach (string pluginId in pluginIdsThatCanEditGlobalProfile)
                    {
                        if (targetPluginStrongName.PluginId.Equals(pluginId))
                        {
                            if (userProfiles.GlobalProfile is InMemoryDataStore)
                            {
                                ((InMemoryDataStore)userProfiles.GlobalProfile).IsReadOnly = false;
                            }
                            break;
                        }
                    }
                }

                QueryWithContext query = new QueryWithContext();
                query.Understanding = result;
                query.ClientContext = clientContext;
                //query.TurnNum = turnNum;
                //query.PastTurns = state.PreviousConversationTurns;
                query.AuthenticationLevel = authLevel;
                query.Source = inputSource;
                query.InputAudio = queryAudio;
                query.RequestData = requestData ?? new Dictionary<string, string>();

                LoadedPluginInformation targetPlugin;
                lock (_pluginsLock)
                {
                    targetPlugin = _allPlugins[targetPluginStrongName];
                }

                // We want triggering to run in parallel, so we build a context closure and task for each potential trigger
                // FIXME this shares user profiles, input entity context, contextual entities, etc. from outside the closure, which could cause thread safety issues if multiple plugins try to modify them at once (which they shouldn't do, but they can try)
                TriggerTaskClosure closure = new TriggerTaskClosure(
                    _pluginProvider,
                    targetPlugin,
                    query,
                    luDomain,
                    luIntent,
                    objectStore,
                    rankedHyp,
                    userProfiles,
                    inputEntityContext,
                    contextualEntities);

                // Only fork the virtual time if this is not the first task.
                // This is to make sure there the "main thread's" time provider is not left behind when awaiting all the trigger tasks in parallel
                bool forkRealTimeForTriggerThread = realTime.IsForDebug && triggeringClosures.Count != 0;
                closure.Start(realTime, forkRealTimeForTriggerThread, queryLogger);
                triggeringClosures.Add(closure);
            }

            foreach (var triggerClosure in triggeringClosures)
            {
                try
                {
                    await triggerClosure.RunningTask.ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    queryLogger.Log("Plugin triggering for \"" + triggerClosure.TargetPlugin.PluginId + "\" has failed with an exception (but execution will continue)", LogLevel.Wrn);
                    queryLogger.Log(e, LogLevel.Wrn);
                    continue;
                }

                if (triggerClosure.ReturnVal != null &&
                    triggerClosure.ReturnVal.PluginOutput != null)
                {
                    string domainIntent = triggerClosure.Domain + "/" + triggerClosure.Intent;
                    returnVal.Results.Add(domainIntent, triggerClosure.ReturnVal.PluginOutput);
                    if (triggerClosure.ReturnVal.UpdatedSessionStore != null &&
                        triggerClosure.ReturnVal.UpdatedSessionStore.GetAllObjects().Count != 0 &&
                        !triggerClosure.ReturnVal.UpdatedSessionStore.ContainsKey(domainIntent))
                    {
                        // Did the trigger method want to store any side effects into session storage?
                        queryLogger.Log("The intent " + domainIntent + " stored side effects from triggering into its session store");
                        returnVal.SessionStores[domainIntent] = triggerClosure.ReturnVal.UpdatedSessionStore;
                    }

                    // Apply trigger boosting or suppression to the hyps themselves
                    if (triggerClosure.ReturnVal.PluginOutput.BoostResult == BoostingOption.Suppress)
                    {
                        triggerClosure.SourceHyp.DialogPriority = DialogConstants.DIALOG_PRIORITY_SUPPRESS;
                    }
                    if (triggerClosure.ReturnVal.PluginOutput.BoostResult == BoostingOption.Boost)
                    {
                        triggerClosure.SourceHyp.DialogPriority = DialogConstants.DIALOG_PRIORITY_BOOST;
                    }
                }
            }

            // Count the number of domains that triggered with boost == true
            int numOfBoostedHyps = 0;
            foreach (var triggerResult in returnVal.Results.Values)
            {
                if (triggerResult.BoostResult == BoostingOption.Boost)
                    numOfBoostedHyps++;
            }

            // If at most 1 plugin triggered, we're all good.
            if (numOfBoostedHyps <= 1)
            {
                returnVal.RequiresDisambiguation = false;
            }
            else
            {
                // But if 2 or more triggered, we need to invoke the disambiguation flow
                returnVal.RequiresDisambiguation = true;
            }

            returnVal.AugmentedHypotheses = results;

            triggerTimer.Stop();

            queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_Triggers, ref triggerTimer), LogLevel.Ins);

            return returnVal;
        }

        private class TriggerTaskClosure
        {
            private readonly WeakPointer<IDurandalPluginProvider> _pluginProvider;
            private readonly QueryWithContext _query;
            private readonly InMemoryDataStore _inputSessionStore;
            private readonly UserProfileCollection _userProfiles;
            private readonly KnowledgeContext _inputEntityContext;
            private readonly IList<ContextualEntity> _inputContextualEntities;

            public readonly LoadedPluginInformation TargetPlugin;
            public readonly RankedHypothesis SourceHyp;
            public readonly string Domain;
            public readonly string Intent;

            public TriggerProcessingResponse ReturnVal;
            public Task RunningTask;

            public TriggerTaskClosure(
                WeakPointer<IDurandalPluginProvider> pluginProvider,
                LoadedPluginInformation targetPlugin,
                QueryWithContext query,
                string domain,
                string intent,
                InMemoryDataStore inputSessionStore,
                RankedHypothesis sourceHyp,
                UserProfileCollection userProfiles,
                KnowledgeContext inputEntityContext,
                IList<ContextualEntity> inputContextualEntities)
            {
                _pluginProvider = pluginProvider;
                TargetPlugin = targetPlugin;
                _query = query;
                Domain = domain;
                Intent = intent;
                _inputSessionStore = inputSessionStore;
                SourceHyp = sourceHyp;
                _userProfiles = userProfiles;
                _inputEntityContext = inputEntityContext;
                _inputContextualEntities = inputContextualEntities;
            }

            public void Start(
                IRealTimeProvider realTime,
                bool forkTime,
                ILogger queryLogger)
            {
                IRealTimeProvider threadLocalTime = forkTime ? realTime.Fork("DialogTriggeringThread") : realTime;
                RunningTask = Run(threadLocalTime, forkTime, queryLogger);
            }

            private async Task Run(
                IRealTimeProvider threadLocalTime,
                bool isTimeForked,
                ILogger queryLogger)
            {
                try
                {
                    ValueStopwatch individualTriggerTimer = ValueStopwatch.StartNew();
                    ReturnVal = await _pluginProvider.Value.TriggerPlugin(
                        TargetPlugin.PluginStrongName,
                        _query,
                        queryLogger,
                        _inputSessionStore,
                        _userProfiles,
                        _inputEntityContext,
                        _inputContextualEntities,
                        threadLocalTime).ConfigureAwait(false);

                    individualTriggerTimer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_Plugin_Trigger, TargetPlugin.PluginId, ref individualTriggerTimer), LogLevel.Ins);
                }
                catch (Exception e)
                {
                    queryLogger.Log("An exception was thrown during triggering of the \"" + Domain + "\" domain", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    if (isTimeForked)
                    {
                        threadLocalTime.Merge();
                    }
                }
            }
        }

        private Stack<ConversationState> InitializeConversationTrees(Stack<ConversationState> stack, ILogger queryLogger)
        {
            List<ConversationState> linearStates = ConversationState.StackToList(stack);
            List<ConversationState> linearProcessedStates = new List<ConversationState>();
            foreach (var state in linearStates)
            {
                ConversationState convertedState = state;
                PluginStrongName pluginStrongName = new PluginStrongName(convertedState.CurrentPluginId, convertedState.CurrentPluginVersion.Major, convertedState.CurrentPluginVersion.Minor);
                lock (_pluginsLock)
                {
                    if (!_allPlugins.ContainsKey(pluginStrongName))
                    {
                        // Check if we have a state in our stack that is for a plugin version that is similar to, but not quite exactly, the version we want.
                        queryLogger.Log("Detected a conversation state version change for which the exact version " + pluginStrongName.ToString() + " doesn't exist. Attempting to resolve...", LogLevel.Wrn);
                        Version bestMatchingVersion = null;
                        foreach (LoadedPluginInformation plugin in _allPlugins.Values)
                        {
                            if (string.Equals(plugin.PluginId, convertedState.CurrentPluginId))
                            {
                                PluginStrongName potentialPlugin = plugin.PluginStrongName;
                                if (potentialPlugin.MajorVersion == convertedState.CurrentPluginVersion.Major &&
                                    (bestMatchingVersion == null || potentialPlugin.Version > bestMatchingVersion))
                                {
                                    pluginStrongName = potentialPlugin;
                                    bestMatchingVersion = potentialPlugin.Version;
                                }
                            }
                        }

                        if (bestMatchingVersion == null)
                        {
                            // Can't reconcile the state version ID and the ones that are loaded in dialog, so just drop this state
                            queryLogger.Log("Dropping state entry for nonexistent plugin \"" + pluginStrongName.ToString() + "\"", LogLevel.Wrn);
                        }
                        else
                        {
                            queryLogger.Log("Upgrading conversation state from " + pluginStrongName.ToString() + " to " + bestMatchingVersion.Major + "." + bestMatchingVersion.Minor, LogLevel.Wrn);
                            pluginStrongName = new PluginStrongName(convertedState.CurrentPluginId, bestMatchingVersion.Major, bestMatchingVersion.Minor);
                        }
                    }

                    // If the state specifies a plugin version that is no longer loaded, or if an explicit continuation is specified
                    // in the state, ignore the conversation tree entirely
                    if (_allPlugins.ContainsKey(pluginStrongName) &&
                        string.IsNullOrEmpty(convertedState.ExplicitContinuation))
                    {
                        convertedState.SetConversationTree(_allPlugins[pluginStrongName].ConversationTree);
                    }
                }

                linearProcessedStates.Add(convertedState);
            }

            Stack<ConversationState> returnVal = ConversationState.ListToStack(linearProcessedStates);
            return returnVal;
        }

        private static List<RankedHypothesis> BuildRankedHypsFromInvokedDialogAction(string originDomain, DialogAction action)
        {
            TaggedData slotContainer = new TaggedData();
            slotContainer.Confidence = 1.0f;
            slotContainer.Slots = new List<SlotValue>();
            foreach (var slot in action.Slots)
            {
                // To keep namespaces from clashing, prepend the domain name of the slot's origin to each slot name
                slot.Name = originDomain + "." + slot.Name;
                slotContainer.Slots.Add(slot);
            }

            slotContainer.Utterance = string.Empty;
            RecoResult syntheticRecoResult = new RecoResult();
            syntheticRecoResult.Domain = action.Domain;
            syntheticRecoResult.Intent = action.Intent;
            syntheticRecoResult.Confidence = 1.0f;
            syntheticRecoResult.Source = "InvokedDialogAction";
            syntheticRecoResult.TagHyps = new List<TaggedData>();
            syntheticRecoResult.TagHyps.Add(slotContainer);
            syntheticRecoResult.Utterance = new Sentence();
            RankedHypothesis dialogActionHyp = new RankedHypothesis(syntheticRecoResult);
            List<RankedHypothesis> dialogActionHyps = new List<RankedHypothesis>();
            dialogActionHyps.Add(dialogActionHyp);
            return dialogActionHyps;
        }

        private async Task<DialogEngineResponse> InvokeDisambiguationScenario(
            TriggerResults triggerResult,
            IList<RankedHypothesis> rankedHyps,
            ClientContext clientContext,
            InputMethod inputSource,
            ClientAuthenticationLevel authLevel,
            Guid? traceId,
            AudioData inputAudio,
            string textInput,
            SpeechRecognitionResult speechInput,
            Stack<ConversationState> conversationStack,
            ILogger queryLogger,
            QueryFlags requestFlags,
            IDictionary<string, string> requestData,
            KnowledgeContext inputEntityContext,
            IList<ContextualEntity> contextualEntities,
            IRealTimeProvider realTime)
        {
            queryLogger.Log("I am beginning to invoke the disambiguation scenario using " + DialogConstants.REFLECTION_DOMAIN  + "/ disambiguate");
            
            // Magically inject the serialized trigger info into the session store for Reflection
            ConversationState reflectionConversationState = new ConversationState();

            queryLogger.Log("Freezing conversation state...");

            IDictionary<string, TriggerResult> triggerResults = new Dictionary<string, TriggerResult>();
            foreach (var t in triggerResult.Results)
            {
                triggerResults.Add(t.Key, t.Value);
            }

            reflectionConversationState.SessionStore.Put("triggerResults", JsonConvert.SerializeObject(triggerResults));
            reflectionConversationState.SessionStore.Put("_cache_rankedhyps", JsonConvert.SerializeObject(rankedHyps));
            reflectionConversationState.SessionStore.Put("_cache_clientcontext", JsonConvert.SerializeObject(clientContext));
            reflectionConversationState.SessionStore.Put("_cache_inputsource", JsonConvert.SerializeObject(inputSource));
            if (inputAudio != null)
            {
                reflectionConversationState.SessionStore.Put("_cache_audiodata", JsonConvert.SerializeObject(inputAudio));
            }
            if (!string.IsNullOrEmpty(textInput))
            {
                reflectionConversationState.SessionStore.Put("_cache_textinput", textInput);
            }
            if (speechInput != null)
            {
                reflectionConversationState.SessionStore.Put("_cache_speechinput", JsonConvert.SerializeObject(speechInput));
            }
            if (inputEntityContext != null && !inputEntityContext.IsEmpty)
            {
                using (PooledBuffer<byte> serializedEntityContext = inputEntityContext.Serialize())
                {
                    byte[] copiedData = new byte[serializedEntityContext.Length];
                    ArrayExtensions.MemCopy(serializedEntityContext.Buffer, 0, copiedData, 0, copiedData.Length);
                    reflectionConversationState.SessionStore.Put("_cache_entityContext", copiedData);
                }

                if (contextualEntities != null)
                {
                    List<EntityReference> entityReferences = new List<EntityReference>();
                    foreach (var entity in contextualEntities)
                    {
                        if (entity.Entity != null)
                        {
                            entityReferences.Add(new EntityReference()
                            {
                                EntityId = entity.Entity.EntityId,
                                Relevance = entity.Relevance
                            });
                        }
                    }

                    reflectionConversationState.SessionStore.Put("_cache_contextualEntities", JsonConvert.SerializeObject(entityReferences));
                }
            }
            if (requestData != null)
            {
                reflectionConversationState.SessionStore.Put("_cache_requestData", JsonConvert.SerializeObject(requestData));
            }

            Dictionary<string, Dictionary<string, string>> sideEffectStores = new Dictionary<string, Dictionary<string, string>>();
            // Also store each trigger side effects 
            if (triggerResult.SessionStores != null)
            {
                foreach (var sessionStore in triggerResult.SessionStores)
                {
                    sideEffectStores[sessionStore.Key] = new Dictionary<string, string>();
                    foreach (var storeItem in sessionStore.Value.GetAllObjects())
                    {
                        sideEffectStores[sessionStore.Key].Add(storeItem.Key, Convert.ToBase64String(storeItem.Value));
                    }
                }
            }
            if (sideEffectStores.Count > 0)
            {
                reflectionConversationState.SessionStore.Put("_cache_sideeffectstores", JsonConvert.SerializeObject(sideEffectStores));
            }

            conversationStack.Push(reflectionConversationState);

            queryLogger.Log("Conversation state saved. Diverting to reflection domain");

            // Build the disambiguation hypothesis and invoke it
            List<RankedHypothesis> disambiguationHypotheses = new List<RankedHypothesis>();
            RecoResult disambiguationRecoResult = new RecoResult()
            {
                Domain = DialogConstants.REFLECTION_DOMAIN,
                Intent = "disambiguate",
                Confidence = 1.0f,
                Source = "InvokedDialogAction",
                Utterance = rankedHyps[0].Result.Utterance
            };

            RankedHypothesis disambiguationHypothesis = new RankedHypothesis(disambiguationRecoResult);
            disambiguationHypotheses.Add(disambiguationHypothesis);
            DialogEngineResponse response = await Process(
                results: disambiguationHypotheses,
                clientContext: clientContext,
                authLevel: authLevel,
                inputSource: InputMethod.Programmatic,
                isNewConversation: false,
                useTriggers: false,
                traceId: traceId,
                queryLogger: queryLogger,
                inputAudio: inputAudio,
                textInput: textInput,
                speechInput: speechInput,
                conversationStack: conversationStack,
                triggerSideEffects: null,
                bargeInTime: -1,
                requestFlags: requestFlags,
                inputEntityContext: null,
                contextualEntities: null,
                requestData: requestData,
                realTime: realTime).ConfigureAwait(false);
            
            if (response.ResponseCode != Result.Success)
            {
                // If, for some reason the reflection domain couldn't honor the request to start the disambiguation scenario,
                // we need to revert the changes we've made in this function.
                queryLogger.Log("Disambiguation scenario failed to trigger. Reverting conversation state", LogLevel.Wrn);
                conversationStack.Pop();
            }

            return response;
        }

        private async Task<DialogEngineResponse> HandleDisambiguationCallback(
            DialogAction invokedDialogAction,
            InMemoryDataStore reflectionDomainSessionStore,
            Guid? traceId,
            ClientAuthenticationLevel authLevel,
            Stack<ConversationState> conversationStack,
            ILogger queryLogger,
            int? bargeInTime,
            QueryFlags requestFlags,
            IRealTimeProvider realTime)
        {
            queryLogger.Log("The reflection domain is issuing a disambiguation callback, so I will now do a lot of magic to restore the previous conversation state");
            InMemoryDataStore disambigCache = reflectionDomainSessionStore;
            List<RankedHypothesis> disambigHyps = JsonConvert.DeserializeObject<List<RankedHypothesis>>(disambigCache.GetString("_cache_rankedhyps"));
            ClientContext disambigContext = JsonConvert.DeserializeObject<ClientContext>(disambigCache.GetString("_cache_clientcontext"));
            InputMethod disambigInputMethod = JsonConvert.DeserializeObject<InputMethod>(disambigCache.GetString("_cache_inputsource"));
            AudioData disambigAudioData = null;
            if (disambigCache.ContainsKey("_cache_audiodata"))
            {
                disambigAudioData = JsonConvert.DeserializeObject<AudioData>(disambigCache.GetString("_cache_audiodata"));
            }
            List<SpeechHypothesis_v16> disambigRawQueries = null;
            if (disambigCache.ContainsKey("_cache_rawqueries"))
            {
                disambigRawQueries = JsonConvert.DeserializeObject<List<SpeechHypothesis_v16>>(disambigCache.GetString("_cache_rawqueries"));
            }
            Dictionary<string, Dictionary<string, string>> sideEffectStores = null;
            if (disambigCache.ContainsKey("_cache_sideeffectstores"))
            {
                sideEffectStores = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(disambigCache.GetString("_cache_sideeffectstores"));
            }
            string originalTextInput = null;
            if (disambigCache.ContainsKey("_cache_textinput"))
            {
                originalTextInput = disambigCache.GetString("_cache_textinput");
            }
            SpeechRecognitionResult originalSpeechInput = null;
            if (disambigCache.ContainsKey("_cache_speechInput"))
            {
                originalSpeechInput = JsonConvert.DeserializeObject<SpeechRecognitionResult>(disambigCache.GetString("_cache_speechInput"));
            }
            List<ContextualEntity> inputContextualEntities = new List<ContextualEntity>();
            KnowledgeContext originalEntityContext = null;
            if (disambigCache.ContainsKey("_cache_entityContext"))
            {
                byte[] serializedContext = disambigCache.GetBinary("_cache_entityContext");
                originalEntityContext = KnowledgeContext.Deserialize(serializedContext);

                // Pull out contextual entities, if any
                if (disambigCache.ContainsKey("_cache_contextualEntities"))
                {
                    List<EntityReference> references = JsonConvert.DeserializeObject<List<EntityReference>>(disambigCache.GetString("_cache_contextualEntities"));

                    foreach (var entityRef in references)
                    {
                        Entity handle = originalEntityContext.GetEntityInMemory(entityRef.EntityId);
                        // FIXME the entity source is wrong here
                        ContextualEntity clientProvidedEntity = new ContextualEntity(handle, ContextualEntitySource.ClientInput, entityRef.Relevance);
                        inputContextualEntities.Add(clientProvidedEntity);
                    }
                }
            }
            Dictionary<string, string> originalRequestData = null;
            if (disambigCache.ContainsKey("_cache_requestData"))
            {
                originalRequestData = JsonConvert.DeserializeObject<Dictionary<string, string>>(disambigCache.GetString("_cache_requestData"));
            }

            // Find the domain/intent that the reflection domain has selected as the primary hyp and suppress all others
            SlotValue disambigDomainIntentSlot = DialogHelpers.TryGetSlot(invokedDialogAction.Slots, "disambiguated_domain_intent");
            if (disambigDomainIntentSlot == null || string.IsNullOrEmpty(disambigDomainIntentSlot.Value) || !disambigDomainIntentSlot.Value.Contains("/"))
            {
                throw new DialogException("Reflection domain did not return a selection for disambiguated hyp");
            }
            
            int sep = disambigDomainIntentSlot.Value.IndexOf('/');
            string winningDomain = disambigDomainIntentSlot.Value.Substring(0, sep);
            string winningIntent = disambigDomainIntentSlot.Value.Substring(sep + 1);

            foreach (RankedHypothesis disambigHyp in disambigHyps)
            {
                if (disambigHyp.DialogPriority == DialogConstants.DIALOG_PRIORITY_BOOST && !(disambigHyp.Result.Domain.Equals(winningDomain) && disambigHyp.Result.Intent.Equals(winningIntent)))
                {
                    disambigHyp.DialogPriority = DialogConstants.DIALOG_PRIORITY_NORMAL;
                }
            }

            // Clear the state for the reflection domain that we injected onto the stack earlier
            conversationStack.Pop();

            IDictionary<string, InMemoryDataStore> reifiedTriggerSideEffects = new Dictionary<string, InMemoryDataStore>();

            // Inject the side effects from way back at turn 1 trigger time into the conversation state
            // (we couldn't do this earlier because we didn't know what domain would become the actual conversation domain)
            if (sideEffectStores != null)
            {
                foreach (var store in sideEffectStores)
                {
                    reifiedTriggerSideEffects[store.Key] = new InMemoryDataStore();
                    foreach (var triggeringValue in store.Value)
                    {
                        reifiedTriggerSideEffects[store.Key].Put(triggeringValue.Key, Convert.FromBase64String(triggeringValue.Value));
                    }
                }
            }

            queryLogger.Log("Mischief Managed");
            return await Process(
                results: disambigHyps,
                clientContext: disambigContext,
                authLevel: authLevel,
                inputSource: disambigInputMethod,
                isNewConversation: false,
                useTriggers: false,
                traceId: traceId,
                queryLogger: queryLogger,
                inputAudio: disambigAudioData,
                textInput: originalTextInput,
                speechInput: originalSpeechInput,
                conversationStack: conversationStack,
                triggerSideEffects: reifiedTriggerSideEffects,
                bargeInTime: bargeInTime,
                requestFlags: requestFlags,
                inputEntityContext: originalEntityContext,
                contextualEntities: null,
                requestData: originalRequestData,
                realTime: realTime).ConfigureAwait(false);
        }

#region Main processing
        
        /// <summary>
        /// All of the complicated conversational logic happens here. A lot of different variables
        /// like current conversation state, multiturn behavior, carryover slots and domains, and plugin
        /// outputs will determine the flow of the conversation.
        /// </summary>
        /// <param name="results">The list of incoming dialog hypotheses (which are ranked recoresults)</param>
        /// <param name="clientContext">The input client context</param>
        /// <param name="authLevel">The client authentication level to pass to the executing plugin (does not affect dialog logic)</param>
        /// <param name="inputSource">The method of input that generated these hyps (does not affect directly dialog logic)</param>
        /// <param name="isNewConversation">If this is true, previous session context will always be ignored</param>
        /// <param name="useTriggers">If true, each relevant plugin will have its Trigger() function invoked on this pass</param>
        /// <param name="traceId">A logging trace ID (optional)</param>
        /// <param name="queryLogger">A logger for the operation</param>
        /// <param name="inputAudio">If speech, the actual audio waveform that was spoken (optional). MUST BE PCM!</param>
        /// <param name="textInput">If text, this is the text query</param>
        /// <param name="speechInput">If speech, this is the speech recognition result</param>
        /// <param name="conversationStack">If you have already fetched the conversation state, you can pass it here to prevent fetching it again</param>
        /// <param name="triggerSideEffects">Used internally for passing conversation stores of plugins between trigger and execution time</param>
        /// <param name="bargeInTime">The number of milliseconds of audio output that was spoken from the previous turn before the user made the current query</param>
        /// <param name="requestFlags">Request flags that can alter logging, debugging, etc.</param>
        /// <param name="inputEntityContext">A context containing all contextual entities from LU or passed from the client</param>
        /// <param name="contextualEntities">A list of contextual entities passed from the client</param>
        /// <param name="requestData">Key-value pairs of data passed from the client, used for SPA and client resolution scenarios</param>
        /// <param name="realTime">Real time, used mostly for unit testing.</param>
        /// <returns>The complete dialog engine response</returns>
        public async Task<DialogEngineResponse> Process(
            List<RankedHypothesis> results,
            ClientContext clientContext,
            ClientAuthenticationLevel authLevel = ClientAuthenticationLevel.None,
            InputMethod inputSource = InputMethod.Unknown,
            bool isNewConversation = false,
            bool useTriggers = true,
            Guid? traceId = null,
            ILogger queryLogger = null,
            AudioData inputAudio = null,
            string textInput = null,
            SpeechRecognitionResult speechInput = null,
            Stack<ConversationState> conversationStack = null,
            IDictionary<string, InMemoryDataStore> triggerSideEffects = null,
            int? bargeInTime = null,
            QueryFlags requestFlags = QueryFlags.None,
            KnowledgeContext inputEntityContext = null,
            IList<ContextualEntity> contextualEntities = null,
            IDictionary<string, string> requestData = null,
            IRealTimeProvider realTime = null)
        {
            if (!traceId.HasValue)
            {
                traceId = Guid.NewGuid();
            }

            if (inputEntityContext == null)
            {
                inputEntityContext = new KnowledgeContext();
            }

            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            queryLogger = queryLogger ?? _logger.CreateTraceLogger(traceId);

            if (results.Count > 0 && results[0].Result != null && results[0].Result.Utterance != null)
            {
                if (string.IsNullOrEmpty(results[0].Result.Utterance.OriginalText))
                {
                    queryLogger.Log("Dialog engine top input is empty string");
                }
                else
                {
                    queryLogger.Log("Dialog engine top input: \"" + results[0].Result.Utterance.OriginalText + "\"", privacyClass: DataPrivacyClassification.PrivateContent);
                }
            }
            else
            {
                queryLogger.Log("No reco results passed to dialog engine!", LogLevel.Wrn);
            }
            
            if (conversationStack == null)
            {
                if (!isNewConversation)
                {
                    RetrieveResult<Stack<ConversationState>> sessionRetrieveResult =
                        await _conversationStateCache.Value.TryRetrieveState(clientContext.UserId, clientContext.ClientId, queryLogger.Clone("SessionStore"), realTime).ConfigureAwait(false);
                    if (sessionRetrieveResult.Success)
                    {
                        queryLogger.Log("Conversation stack exists; treating this as a multiturn conversation", LogLevel.Std);
                        conversationStack = sessionRetrieveResult.Result;
                    }
                    else
                    {
                        queryLogger.Log("Starting with empty conversation stack", LogLevel.Std);
                        conversationStack = new Stack<ConversationState>();
                    }

                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Store_SessionRead, sessionRetrieveResult.LatencyMs), LogLevel.Ins);
                }
                else
                {
                    queryLogger.Log("The isNewConversation flag is set; clearing all context for user " + clientContext.UserId, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                    // BUGBUG: If I cleared the state here, it might cause a race condition where the state gets set to a new value before the delete finishes.
                    // Since I presume the state will be overwritten anyways, I'm not going to explicitly clear it here.
                    //_conversationStateCache.Value.ClearState(clientContext.UserId, clientContext.ClientId, queryLogger.Clone("SessionStore"));
                    conversationStack = new Stack<ConversationState>();
                }

                // Convert the state from its serialized bond form
                conversationStack = InitializeConversationTrees(conversationStack, queryLogger);
            }
            else
            {
                queryLogger.Log("Reusing an existing conversation stack");

                // Associate each conversation state with the currently loaded plugins and their convo trees (kind of messy but this code path has a lot of history)
                conversationStack = InitializeConversationTrees(conversationStack, queryLogger);
            }

            if (conversationStack.Count == 0)
            {
                conversationStack.Push(new ConversationState());
            }

            MultiTurnBehavior originalMultiturnState = conversationStack.Peek().LastMultiturnState;
            DialogEngineResponse dialogEngineResult = new DialogEngineResponse();
            DialogEngineResponse bestSkipResult = null;
            PluginPostExecutionClosure bestSkipResultClosure = null;
            dialogEngineResult.NextTurnBehavior = MultiTurnBehavior.None;

            TriggerResults triggerResult = null;

            // Cache the global user profiles so we don't have to re-fetch it between plugin invocations
            InMemoryDataStore globalUserProfile = null;
            InMemoryEntityHistory globalUserHistory = null;

            if (useTriggers)
            {
                queryLogger.Log("Running triggering on all plugins");
                triggerResult = await ProcessTriggerValues(
                    results,
                    queryLogger,
                    clientContext,
                    authLevel,
                    inputSource,
                    inputAudio,
                    globalUserProfile,
                    globalUserHistory,
                    inputEntityContext,
                    contextualEntities,
                    requestData,
                    realTime).ConfigureAwait(false);
                
                results = triggerResult.AugmentedHypotheses;
                triggerSideEffects = triggerResult.SessionStores;

                // Do we need to disambiguate multiple triggers?
                if (triggerResult.RequiresDisambiguation)
                {
                    queryLogger.Log("Multiple plugins triggered with high confidence; invoking disambiguation scenario");
                    DialogEngineResponse result = await InvokeDisambiguationScenario(
                        triggerResult,
                        results,
                        clientContext,
                        inputSource,
                        authLevel,
                        traceId,
                        inputAudio,
                        textInput,
                        speechInput,
                        conversationStack,
                        queryLogger,
                        requestFlags,
                        requestData,
                        inputEntityContext,
                        contextualEntities,
                        realTime).ConfigureAwait(false);
                    if (result.ResponseCode == Result.Success)
                    {
                        queryLogger.Log("Disambiguation scenario successfully started");
                        return result;
                    }

                    queryLogger.Log("Attempted to invoke the disambiguation scenario, but it failed (is reflection domain loaded and configured to handle this scenario?). Falling back to static ranking of hyps", LogLevel.Wrn);
                }
            }

            if (!_dialogConfig.IgnoreSideSpeech)
            {
                CreateHighConfSideSpeechHyp(ref results, _dialogConfig.MaxSideSpeechConfidence);
            }

            ArbitrateIdenticalConfidences(ref results, conversationStack.Peek());
            AddSideSpeechIfNotPresent(ref results);
            AddNoRecoIfNotPresent(ref results, textInput, speechInput);
            CapSideSpeechConfidence(ref results, _dialogConfig.MaxSideSpeechConfidence);

            // MASTER SORT OF INCOMING HYPOTHESES //
            results.Sort();

            // the incoming hypothesis list is now sorted into "dialog order", which is slightly different
            // from straight LU confidence because it factors in boosting, synthesized results, etc.
            foreach (RankedHypothesis rankedHyp in results)
            {
                RecoResult luInfo = rankedHyp.Result;
                
                queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Dialog is processing LU hypothesis {0}/{1}/{2}", luInfo.Domain, luInfo.Intent, luInfo.Confidence);

                int conversationFallbackDepth = 0;
                bool topConversationStateIsNonTenative = false;

                // Iterate through the conversation stack to handle the case where the top conversation state is tenative
                // and the incoming input triggers the continuation of a state lower down in the stack.
                foreach (ConversationState hypothesizedConversationState in conversationStack) // This iterator must go from top-of-stack downwards
                {
                    // Stop iterating through old conversation states after we reach a state that is not tenative
                    if (topConversationStateIsNonTenative)
                        break;
                    topConversationStateIsNonTenative = hypothesizedConversationState.LastMultiturnState.Continues && (hypothesizedConversationState.LastMultiturnState.IsImmediate || hypothesizedConversationState.LastMultiturnState.FullConversationControl);
                    conversationFallbackDepth++;

                    DialogProcessingFlags convFlags = new DialogProcessingFlags();

                    // True as long as the previous utterance started a multiturn conversation,
                    // either by locking the domain, or flagging tenative multiturn
                    convFlags[DialogFlag.InMultiturnConversation] = hypothesizedConversationState != null && hypothesizedConversationState.TurnNum > 0;
                    convFlags[DialogFlag.IgnoringSideSpeech] = _dialogConfig.IgnoreSideSpeech;
                    convFlags[DialogFlag.TenativeMultiturnEnabled] = hypothesizedConversationState != null &&
                                                    hypothesizedConversationState.LastMultiturnState.Continues &&
                                                    !hypothesizedConversationState.LastMultiturnState.IsImmediate &&
                                                    !hypothesizedConversationState.LastMultiturnState.FullConversationControl;

                    // If we are in a tenative multiturn scenario (i.e. there is an existing conversation state)
                    // and we fail out of the primary domain, we need to clear all context
                    convFlags[DialogFlag.UseEmptyConversationState] = false;
                    convFlags[DialogFlag.IsSideSpeech] = luInfo.Domain.Equals(_commonDomainName) &&
                                        (luInfo.Intent.Equals(DialogConstants.SIDE_SPEECH_INTENT) || luInfo.Intent.Equals(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT));
                    convFlags[DialogFlag.BelowConfidence] = luInfo.Confidence < _dialogConfig.MinPluginConfidence && !(convFlags[DialogFlag.IsSideSpeech] && !convFlags[DialogFlag.IgnoringSideSpeech]);
                    convFlags[DialogFlag.IsCommonCarryoverIntent] = luInfo.Domain.Equals(_commonDomainName);

                    // If we are not in multiturn, and we triggered side speech, break processing as soon as possible.
                    if (!convFlags[DialogFlag.InMultiturnConversation] && convFlags[DialogFlag.IsSideSpeech] && convFlags[DialogFlag.IgnoringSideSpeech])
                    {
                        queryLogger.Log("Ignored as side speech");
                        break;
                    }

                    // In a multi-turn scenario, don't apply the confidence cutoff. Keep iterating until something answers.
                    // Also, I need to prevent an error where tenative multiturn will allow confidences below the threshold
                    if (convFlags[DialogFlag.BelowConfidence] && (!convFlags[DialogFlag.InMultiturnConversation] || convFlags[DialogFlag.TenativeMultiturnEnabled]))
                    {
                        // We went below the confidence threshold for this domain; skip
                        queryLogger.Log(string.Format("Input {0}/{1}/{2} fell below confidence threshold; ignoring", luInfo.Domain, luInfo.Intent, luInfo.Confidence));
                        continue;
                    }

                    if (convFlags[DialogFlag.IsCommonCarryoverIntent] && !convFlags[DialogFlag.InMultiturnConversation] && !(convFlags[DialogFlag.IsSideSpeech] && !convFlags[DialogFlag.IgnoringSideSpeech]))
                    {
                        // Remember to skip common intents on the first turn (those are only for turn 2+ scenarios)
                        queryLogger.Log("Skipping common intent \"" + luInfo.Intent + "\" on first-turn");
                        continue;
                    }

                    // Allow the 1st-turn domain to rewrite the "common" domain value. So, effectively,
                    // "common/confirm" becomes "{original_domain}/confirm"
                    string actualConversationDomain = luInfo.Domain;

                    if (convFlags[DialogFlag.IsSideSpeech] && // If the incoming hyp is side speech AND
                        (!convFlags[DialogFlag.InMultiturnConversation] || // either we are not in multiturn,
                        (convFlags[DialogFlag.InMultiturnConversation] && convFlags[DialogFlag.TenativeMultiturnEnabled]) || // we are in tenative multiturn to some other domain
                        (hypothesizedConversationState != null && string.Equals(hypothesizedConversationState.CurrentPluginDomain, _sideSpeechDomainName)))) // or we are locked in multiturn to the side_speech domain
                    {
                        // Assign "meaningful" side-speech to its own domain, so plugins can consume or redirect it
                        actualConversationDomain = _sideSpeechDomainName;
                        convFlags[DialogFlag.DivertToSideSpeechDomain] = true;
                        queryLogger.Log("Potentially meaningful side-speech input; diverting to side speech domain");
                    }
                    else if (hypothesizedConversationState != null &&
                        !string.IsNullOrEmpty(hypothesizedConversationState.CurrentPluginDomain) &&
                        (!convFlags[DialogFlag.TenativeMultiturnEnabled] || convFlags[DialogFlag.IsCommonCarryoverIntent]) &&
                        (!actualConversationDomain.Equals(hypothesizedConversationState.CurrentPluginDomain)))
                    {
                        // This statement will lock the conversation domain to the current multiturn domain, if any
                        // This is only applicable if we are in multiturn but NOT tenative multiturn, as the domain
                        // is still flexible in those cases. The common domain is always overwritten, except side speech
                        // which is a special case, and even then only in first-turn (oh man this is complicated)
                        queryLogger.Log("Attempting to divert conversation domain from \"" + actualConversationDomain + "\" to \"" + hypothesizedConversationState.CurrentPluginDomain + "\"");
                        actualConversationDomain = hypothesizedConversationState.CurrentPluginDomain;
                    }

                    // Attempt to trigger the plugin that matches the resulting domain
                    Version pluginVersionConstraint = null;
                    if (hypothesizedConversationState != null &&
                        hypothesizedConversationState.CurrentPluginVersion.Major != 0 &&
                        hypothesizedConversationState.CurrentPluginVersion.Minor != 0)
                    {
                        pluginVersionConstraint = hypothesizedConversationState.CurrentPluginVersion;
                    }

                    PluginStrongName targetPluginStrongName = GetPluginForLUDomain(actualConversationDomain, pluginVersionConstraint);
                    LoadedPluginInformation targetPlugin;
                    Monitor.Enter(_pluginsLock);
                    if (targetPluginStrongName == null || !_allPlugins.TryGetValue(targetPluginStrongName, out targetPlugin))
                    {
                        Monitor.Exit(_pluginsLock);
                        queryLogger.Log("No plugin is registered to handle the domain \"" + actualConversationDomain + "\", continuing execution...", LogLevel.Wrn);
                    }
                    else
                    {
                        Monitor.Exit(_pluginsLock);
                        ConversationState actualConversationState = hypothesizedConversationState ?? new ConversationState();

                        // Get a new conversation tree for that plugin (on turn 1 only)
                        if (actualConversationState.ConversationTree == null && actualConversationState.TurnNum == 0)
                        {
                            actualConversationState.SetConversationTree(targetPlugin.ConversationTree);
                        }

                        // Ensure that this transition is valid. If not, skip.
                        IConversationNodeEdge nextConvoTransition = null;
                        if (actualConversationState.ConversationTree != null)
                        {
                            string treeDomain = luInfo.Domain;
                            if (treeDomain.Equals(_commonDomainName) && convFlags[DialogFlag.DivertToSideSpeechDomain])
                            {
                                treeDomain = _sideSpeechDomainName;
                            }

                            try
                            {
                                actualConversationState.ConversationTree.Transition(
                                    actualConversationState.CurrentNode, treeDomain, luInfo.Intent, out nextConvoTransition);
                            }
                            catch (DialogException e)
                            {
                                queryLogger.Log("Dialog exception while processing turn; most likely caused by old conversation states being used after breaking version changes in the plugin", LogLevel.Wrn);
                                queryLogger.Log(e, LogLevel.Wrn);
                                convFlags[DialogFlag.UseEmptyConversationState] = true;
                            }
                        }

                        // Indicates that the scope of the next conversation tree transition is marked as "external"
                        bool nextTurnExternalDomain = nextConvoTransition != null && (nextConvoTransition.Scope == DomainScope.External || nextConvoTransition.Scope == DomainScope.CommonExternal);
                        if (nextTurnExternalDomain)
                        {
                            queryLogger.Log("I get the feeling that this hyp will take us to an external domain");
                        }

                        bool isRetryEnabled = IsNoReco(luInfo) &&
                            !string.IsNullOrEmpty(actualConversationState.CurrentNode) &&
                            actualConversationState.ConversationTree != null &&
                            !string.IsNullOrEmpty(actualConversationState.ConversationTree.GetRetryHandlerName(actualConversationState.CurrentNode));

                        // Test if we are in a multi-turn conversation, and there is no valid state to go to
                        // This usually means that we are in tenative multiturn and have entered a new conversation
                        if (actualConversationState.ConversationTree != null && nextConvoTransition == null && !isRetryEnabled)
                        {
                            // Are we still locked in this domain? (this prevents a bug where you could restart
                            // a conversation after being locked in because the start node is considered valid)
                            if (convFlags[DialogFlag.InMultiturnConversation] && !convFlags[DialogFlag.TenativeMultiturnEnabled])
                            {
                                if (luInfo.Domain.Equals(actualConversationState.CurrentPluginDomain))
                                {
                                    queryLogger.Log("Since we are already in multiturn within the \"" + actualConversationState.CurrentPluginDomain + "\" domain, we will not restart the conversation");
                                }
                                else
                                {
                                    queryLogger.Log("Ignoring new domain conversation starter because we are already locked into multiturn for \"" + actualConversationState.CurrentPluginDomain + "\"");
                                }
                                continue;
                            }

                            convFlags[DialogFlag.UseEmptyConversationState] = true;
                            // Is the resulting state not a valid start state? Then skip it.
                            IConversationTree testTree = targetPlugin.ConversationTree;
                            
                            if (testTree != null)
                            {
                                if (convFlags[DialogFlag.DivertToSideSpeechDomain])
                                {
                                    if (!testTree.HasStartNode(_sideSpeechDomainName, luInfo.Intent))
                                    {
                                        queryLogger.Log("The side speech plugin is not configured to handle the intent \"" + luInfo.Intent +
                                                "\", skipping");
                                        continue;
                                    }
                                }
                                else if (!testTree.HasStartNode(luInfo.Domain, luInfo.Intent))
                                {
                                    queryLogger.Log(string.Format("No transition exists from \"{0}:{1}\" to intent \"{2}/{3}\"; skipping (did you forget to set MultiTurnBehavior = Continues in the previous turn response?)",
                                        actualConversationState.CurrentPluginId, actualConversationState.CurrentNode, luInfo.Domain, luInfo.Intent));
                                    continue;
                                }
                            }
                        }

                        bool crossDomainTriggered = nextTurnExternalDomain &&
                            actualConversationDomain.Equals(actualConversationState.CurrentPluginDomain);
                        if (crossDomainTriggered)
                        {
                            // See if we have a cross-domain query ("external" conversation scope,
                            // meaning we jump to entirely new conversation domain and apply slot carryover)
                            string newDomain = nextConvoTransition.ExternalDomain;
                            string newIntent = nextConvoTransition.ExternalIntent;
                            queryLogger.Log("Triggered cross-domain conversation scope: transitioning from \"" + luInfo.Domain + "/" + luInfo.Intent +
                                "\" to \"" + newDomain + "/" + newIntent + "\"");

                            PluginStrongName crossDomainTargetStrongName = GetPluginForLUDomain(newDomain);

                            // Ensure that the specified domain actually exists
                            lock (_pluginsLock)
                            {
                                if (!_allPlugins.ContainsKey(crossDomainTargetStrongName))
                                {
                                    queryLogger.Log("Crossdomain request: Target domain \"" + newDomain + "\" does not exist", LogLevel.Err);
                                    continue;
                                }
                            }
                            
                            // Ensure that they can respond to crossdomain function calls
                            CrossDomainRequestData cdRequestResult = await _pluginProvider.Value.CrossDomainRequest(crossDomainTargetStrongName, newIntent, queryLogger, realTime).ConfigureAwait(false);
                            if (cdRequestResult == null)
                            {
                                throw new DialogException("Domain \"" + newDomain + "\" does not support this cross domain request (target intent is \"" + newIntent + "\")");
                            }

                            CrossDomainContext cdrContext = new CrossDomainContext()
                            {
                                RequestDomain = newDomain,
                                RequestIntent = newIntent,
                                RequestedSlots = cdRequestResult.RequestedSlots,
                                PastConversationTurns = new List<RecoResult>(actualConversationState.PreviousConversationTurns)
                            };
                            
                            // For the purposes of cross-domain slots, we consider the current turn to be on the list of "past conversation turns", so append it to the end
                            // This is so that the current turn's input can affect the state of the transition
                            cdrContext.PastConversationTurns.Add(luInfo);
                            
                            CrossDomainResponseResponse cdResponseResponse = await _pluginProvider.Value.CrossDomainResponse(
                                targetPlugin.PluginStrongName,
                                cdrContext,
                                queryLogger,
                                actualConversationState.SessionStore,
                                globalUserProfile,
                                inputEntityContext,
                                realTime).ConfigureAwait(false);

                            if (cdResponseResponse == null || cdResponseResponse.PluginResponse == null)
                            {
                                throw new DialogException("Plugin \"" + targetPlugin.PluginId + "\" does not support this cross domain response (target intent is \"" + newIntent + "\")");
                            }

                            lock (_pluginsLock)
                            {
                                IConversationTree newTree = _allPlugins[crossDomainTargetStrongName].ConversationTree;
                                if (!newTree.HasStartNode(newDomain, newIntent))
                                {
                                    throw new DialogException("Intent \"" + newIntent + "\" doesn't exist or isn't enabled for 1st turn in domain \"" + newDomain + "\"");
                                }

                                // Actually perform the transition
                                // First, advance the caller's conversation tree to either its callback node or null
                                actualConversationState.TransitionToConversationNode(
                                    luInfo.Clone(),
                                    cdResponseResponse.PluginResponse.CallbackMultiturnBehavior,
                                    queryLogger,
                                    _maxConversationTurnLength.Value,
                                    realTime,
                                    targetConversationNode: null,
                                    pluginId: null,
                                    pluginVersion: null);

                                // If the caller has no callback, delete its conversation state entirely
                                if (actualConversationState.CurrentNode == null)
                                {
                                    conversationStack.Pop();
                                }

                                convFlags[DialogFlag.UseEmptyConversationState] = false;
                                actualConversationDomain = newDomain;
                                actualConversationState = new ConversationState();
                                actualConversationState.SetConversationTree(newTree);
                                luInfo.Domain = newDomain;
                                luInfo.Intent = newIntent;
                                // Push the new domain state to the convo stack
                                conversationStack.Push(actualConversationState);
                                // Carry the slots across. This operation will leave any existing slots,
                                // as well as the original utterance
                                foreach (SlotValue sco in cdResponseResponse.PluginResponse.FilledSlots)
                                {
                                    sco.Format = SlotValueFormat.CrossDomainTag;
                                    luInfo.MostLikelyTags.Slots.Add(sco);

                                    // Also copy slot entities to the current entity context
                                    foreach (ContextualEntity crossDomainEntity in sco.GetEntities(cdResponseResponse.OutEntityContext))
                                    {
                                        crossDomainEntity.Entity.CopyTo(inputEntityContext, true);
                                    }
                                }

                                targetPlugin = _allPlugins[crossDomainTargetStrongName];
                                targetPluginStrongName = crossDomainTargetStrongName;
                            }
                        }

                        // Honor the isNewConversation flag by forcing an empty conversation state
                        convFlags[DialogFlag.UseEmptyConversationState] = isNewConversation || convFlags[DialogFlag.UseEmptyConversationState];

                        ConversationState emptyState = new ConversationState();
                        emptyState.SetConversationTree(targetPlugin.ConversationTree);

                        // Pick up side effects from triggering, if there were any
                        InMemoryDataStore triggerTimeSessionStore = null;
                        if (triggerSideEffects != null && !triggerSideEffects.TryGetValue(targetPlugin.LUDomain + "/" + luInfo.Intent, out triggerTimeSessionStore))
                        {
                            triggerTimeSessionStore = null;
                        }

                        // Log detailed info about the dialog execution
                        convFlags.Log(queryLogger, LogLevel.Vrb);

                        // Get the user profiles, both global and plugin-specific
                        UserProfileCollection userProfiles = await RetrieveUserProfiles(clientContext.UserId, targetPlugin.PluginId, globalUserProfile, globalUserHistory, queryLogger).ConfigureAwait(false);

                        // If configured, make the global profile read-only to every plugin except a set of trusted ones (usually the reflection plugin)
                        IList<string> allowedProfileEditPlugins = _dialogConfig.AllowedGlobalProfileEditors;
                        if (allowedProfileEditPlugins != null)
                        {
                            if (userProfiles.GlobalProfile is InMemoryDataStore)
                            {
                                ((InMemoryDataStore)userProfiles.GlobalProfile).IsReadOnly = true;
                            }

                            foreach (string domain in allowedProfileEditPlugins)
                            {
                                if (targetPlugin.PluginId.Equals(domain))
                                {
                                    if (userProfiles.GlobalProfile is InMemoryDataStore)
                                    {
                                        ((InMemoryDataStore)userProfiles.GlobalProfile).IsReadOnly = false;
                                    }
                                    break;
                                }
                            }
                        }

                        globalUserProfile = userProfiles.GlobalProfile;
                        globalUserHistory = userProfiles.EntityHistory;
                        globalUserHistory.Touched = false;
                        
                        // Execute the plugin!
                        DialogProcessingResponse combinedResult = await CallSinglePlugin(
                            targetPlugin,
                            luInfo,
                            clientContext,
                            convFlags[DialogFlag.UseEmptyConversationState] ? emptyState : actualConversationState,
                            authLevel,
                            inputSource,
                            inputAudio,
                            queryLogger,
                            triggerTimeSessionStore,
                            userProfiles,
                            bargeInTime,
                            requestFlags,
                            speechInput,
                            inputEntityContext,
                            contextualEntities,
                            requestData,
                            realTime).ConfigureAwait(false);

                        PluginResult pluginResult = combinedResult.PluginOutput;
                        bool wasRetrying = combinedResult.WasRetrying;

                        // Augment privacy classification if it's specified in the configuration
                        if (pluginResult.ResponsePrivacyClassification == DataPrivacyClassification.Unknown &&
                            _dialogConfig.AssumePluginResponsesArePII)
                        {
                            pluginResult.ResponsePrivacyClassification = DataPrivacyClassification.PrivateContent;
                        }

                        if (pluginResult.ResponseCode == Result.Success)
                        {
                            if (convFlags[DialogFlag.UseEmptyConversationState])
                            {
                                actualConversationState = emptyState;
                            }

                            // FIXME ensure that the in memory entity history carries over properly

                            // If the updated profile / session store values are non-null, they must have been touched by the plugin. So for simplicity we just set the Touched flag here
                            if (combinedResult.UpdatedSessionStore != null)
                            {
                                combinedResult.UpdatedSessionStore.Touched = true;
                                queryLogger.Log(CommonInstrumentation.GenerateInstancedSizeEntry(CommonInstrumentation.Key_Size_Store_Session, targetPluginStrongName.PluginId, combinedResult.UpdatedSessionStore.SizeInBytes), LogLevel.Ins, privacyClass: DataPrivacyClassification.SystemMetadata);
                                if (!string.Equals(DialogConstants.REFLECTION_DOMAIN, targetPluginStrongName.PluginId) &&
                                    combinedResult.UpdatedSessionStore.SizeInBytes > _dialogConfig.MaxStoreSizeBytes)
                                {
                                    throw new DialogException("Session store cannot hold more than " + _dialogConfig.MaxStoreSizeBytes + " bytes of data (current size is " + combinedResult.UpdatedSessionStore.SizeInBytes + " bytes)");
                                }
                            }
                            if (combinedResult.UpdatedLocalUserProfile != null)
                            {
                                combinedResult.UpdatedLocalUserProfile.Touched = true;
                                queryLogger.Log(CommonInstrumentation.GenerateInstancedSizeEntry(CommonInstrumentation.Key_Size_Store_LocalProfile, targetPluginStrongName.PluginId, combinedResult.UpdatedLocalUserProfile.SizeInBytes), LogLevel.Ins, privacyClass: DataPrivacyClassification.SystemMetadata);
                                if (!string.Equals(DialogConstants.REFLECTION_DOMAIN, targetPluginStrongName.PluginId) &&
                                    combinedResult.UpdatedLocalUserProfile.SizeInBytes > _dialogConfig.MaxStoreSizeBytes)
                                {
                                    throw new DialogException("Local user profile cannot hold more than " + _dialogConfig.MaxStoreSizeBytes + " bytes of data (current size is " + combinedResult.UpdatedLocalUserProfile.SizeInBytes + " bytes)");
                                }
                            }
                            if (combinedResult.UpdatedGlobalUserProfile != null)
                            {
                                combinedResult.UpdatedGlobalUserProfile.Touched = true;
                                queryLogger.Log(CommonInstrumentation.GenerateInstancedSizeEntry(CommonInstrumentation.Key_Size_Store_GlobalProfile, targetPluginStrongName.PluginId, combinedResult.UpdatedGlobalUserProfile.SizeInBytes), LogLevel.Ins, privacyClass: DataPrivacyClassification.SystemMetadata);
                                if (!string.Equals(DialogConstants.REFLECTION_DOMAIN, targetPluginStrongName.PluginId) &&
                                    combinedResult.UpdatedGlobalUserProfile.SizeInBytes > _dialogConfig.MaxStoreSizeBytes)
                                {
                                    throw new DialogException("Global user profile cannot hold more than " + _dialogConfig.MaxStoreSizeBytes + " bytes of data (current size is " + combinedResult.UpdatedGlobalUserProfile.SizeInBytes + " bytes)");
                                }
                            }
                            if (combinedResult.UpdatedEntityHistory != null)
                            {
                                combinedResult.UpdatedEntityHistory.Touched = true;
                            }

                            UserProfileCollection updatedUserProfiles = new UserProfileCollection(
                                combinedResult.UpdatedLocalUserProfile ?? userProfiles.LocalProfile,
                                combinedResult.UpdatedGlobalUserProfile ?? userProfiles.GlobalProfile,
                                combinedResult.UpdatedEntityHistory ?? userProfiles.EntityHistory);

                            PluginPostExecutionClosure closure = new PluginPostExecutionClosure()
                            {
                                Plugin = targetPlugin,
                                UserProfiles = updatedUserProfiles,
                                BeginningConversationState = actualConversationState,
                                PluginResult = pluginResult,
                                ConversationFallbackDepth = conversationFallbackDepth,
                                ConversationFlags = convFlags,
                                ConversationStack = conversationStack,
                                UnderstandingResult = luInfo,
                                PluginVersion = targetPluginStrongName.Version
                            };

                            // Update user profiles and conversation state in parallel
                            Task userProfileUpdateTask = WritebackUserProfilesAfterPluginExecution(
                                targetPlugin,
                                updatedUserProfiles,
                                clientContext,
                                queryLogger);
                            Task conversationStateUpdateTask = WritebackStateAfterPluginExecution(
                                closure,
                                clientContext,
                                inputSource,
                                queryLogger,
                                realTime);
                            Task dialogActionCacheWriteTask = WritebackDialogActionsAfterPluginExecution(
                                combinedResult.UpdatedDialogActions,
                                pluginResult.MultiTurnResult.ConversationTimeoutSeconds,
                                realTime,
                                queryLogger);
                            Task webDataCacheWriteTask = WritebackWebDataAfterPluginExecution(
                                combinedResult.UpdatedWebDataCache,
                                queryLogger,
                                realTime);

                            ValueStopwatch stateWriteTimer = ValueStopwatch.StartNew();
                            await userProfileUpdateTask.ConfigureAwait(false);
                            await conversationStateUpdateTask.ConfigureAwait(false);
                            await dialogActionCacheWriteTask.ConfigureAwait(false);
                            await webDataCacheWriteTask.ConfigureAwait(false);
                            stateWriteTimer.Stop();
                            queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_WritebackAllState, ref stateWriteTimer), LogLevel.Ins);

                            // Now, this could get heavy. If the plugin returned an InvokedDialogAction, we need to recurse and redo everything that we just did,
                            // but operating on the invoked action instead. This allows 2 or more plugins to execute for the same query, and work cooperatively
                            // in things like cross-domain handoffs with parameters
                            if (pluginResult.InvokedDialogAction != null)
                            {
                                if (string.IsNullOrEmpty(pluginResult.InvokedDialogAction.Domain) || string.IsNullOrEmpty(pluginResult.InvokedDialogAction.Intent))
                                {
                                    throw new DialogException("The plugin \"" + targetPlugin.PluginId + "\" attempted to invoke a dialog action, but the target domain or intent was unspecified");
                                }

                                string invocationTargetIntent = pluginResult.InvokedDialogAction.Domain + "/" + pluginResult.InvokedDialogAction.Intent;

                                // Special case for handling the callback after disambiguation.
                                // When this arises, we need to do a lot of black magic to restore the original conversation stack
                                // and rewind time to the input state that happened 2 turns ago (when the ambiguous query was first given)
                                if (invocationTargetIntent.Equals(DialogConstants.REFLECTION_DOMAIN + "/disambiguation_callback"))
                                {
                                    dialogEngineResult = await HandleDisambiguationCallback(
                                        pluginResult.InvokedDialogAction,
                                        actualConversationState.SessionStore,
                                        traceId,
                                        authLevel,
                                        conversationStack,
                                        queryLogger,
                                        bargeInTime,
                                        requestFlags,
                                        realTime).ConfigureAwait(false);
                                    return dialogEngineResult;
                                }
                                else
                                {
                                    queryLogger.Log("The dialog plugin " + targetPlugin.PluginId + " returned an invokable dialog action which is diverting to " + invocationTargetIntent);
                                    List<RankedHypothesis> dialogActionHyps = BuildRankedHypsFromInvokedDialogAction(targetPlugin.LUDomain, pluginResult.InvokedDialogAction);
                                    dialogEngineResult = await Process(
                                        results: dialogActionHyps,
                                        clientContext: clientContext,
                                        authLevel: authLevel,
                                        inputSource: inputSource,
                                        isNewConversation: false,
                                        useTriggers: false, // todo: should triggers apply on this pass? I'm setting it to false for now.
                                        traceId: traceId,
                                        queryLogger: queryLogger,
                                        inputAudio: inputAudio,
                                        textInput: textInput,
                                        speechInput: speechInput,
                                        conversationStack: conversationStack,
                                        triggerSideEffects: null,
                                        bargeInTime: bargeInTime,
                                        requestFlags: requestFlags,
                                        inputEntityContext: inputEntityContext,
                                        contextualEntities: contextualEntities,
                                        requestData: requestData,
                                        realTime: realTime).ConfigureAwait(false);

                                    if (dialogEngineResult.ResponseCode == Result.Skip)
                                    {
                                        // This means that the callback intent was ignored or doesn't exist. Surface an explicit error in this case
                                        queryLogger.Log("A dialog action was invoked, but the invocation target either ignored the request, or the intent \"" + invocationTargetIntent + "\" doesn't exist!", LogLevel.Err);
                                        throw new DialogException("The plugin \"" + targetPlugin.PluginId + "\" attempted to invoke a dialog action callback to \"" + invocationTargetIntent + "\", but the target intent does not exist or did not respond");
                                    }

                                    return dialogEngineResult;
                                }
                            }
                            else
                            {
                                dialogEngineResult = BuildCompleteResponseFromPluginResult(
                                    pluginResult,
                                    luInfo,
                                    queryLogger,
                                    actualConversationDomain,
                                    luInfo.Intent,
                                    clientContext.Locale,
                                    wasRetrying,
                                    targetPluginStrongName);
                            }

                            queryLogger.Log("Plugin \"" + targetPlugin.PluginId + "\" reported a success", LogLevel.Vrb);
                        }
                        else if (pluginResult.ResponseCode == Result.Failure)
                        {
                            dialogEngineResult.ResponseCode = Result.Failure;
                            queryLogger.Log("Plugin \"" + targetPlugin.PluginId + "\" reported a failure", LogLevel.Vrb);
                            dialogEngineResult.SelectedRecoResult = luInfo;
                            if (!string.IsNullOrWhiteSpace(pluginResult.ErrorMessage))
                            {
                                dialogEngineResult.ErrorMessage = pluginResult.ErrorMessage;
                            }
                            else
                            {
                                dialogEngineResult.ErrorMessage = "Unspecified plugin error in \"" + luInfo.Domain + "/" + luInfo.Intent + "\"";
                            }
                            
                            await _conversationStateCache.Value.ClearBothStates(clientContext.UserId, clientContext.ClientId, queryLogger.Clone("SessionStore"), true).ConfigureAwait(false);
                            queryLogger.Log("Cleared conversation state");
                        }
                        else if (pluginResult.ResponseCode == Result.Skip &&
                            bestSkipResult == null &&
                            (!string.IsNullOrEmpty(pluginResult.ErrorMessage) ||
                                !string.IsNullOrEmpty(pluginResult.ResponseText) ||
                                !string.IsNullOrEmpty(pluginResult.ResponseHtml) ||
                                !string.IsNullOrEmpty(pluginResult.ResponseSsml) ||
                                !string.IsNullOrEmpty(pluginResult.ResponseUrl) ||
                                pluginResult.ResponseAudio != null))
                        {
                            // If the plugin skipped, but returned some useful info, capture it.
                            bestSkipResult = this.BuildCompleteResponseFromPluginResult(pluginResult, luInfo, queryLogger, luInfo.Domain, luInfo.Intent, clientContext.Locale, wasRetrying, targetPluginStrongName);

                            // FIXME how the heck are side effects managed in this case? Is the plugin allowed to update session state or entity history??
                            UserProfileCollection updatedUserProfiles = new UserProfileCollection(combinedResult.UpdatedLocalUserProfile, combinedResult.UpdatedGlobalUserProfile, userProfiles.EntityHistory);
                            bestSkipResultClosure = new PluginPostExecutionClosure()
                            {
                                Plugin = targetPlugin,
                                UserProfiles = updatedUserProfiles,
                                BeginningConversationState = convFlags[DialogFlag.UseEmptyConversationState] ? emptyState : actualConversationState,
                                PluginResult = pluginResult,
                                ConversationFallbackDepth = conversationFallbackDepth,
                                ConversationFlags = convFlags,
                                ConversationStack = conversationStack,
                                UnderstandingResult = luInfo,
                                PluginVersion = targetPluginStrongName.Version
                            };
                        }

                        if (pluginResult.ResponseCode != Result.Skip)
                        {
                            // Break out of the loop, unless the plugin was skipped over
                            return dialogEngineResult;
                        }

                        queryLogger.Log("Plugin \"" + targetPlugin.PluginId + "\" declined to respond", LogLevel.Vrb);
                        // Reset the conversation state
                        // TODO: Does this need more logic?
                        if (!convFlags[DialogFlag.TenativeMultiturnEnabled])
                        {
                            actualConversationState.SetConversationTree(null);
                        }
                    }
                }
            }

            // We can only reach this point if no plugin returned a success (or all of them declined to answer)
            queryLogger.Log("Dialog core ignored all inputs (this usually means that the query was invalid for this conversation context or too low of confidence, and no retry handler is configured)");

            // This behavior can seem a bit strange so I'll explain.
            // If all dialog responses return "skip", what we do is return the first non-empty skip result if one exists.
            // What this allows in the typical case is for all plugins to generate a dismissal response that is tailored to their own domain
            // while still allowing lower-ranked plugins to take precendence.
            // For example, if input was "When is tool time", the "time" domain could trigger and generate a skip response "I don't know when tool time is",
            // and the side_speech fallback domain could generate a skip response "I don't know what you mean". Then when execution reaches this point the
            // dialog engine will be able to return the more "precise" dismissal response while still allowing the possibility that a "calendar" domain could
            // come in and take precendence with a Success result that matches a calendar item or something
            if (bestSkipResult != null)
            {
                queryLogger.Log("There is a best candidate skip result, so I will return that");
                dialogEngineResult = bestSkipResult;
                dialogEngineResult.ResponseCode = Result.Success;

                // FIXME Do we allow a skip answer to update the user profile? That would be a very strange edge case.....
                //Task userProfileUpdateTask = WritebackUserProfilesAfterPluginExecution(
                //    bestSkipResultClosure.Plugin,
                //    bestSkipResultClosure.UserProfiles,
                //    clientContext,
                //    queryLogger);
                Task conversationStateUpdateTask = WritebackStateAfterPluginExecution(
                    bestSkipResultClosure,
                    clientContext,
                    inputSource,
                    queryLogger,
                    realTime);

                ValueStopwatch stateWriteTimer = ValueStopwatch.StartNew();
                //await userProfileUpdateTask;
                await conversationStateUpdateTask.ConfigureAwait(false);
                stateWriteTimer.Stop();
                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_WritebackAllState, ref stateWriteTimer), LogLevel.Ins);
            }
            else
            {
                dialogEngineResult.ResponseCode = Result.Skip;
                dialogEngineResult.NextTurnBehavior = originalMultiturnState;
            }

            if (dialogEngineResult.NextTurnBehavior.Continues)
            {
                queryLogger.Log("Next turn continues = true; Tenative multiturn = " + !dialogEngineResult.NextTurnBehavior.IsImmediate);
            }
            else
            {
                queryLogger.Log("Next turn continues = false");
            }

            return dialogEngineResult;
        }

        /// <summary>
        /// Attempts to find the highest-versioned plugin that is registered with the specified LU domain
        /// </summary>
        /// <param name="luDomain"></param>
        /// <param name="alreadyRunningVersionConstraint"></param>
        /// <returns></returns>
        private PluginStrongName GetPluginForLUDomain(string luDomain, Version alreadyRunningVersionConstraint = null)
        {
            Version maxAllowedVersion = alreadyRunningVersionConstraint == null ? new Version(int.MaxValue, int.MaxValue) : new Version(alreadyRunningVersionConstraint.Major + 1, 0);
            Version highestVersionFound = new Version(0, 0);
            PluginStrongName returnVal = null;

            lock (_pluginsLock)
            {
                foreach (var plugin in _allPlugins.Values)
                {
                    if (string.Equals(plugin.LUDomain, luDomain))
                    {
                        Version thisEntryVersion = plugin.PluginStrongName.Version;
                        if (thisEntryVersion < maxAllowedVersion)
                        {
                            if (returnVal == null ||
                                thisEntryVersion > highestVersionFound)
                            {
                                returnVal = plugin.PluginStrongName;
                                highestVersionFound = thisEntryVersion;
                            }
                        }
                    }
                }
            }

            return returnVal;
        }

        private async Task WritebackUserProfilesAfterPluginExecution(
            LoadedPluginInformation targetPlugin,
            UserProfileCollection userProfiles,
            ClientContext clientContext,
            ILogger queryLogger)
        {
            userProfiles.EntityHistory.Turn();
            if (userProfiles.LocalProfile.Touched || userProfiles.GlobalProfile.Touched || userProfiles.EntityHistory.Touched)
            {
                UserProfileType typesToUpdate = UserProfileType.None;
                if (userProfiles.LocalProfile.Touched)
                {
                    queryLogger.Log("Persisting local user profile for domain " + targetPlugin.PluginId, LogLevel.Std);
                    typesToUpdate |= UserProfileType.PluginLocal;
                }
                if (userProfiles.GlobalProfile.Touched)
                {
                    queryLogger.Log("Persisting global user profile", LogLevel.Std);
                    typesToUpdate |= UserProfileType.PluginGlobal;
                }
                if (userProfiles.EntityHistory.Touched)
                {
                    queryLogger.Log("Persisting global entity history", LogLevel.Std);
                    typesToUpdate |= UserProfileType.EntityHistoryGlobal;
                }

                // OPT: this could be fire-and-forget rather than awaitable
                ValueStopwatch profileWriteTimer = ValueStopwatch.StartNew();
                await _userProfiles.UpdateProfiles(typesToUpdate, userProfiles, clientContext.UserId, targetPlugin.PluginId, queryLogger).ConfigureAwait(false);
                profileWriteTimer.Stop();
                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_UserProfileWriteHotPath, ref profileWriteTimer), LogLevel.Ins);
            }
        }

        private Task WritebackDialogActionsAfterPluginExecution(
            InMemoryDialogActionCache updatedDialogActions,
            int conversationExpireTimeSeconds,
            IRealTimeProvider realTime,
            ILogger queryLogger)
        {
            if (updatedDialogActions != null && updatedDialogActions.Count > 0)
            {
                queryLogger.Log("Writing back " + updatedDialogActions.Count + " cached dialog actions");

                IList<CachedItem<DialogAction>> actionsToWrite = new List<CachedItem<DialogAction>>(updatedDialogActions.GetAllItems());
                foreach (CachedItem<DialogAction> action in actionsToWrite)
                {
                    // If the cached actions have no expire time set, make it equal to the lifetime of the conversation plus 30 seconds
                    if (!action.ExpireTime.HasValue)
                    {
                        TimeSpan newLifetime = TimeSpan.FromSeconds(conversationExpireTimeSeconds + 30);
                        action.LifeTime = newLifetime;
                        action.ExpireTime = realTime.Time.Add(newLifetime);
                    }
                }

                return _dialogActionCache.Value.Store(actionsToWrite, true, queryLogger, realTime);
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        private Task WritebackWebDataAfterPluginExecution(
            InMemoryWebDataCache updatedWebData,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            if (updatedWebData != null && updatedWebData.Count > 0)
            {
                queryLogger.Log("Writing back " + updatedWebData.Count + " cached web data items");

                IList<CachedItem<CachedWebData>> actionsToWrite = new List<CachedItem<CachedWebData>>(updatedWebData.GetAllItems());
                return _webDataCache.Value.Store(actionsToWrite, true, queryLogger, realTime);
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        /// <summary>
        /// Closure used to store the state of a plugin after execution for the purpose of updating its conversation state after execution.
        /// </summary>
        private class PluginPostExecutionClosure
        {
            public LoadedPluginInformation Plugin;
            public PluginResult PluginResult;
            public RecoResult UnderstandingResult;
            public Stack<ConversationState> ConversationStack;
            public ConversationState BeginningConversationState;
            public DialogProcessingFlags ConversationFlags;
            public int ConversationFallbackDepth;
            public UserProfileCollection UserProfiles;
            public Version PluginVersion;
        }

        private async Task WritebackStateAfterPluginExecution(
            PluginPostExecutionClosure stateClosure,
            ClientContext clientContext,
            InputMethod inputSource,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            // Update the conversation stack
            // If we fell back to a lower conversation state in the stack, we clear all states above it
            // Note that this count will always be greater than 0 because we also need to pop
            // the old state for the current domain
            int conversationFallbackDepth = Math.Min(stateClosure.ConversationFallbackDepth, stateClosure.ConversationStack.Count);
            for (int popCount = 0; popCount < conversationFallbackDepth; popCount++)
            {
                stateClosure.ConversationStack.Pop();
            }

            if (stateClosure.PluginResult.MultiTurnResult != null && stateClosure.PluginResult.MultiTurnResult.Continues)
            {
                RecoResult transitionRecoResult = stateClosure.UnderstandingResult.Clone();
                // Make sure we don't even have "common" as the root domain; since no plugin actually exists to match it
                if (transitionRecoResult.Domain.Equals(_commonDomainName) && stateClosure.ConversationFlags[DialogFlag.DivertToSideSpeechDomain])
                {
                    transitionRecoResult.Domain = _sideSpeechDomainName;
                }

                if (!string.IsNullOrEmpty(stateClosure.PluginResult.ContinuationFuncName))
                {
                    // If the plugin returned an explicit continuation, validate it
                    stateClosure.BeginningConversationState.TransitionToContinuation(
                        transitionRecoResult,
                        stateClosure.PluginResult.MultiTurnResult,
                        queryLogger.Clone("DialogStateManager"),
                        _maxConversationTurnLength.Value,
                        realTime,
                        stateClosure.PluginResult.ContinuationFuncName,
                        stateClosure.Plugin.PluginId,
                        stateClosure.PluginVersion);

                    stateClosure.ConversationStack.Push(stateClosure.BeginningConversationState);
                }
                else
                {
                    // If the conversation continues, advance our position in the conversation tree
                    stateClosure.BeginningConversationState.TransitionToConversationNode(
                        transitionRecoResult,
                        stateClosure.PluginResult.MultiTurnResult,
                        queryLogger.Clone("DialogStateManager"),
                        _maxConversationTurnLength.Value,
                        realTime,
                        stateClosure.PluginResult.ResultConversationNode,
                        stateClosure.Plugin.PluginId,
                        stateClosure.PluginVersion);

                    stateClosure.ConversationStack.Push(stateClosure.BeginningConversationState);
                }
            }
            else if (stateClosure.ConversationStack.Count > 0)
            {
                // Did we fall back to a lower domain just now? If so, ensure that its multiturn behavior is surfaced, rather
                // than the behavior of the domain that just failed
                stateClosure.PluginResult.MultiTurnResult = stateClosure.ConversationStack.Peek().LastMultiturnState;
            }

            Stack<ConversationState> convertedConversationStack = stateClosure.ConversationStack;

            // Pop all finished conversation states from the stack (fixme: this should never actually be necessary?)
            while (convertedConversationStack.Count > 0 && !convertedConversationStack.Peek().LastMultiturnState.Continues)
            {
                convertedConversationStack.Pop();
            }

            // And save it
            if (convertedConversationStack.Count > 0)
            {
                ConversationState topConvoState = convertedConversationStack.Peek();
                queryLogger.LogFormat(
                    LogLevel.Std,
                    DataPrivacyClassification.SystemMetadata,
                    "Final conversation state: current domain = {0} currentNode = {1} turnNum = {2} retryNum = {3}",
                    topConvoState.CurrentPluginDomain,
                    topConvoState.CurrentNode,
                    topConvoState.TurnNum,
                    topConvoState.RetryNum);

                ValueStopwatch sessionStoreTimer = ValueStopwatch.StartNew();
                //queryLogger.Log("Saving client state " + clientContext.ClientId, LogLevel.Vrb);
                Task clientStateSaveTask = _conversationStateCache.Value.SetClientSpecificState(clientContext.UserId, clientContext.ClientId, convertedConversationStack, queryLogger.Clone("SessionStore"), fireAndForget: true);
                Task roamingStateSaveTask = DurandalTaskExtensions.NoOpTask;

                // If this is the first turn or the input method is not programmatic, we can assume that the user is actively interacting with this client.
                // Therefore we update the roaming conversation state as well so they can carry their conversation to other devices
                if (!stateClosure.ConversationFlags[DialogFlag.InMultiturnConversation] || inputSource == InputMethod.Typed || inputSource == InputMethod.Spoken)
                {
                    //queryLogger.Log("Saving roaming user state " + clientContext.UserId, LogLevel.Vrb);
                    roamingStateSaveTask = _conversationStateCache.Value.SetRoamingState(clientContext.UserId, convertedConversationStack, queryLogger.Clone("SessionStore"), fireAndForget: true);
                }

                // Writeback client states in parallel
                // Since we specified fire-and-forget storage above, these awaits don't actually block anything significant
                await clientStateSaveTask.ConfigureAwait(false);
                await roamingStateSaveTask.ConfigureAwait(false);

                sessionStoreTimer.Stop();
                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_SessionWriteHotPath, ref sessionStoreTimer), LogLevel.Ins);
                queryLogger.Log("Queued async conversation state write", LogLevel.Std);
            }
            else
            {
                await _conversationStateCache.Value.ClearBothStates(clientContext.UserId, clientContext.ClientId, queryLogger.Clone("SessionStore"), true).ConfigureAwait(false);
                queryLogger.Log("Conversation stack has exhausted; all state for user is cleared", LogLevel.Std);
            }
        }

#endregion

#region Events

        public event EventHandler<PluginRegisteredEventArgs> PluginRegistered;

        private void OnPluginRegistered(PluginStrongName pluginId)
        {
            int pluginCount;
            Monitor.Enter(_pluginsLock);
            pluginCount = _allPlugins.Count;
            Monitor.Exit(_pluginsLock);
            PluginRegisteredEventArgs args = new PluginRegisteredEventArgs()
                {
                    PluginId = pluginId,
                    LoadedPluginCount = pluginCount
                };

            if (PluginRegistered != null)
            {
                PluginRegistered(this, args);
            }
        }

#endregion
    }
}
