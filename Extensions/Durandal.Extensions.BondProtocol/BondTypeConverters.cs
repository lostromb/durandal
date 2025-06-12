using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.NLP.Language;
using Durandal.Common.Collections;
using Durandal.Common.Ontology;
using Durandal.Common.Utils;
using Durandal.Common.Cache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using BONDAPI = Durandal.Extensions.BondProtocol.API;
using BONDREMOTING = Durandal.Extensions.BondProtocol.Remoting;

using CAPI = Durandal.API;
using CREMOTING = Durandal.Common.Remoting.Protocol;

namespace Durandal.Extensions.BondProtocol
{
    /// <summary>
    /// Static handcrafted converters for all of the bond types that we specify in the entire bond protocol package
    /// </summary>
    public static class BondTypeConverters
    {
        #region AudioData

        public static CAPI.AudioData Convert(BONDAPI.AudioData source)
        {
            if (source == null) return null;
            CAPI.AudioData target = new CAPI.AudioData();
            target.Codec = source.Codec;
            target.CodecParams = source.CodecParams;
            target.Data = source.Data;
            return target;
        }

        public static BONDAPI.AudioData Convert(CAPI.AudioData source)
        {
            if (source == null) return null;
            BONDAPI.AudioData target = new BONDAPI.AudioData();
            target.Codec = source.Codec;
            target.CodecParams = source.CodecParams;
            target.Data = source.Data;
            return target;
        }

        #endregion

        #region AudioResponse

        public static CAPI.AudioResponse Convert(BONDAPI.AudioResponse source)
        {
            if (source == null) return null;
            CAPI.AudioResponse target = new CAPI.AudioResponse(Convert(source.Data), Convert(source.Ordering));
            return target;
        }

        public static BONDAPI.AudioResponse Convert(CAPI.AudioResponse source)
        {
            if (source == null) return null;
            BONDAPI.AudioResponse target = new BONDAPI.AudioResponse();
            target.Data = Convert(source.Data);
            target.Ordering = Convert(source.Ordering);
            return target;
        }

        #endregion
        
        #region AudioOrdering

        public static CAPI.AudioOrdering Convert(BONDAPI.AudioOrdering source)
        {
            return (CAPI.AudioOrdering)source;
        }

        public static BONDAPI.AudioOrdering Convert(CAPI.AudioOrdering source)
        {
            return (BONDAPI.AudioOrdering)source;
        }

        #endregion

        #region BoostingOption

        public static CAPI.BoostingOption Convert(BONDAPI.BoostingOption source)
        {
            return (CAPI.BoostingOption)source;
        }

        public static BONDAPI.BoostingOption Convert(CAPI.BoostingOption source)
        {
            return (BONDAPI.BoostingOption)source;
        }

        #endregion

        #region CachedWebData

        public static CAPI.CachedWebData Convert(BONDAPI.CachedWebData source)
        {
            if (source == null) return null;
            CAPI.CachedWebData target = new CAPI.CachedWebData();
            target.Data = source.Data;
            target.TraceId = CommonInstrumentation.TryParseTraceIdGuid(source.TraceId);
            target.MimeType = source.MimeType;
            target.LifetimeSeconds = source.LifetimeSeconds;
            return target;
        }

        public static BONDAPI.CachedWebData Convert(CAPI.CachedWebData source)
        {
            if (source == null) return null;
            BONDAPI.CachedWebData target = new BONDAPI.CachedWebData();
            target.Data = source.Data;
            target.TraceId = CommonInstrumentation.FormatTraceId(source.TraceId);
            target.MimeType = source.MimeType ?? string.Empty;
            target.LifetimeSeconds = source.LifetimeSeconds;
            return target;
        }

        #endregion

        #region ClientAuthenticationScope

        public static CAPI.ClientAuthenticationScope Convert(BONDAPI.ClientAuthenticationScope source)
        {
            return (CAPI.ClientAuthenticationScope)source;
        }

        public static BONDAPI.ClientAuthenticationScope Convert(CAPI.ClientAuthenticationScope source)
        {
            return (BONDAPI.ClientAuthenticationScope)source;
        }

        #endregion

        #region ClientAuthenticationLevel

        public static CAPI.ClientAuthenticationLevel Convert(BONDAPI.ClientAuthenticationLevel source)
        {
            return (CAPI.ClientAuthenticationLevel)source;
        }

        public static BONDAPI.ClientAuthenticationLevel Convert(CAPI.ClientAuthenticationLevel source)
        {
            return (BONDAPI.ClientAuthenticationLevel)source;
        }

        #endregion

        #region ClientContext

        public static CAPI.ClientContext Convert(BONDAPI.ClientContext source)
        {
            if (source == null) return null;
            CAPI.ClientContext target = new CAPI.ClientContext();
            target.Capabilities = (CAPI.ClientCapabilities)source.Capabilities;
            target.ClientId = source.ClientId;
            target.ClientName = source.ClientName;
            target.ExtraClientContext = source.ExtraClientContext;
            target.Latitude = source.Latitude;
            target.Locale = LanguageCode.TryParse(source.Locale);
            target.LocationAccuracy = source.LocationAccuracy;
            target.Longitude = source.Longitude;
            target.ReferenceDateTime = source.ReferenceDateTime;
            target.SupportedClientActions = source.SupportedClientActions;
            target.UserId = source.UserId;
            target.UTCOffset = source.UTCOffset;
            target.UserTimeZone = source.UserTimeZone;
            return target;
        }

        public static BONDAPI.ClientContext Convert(CAPI.ClientContext source)
        {
            if (source == null) return null;
            BONDAPI.ClientContext target = new BONDAPI.ClientContext();
            target.Capabilities = (uint)source.Capabilities;
            target.ClientId = source.ClientId ?? string.Empty;
            target.ClientName = source.ClientName;
            target.ExtraClientContext = source.ExtraClientContext;
            target.Latitude = source.Latitude;
            if (source.Locale != null)
            {
                target.Locale = source.Locale.ToBcp47Alpha2String();
            }
            else
            {
                target.Locale = string.Empty;
            }

            target.LocationAccuracy = source.LocationAccuracy;
            target.Longitude = source.Longitude;
            target.ReferenceDateTime = source.ReferenceDateTime;
            target.SupportedClientActions = source.SupportedClientActions;
            target.UserId = source.UserId ?? string.Empty;
            target.UTCOffset = source.UTCOffset;
            target.UserTimeZone = source.UserTimeZone;
            return target;
        }

        #endregion

        #region CachedItem<DialogAction>

        public static List<KeyValuePair<string, CachedItem<CAPI.CachedWebData>>> Convert(List<BONDAPI.InMemoryCachedWebData> source)
        {
            if (source == null) return null;
            List<KeyValuePair<string, CachedItem<CAPI.CachedWebData>>> target = new List<KeyValuePair<string, CachedItem<CAPI.CachedWebData>>>();
            foreach (BONDAPI.InMemoryCachedWebData s in source)
            {
                target.Add(new KeyValuePair<string, CachedItem<CAPI.CachedWebData>>(s.Key, Convert(s)));
            }

            return target;
        }

        public static List<BONDAPI.InMemoryCachedWebData> Convert(IEnumerable<CachedItem<CAPI.CachedWebData>> source)
        {
            if (source == null) return null;
            List<BONDAPI.InMemoryCachedWebData> target = new List<BONDAPI.InMemoryCachedWebData>();
            foreach (CachedItem<CAPI.CachedWebData> s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CachedItem<CAPI.CachedWebData> Convert(BONDAPI.InMemoryCachedWebData source)
        {
            CAPI.CachedWebData convertedAction = Convert(source.Value);
            TimeSpan? convertedLifespan = null;
            if (source.LifeTimeSeconds.HasValue)
            {
                convertedLifespan = TimeSpan.FromSeconds(source.LifeTimeSeconds.Value);
            }

            DateTimeOffset? convertedExpireTime = null;
            if (source.ExpireTimeUtcTicks.HasValue)
            {
                convertedExpireTime = new DateTimeOffset(source.ExpireTimeUtcTicks.Value, TimeSpan.Zero);
            }

            CachedItem<CAPI.CachedWebData> convertedItem = new CachedItem<CAPI.CachedWebData>(source.Key, convertedAction, convertedLifespan, convertedExpireTime);
            return convertedItem;
        }

        public static BONDAPI.InMemoryCachedWebData Convert(CachedItem<CAPI.CachedWebData> source)
        {
            BONDAPI.InMemoryCachedWebData target = new BONDAPI.InMemoryCachedWebData();
            target.Key = source.Key;
            target.Value = Convert(source.Item);
            target.LifeTimeSeconds = null;
            if (source.LifeTime.HasValue)
            {
                target.LifeTimeSeconds = (int)source.LifeTime.Value.TotalSeconds;
            }

            target.ExpireTimeUtcTicks = null;
            if (source.ExpireTime.HasValue)
            {
                target.ExpireTimeUtcTicks = source.ExpireTime.Value.Ticks;
            }

            return target;
        }

        #endregion

        #region CachedItem<DialogAction>

        public static List<KeyValuePair<string, CachedItem<CAPI.DialogAction>>> Convert(List<BONDAPI.InMemoryCachedDialogAction> source)
        {
            if (source == null) return null;
            List<KeyValuePair<string, CachedItem<CAPI.DialogAction>>> target = new List<KeyValuePair<string, CachedItem<CAPI.DialogAction>>>();
            foreach (BONDAPI.InMemoryCachedDialogAction s in source)
            {
                target.Add(new KeyValuePair<string, CachedItem<CAPI.DialogAction>>(s.Key, Convert(s)));
            }

            return target;
        }

        public static List<BONDAPI.InMemoryCachedDialogAction> Convert(IEnumerable<CachedItem<CAPI.DialogAction>> source)
        {
            if (source == null) return null;
            List<BONDAPI.InMemoryCachedDialogAction> target = new List<BONDAPI.InMemoryCachedDialogAction>();
            foreach (CachedItem<CAPI.DialogAction> s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CachedItem<CAPI.DialogAction> Convert(BONDAPI.InMemoryCachedDialogAction source)
        {
            CAPI.DialogAction convertedAction = Convert(source.Value);
            TimeSpan? convertedLifespan = null;
            if (source.LifeTimeSeconds.HasValue)
            {
                convertedLifespan = TimeSpan.FromSeconds(source.LifeTimeSeconds.Value);
            }

            DateTimeOffset? convertedExpireTime = null;
            if (source.ExpireTimeUtcTicks.HasValue)
            {
                convertedExpireTime = new DateTimeOffset(source.ExpireTimeUtcTicks.Value, TimeSpan.Zero);
            }

            CachedItem<CAPI.DialogAction> convertedItem = new CachedItem<CAPI.DialogAction>(source.Key, convertedAction, convertedLifespan, convertedExpireTime);
            return convertedItem;
        }

        public static BONDAPI.InMemoryCachedDialogAction Convert(CachedItem<CAPI.DialogAction> source)
        {
            BONDAPI.InMemoryCachedDialogAction target = new BONDAPI.InMemoryCachedDialogAction();
            target.Key = source.Key;
            target.Value = Convert(source.Item);
            target.LifeTimeSeconds = null;
            if (source.LifeTime.HasValue)
            {
                target.LifeTimeSeconds = (int)source.LifeTime.Value.TotalSeconds;
            }

            target.ExpireTimeUtcTicks = null;
            if (source.ExpireTime.HasValue)
            {
                target.ExpireTimeUtcTicks = source.ExpireTime.Value.Ticks;
            }

            return target;
        }

        #endregion

        #region ConfusionNetwork

        public static CAPI.ConfusionNetwork Convert(BONDAPI.ConfusionNetwork source)
        {
            if (source == null) return null;
            CAPI.ConfusionNetwork target = new CAPI.ConfusionNetwork();
            target.Arcs = Convert(source.Arcs);
            target.BestArcsIndexes = source.BestArcsIndexes;
            target.Nodes = Convert(source.Nodes);
            target.WordTable = source.WordTable;
            return target;
        }

        public static BONDAPI.ConfusionNetwork Convert(CAPI.ConfusionNetwork source)
        {
            if (source == null) return null;
            BONDAPI.ConfusionNetwork target = new BONDAPI.ConfusionNetwork();
            target.Arcs = Convert(source.Arcs);
            target.BestArcsIndexes = source.BestArcsIndexes;
            target.Nodes = Convert(source.Nodes);
            target.WordTable = source.WordTable;
            return target;
        }

        #endregion

        #region ConfusionNetworkArc

        public static List<CAPI.ConfusionNetworkArc> Convert(List<BONDAPI.ConfusionNetworkArc> source)
        {
            if (source == null) return null;
            List<CAPI.ConfusionNetworkArc> target = new List<CAPI.ConfusionNetworkArc>();
            foreach (BONDAPI.ConfusionNetworkArc s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.ConfusionNetworkArc> Convert(List<CAPI.ConfusionNetworkArc> source)
        {
            if (source == null) return null;
            List<BONDAPI.ConfusionNetworkArc> target = new List<BONDAPI.ConfusionNetworkArc>();
            foreach (CAPI.ConfusionNetworkArc s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.ConfusionNetworkArc Convert(BONDAPI.ConfusionNetworkArc source)
        {
            if (source == null) return null;
            CAPI.ConfusionNetworkArc target = new CAPI.ConfusionNetworkArc();
            target.IsLastArc = source.IsLastArc;
            target.NextNodeIndex = source.NextNodeIndex;
            target.PreviousNodeIndex = source.PreviousNodeIndex;
            target.Score = source.Score;
            target.WordStartIndex = source.WordStartIndex;
            return target;
        }

        public static BONDAPI.ConfusionNetworkArc Convert(CAPI.ConfusionNetworkArc source)
        {
            if (source == null) return null;
            BONDAPI.ConfusionNetworkArc target = new BONDAPI.ConfusionNetworkArc();
            target.IsLastArc = source.IsLastArc;
            target.NextNodeIndex = source.NextNodeIndex;
            target.PreviousNodeIndex = source.PreviousNodeIndex;
            target.Score = source.Score;
            target.WordStartIndex = source.WordStartIndex;
            return target;
        }

        #endregion

        #region ConfusionNetworkNode

        public static List<CAPI.ConfusionNetworkNode> Convert(List<BONDAPI.ConfusionNetworkNode> source)
        {
            if (source == null) return null;
            List<CAPI.ConfusionNetworkNode> target = new List<CAPI.ConfusionNetworkNode>();
            foreach (BONDAPI.ConfusionNetworkNode s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.ConfusionNetworkNode> Convert(List<CAPI.ConfusionNetworkNode> source)
        {
            if (source == null) return null;
            List<BONDAPI.ConfusionNetworkNode> target = new List<BONDAPI.ConfusionNetworkNode>();
            foreach (CAPI.ConfusionNetworkNode s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.ConfusionNetworkNode Convert(BONDAPI.ConfusionNetworkNode source)
        {
            if (source == null) return null;
            CAPI.ConfusionNetworkNode target = new CAPI.ConfusionNetworkNode();
            target.AudioTimeOffset = source.AudioTimeOffset;
            target.FirstFollowingArc = source.FirstFollowingArc;
            return target;
        }

        public static BONDAPI.ConfusionNetworkNode Convert(CAPI.ConfusionNetworkNode source)
        {
            if (source == null) return null;
            BONDAPI.ConfusionNetworkNode target = new BONDAPI.ConfusionNetworkNode();
            target.AudioTimeOffset = source.AudioTimeOffset;
            target.FirstFollowingArc = source.FirstFollowingArc;
            return target;
        }

        #endregion

        #region ContextualEntity

        public static List<CAPI.ContextualEntity> Convert(List<BONDAPI.ContextualEntity> source, Durandal.Common.Ontology.KnowledgeContext entityContext)
        {
            if (source == null) return null;
            List<CAPI.ContextualEntity> target = new List<CAPI.ContextualEntity>();
            foreach (BONDAPI.ContextualEntity s in source)
            {
                target.Add(Convert(s, entityContext));
            }

            return target;
        }

        public static List<BONDAPI.ContextualEntity> Convert(IList<CAPI.ContextualEntity> source)
        {
            if (source == null) return null;
            List<BONDAPI.ContextualEntity> target = new List<BONDAPI.ContextualEntity>();
            foreach (CAPI.ContextualEntity s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.ContextualEntity Convert(BONDAPI.ContextualEntity source, Durandal.Common.Ontology.KnowledgeContext entityContext)
        {
            if (source == null) return null;

            Common.Ontology.Entity entity = entityContext.GetEntityInMemory(source.EntityId);
            CAPI.ContextualEntitySource entitySource = Convert(source.Source);
            return new CAPI.ContextualEntity(entity, entitySource, source.Relevance);
        }

        public static BONDAPI.ContextualEntity Convert(CAPI.ContextualEntity source)
        {
            if (source == null) return null;
            BONDAPI.ContextualEntity target = new BONDAPI.ContextualEntity();
            target.EntityId = source.Entity.EntityId;
            target.Relevance = source.Relevance;
            target.Source = Convert(source.Source);
            return target;
        }

        #endregion

        #region ContextualEntitySource

        public static CAPI.ContextualEntitySource Convert(BONDAPI.ContextualEntitySource source)
        {
            return (CAPI.ContextualEntitySource)source;
        }

        public static BONDAPI.ContextualEntitySource Convert(CAPI.ContextualEntitySource source)
        {
            return (BONDAPI.ContextualEntitySource)source;
        }

        #endregion

        #region CrossDomainContext

        public static CAPI.CrossDomainContext Convert(BONDAPI.CrossDomainContext source)
        {
            if (source == null) return null;
            CAPI.CrossDomainContext target = new CAPI.CrossDomainContext();
            target.RequestedSlots = Convert(source.RequestedSlots);
            target.PastConversationTurns = Convert(source.PastConversationTurns);
            target.RequestDomain = source.RequestDomain;
            target.RequestIntent = source.RequestIntent;
            return target;
        }

        public static BONDAPI.CrossDomainContext Convert(CAPI.CrossDomainContext source)
        {
            if (source == null) return null;
            BONDAPI.CrossDomainContext target = new BONDAPI.CrossDomainContext();
            target.RequestedSlots = Convert(source.RequestedSlots);
            target.PastConversationTurns = Convert(source.PastConversationTurns);
            target.RequestDomain = source.RequestDomain;
            target.RequestIntent = source.RequestIntent;
            return target;
        }

        #endregion

        #region CrossDomainResponseData

        public static CAPI.CrossDomainResponseData Convert(BONDAPI.CrossDomainResponseData source)
        {
            if (source == null) return null;
            CAPI.CrossDomainResponseData target = new CAPI.CrossDomainResponseData();
            target.FilledSlots = Convert(source.FilledSlots);
            target.CallbackMultiturnBehavior = Convert(source.CallbackMultiturnBehavior);
            return target;
        }

        public static BONDAPI.CrossDomainResponseData Convert(CAPI.CrossDomainResponseData source)
        {
            if (source == null) return null;
            BONDAPI.CrossDomainResponseData target = new BONDAPI.CrossDomainResponseData();
            target.FilledSlots = Convert(source.FilledSlots);
            target.CallbackMultiturnBehavior = Convert(source.CallbackMultiturnBehavior);
            return target;
        }

        #endregion

        #region CrossDomainResponseResponse

        public static CAPI.CrossDomainResponseResponse Convert(BONDAPI.CrossDomainResponseResponse source)
        {
            if (source == null) return null;
            CAPI.CrossDomainResponseResponse target = new CAPI.CrossDomainResponseResponse();
            target.PluginResponse = Convert(source.PluginResponse);
            target.OutEntityContext = ConvertKnowledgeContext(source.OutEntityContext);
            return target;
        }

        public static BONDAPI.CrossDomainResponseResponse Convert(CAPI.CrossDomainResponseResponse source)
        {
            if (source == null) return null;
            BONDAPI.CrossDomainResponseResponse target = new BONDAPI.CrossDomainResponseResponse();
            target.PluginResponse = Convert(source.PluginResponse);
            target.OutEntityContext = ConvertKnowledgeContext(source.OutEntityContext);
            return target;
        }

        #endregion

        #region CrossDomainRequestData

        public static CAPI.CrossDomainRequestData Convert(BONDAPI.CrossDomainRequestData source)
        {
            if (source == null) return null;
            CAPI.CrossDomainRequestData target = new CAPI.CrossDomainRequestData();
            target.RequestedSlots = Convert(source.RequestedSlots);
            return target;
        }

        public static BONDAPI.CrossDomainRequestData Convert(CAPI.CrossDomainRequestData source)
        {
            if (source == null) return null;
            BONDAPI.CrossDomainRequestData target = new BONDAPI.CrossDomainRequestData();
            target.RequestedSlots = Convert(source.RequestedSlots);
            return target;
        }

        #endregion

        #region CrossDomainSlot

        public static HashSet<CAPI.CrossDomainSlot> Convert(List<BONDAPI.CrossDomainSlot> source)
        {
            if (source == null) return null;
            HashSet<CAPI.CrossDomainSlot> target = new HashSet<CAPI.CrossDomainSlot>();
            foreach (BONDAPI.CrossDomainSlot s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.CrossDomainSlot> Convert(IEnumerable<CAPI.CrossDomainSlot> source)
        {
            if (source == null) return null;
            List<BONDAPI.CrossDomainSlot> target = new List<BONDAPI.CrossDomainSlot>();
            foreach (CAPI.CrossDomainSlot s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.CrossDomainSlot Convert(BONDAPI.CrossDomainSlot source)
        {
            if (source == null) return null;
            CAPI.CrossDomainSlot target = new CAPI.CrossDomainSlot(source.SlotName, source.IsRequired, source.AcceptedSchemas)
            {
                Documentation = source.Documentation
            };
            return target;
        }

        public static BONDAPI.CrossDomainSlot Convert(CAPI.CrossDomainSlot source)
        {
            if (source == null) return null;
            BONDAPI.CrossDomainSlot target = new BONDAPI.CrossDomainSlot();
            target.AcceptedSchemas = source.AcceptedSchemas == null ? new List<string>() : source.AcceptedSchemas.ToList();
            target.Documentation = source.Documentation;
            target.IsRequired = source.IsRequired;
            target.SlotName = source.SlotName;
            return target;
        }

        #endregion

        #region DialogAction

        public static CAPI.DialogAction Convert(BONDAPI.DialogAction source)
        {
            if (source == null) return null;
            CAPI.DialogAction target = new CAPI.DialogAction();
            target.Domain = source.Domain;
            target.Intent = source.Intent;
            target.InteractionMethod = Convert(source.InteractionMethod);
            target.Slots = Convert(source.Slots);
            return target;
        }

        public static BONDAPI.DialogAction Convert(CAPI.DialogAction source)
        {
            if (source == null) return null;
            BONDAPI.DialogAction target = new BONDAPI.DialogAction();
            target.Domain = source.Domain ?? string.Empty;
            target.Intent = source.Intent ?? string.Empty;
            target.InteractionMethod = Convert(source.InteractionMethod);
            target.Slots = Convert(source.Slots) ?? new List<BONDAPI.SlotValue>();
            return target;
        }

        #endregion

        #region DialogProcessingResponse

        public static CAPI.DialogProcessingResponse Convert(BONDAPI.DialogProcessingResponse source)
        {
            if (source == null) return null;
            CAPI.PluginResult convertedPluginOutput = Convert(source.PluginOutput);
            CAPI.DialogProcessingResponse target = new CAPI.DialogProcessingResponse(convertedPluginOutput, source.WasRetrying);
            target.UpdatedDialogActions = Convert(source.UpdatedDialogActions);
            target.UpdatedWebDataCache = Convert(source.UpdatedWebDataCache);
            target.UpdatedEntityContext = ConvertKnowledgeContext(source.UpdatedEntityContext);
            target.UpdatedEntityHistory = ConvertEntityHistory(source.UpdatedEntityHistory);
            target.UpdatedGlobalUserProfile = Convert(source.UpdatedGlobalProfile);
            target.UpdatedLocalUserProfile = Convert(source.UpdatedLocalProfile);
            target.UpdatedSessionStore = Convert(source.UpdatedSessionStore);
            return target;
        }

        public static BONDAPI.DialogProcessingResponse Convert(CAPI.DialogProcessingResponse source)
        {
            if (source == null) return null;
            BONDAPI.DialogProcessingResponse target = new BONDAPI.DialogProcessingResponse();
            target.PluginOutput = Convert(source.PluginOutput);
            target.WasRetrying = source.WasRetrying;
            target.UpdatedDialogActions = Convert(source.UpdatedDialogActions);
            target.UpdatedWebDataCache = Convert(source.UpdatedWebDataCache);
            target.UpdatedEntityContext = ConvertKnowledgeContext(source.UpdatedEntityContext);
            target.UpdatedEntityHistory = ConvertEntityHistory(source.UpdatedEntityHistory);
            target.UpdatedGlobalProfile = Convert(source.UpdatedGlobalUserProfile);
            target.UpdatedLocalProfile = Convert(source.UpdatedLocalUserProfile);
            target.UpdatedSessionStore = Convert(source.UpdatedSessionStore);
            return target;
        }

        #endregion

        #region DialogRequest

        public static CAPI.DialogRequest Convert(BONDAPI.DialogRequest source)
        {
            if (source == null) return null;
            CAPI.DialogRequest target = new CAPI.DialogRequest();
            target.ProtocolVersion = source.ProtocolVersion;
            target.ClientContext = Convert(source.ClientContext);
            target.InteractionType = Convert(source.InteractionType);
            target.TextInput = source.TextInput;
            target.SpeechInput = Convert(source.SpeechInput);
            target.AuthTokens = Convert(source.AuthTokens);
            target.AudioInput = Convert(source.AudioInput);
            target.LanguageUnderstanding = Convert(source.LanguageUnderstanding);
            target.PreferredAudioCodec = source.PreferredAudioCodec;
            target.TraceId = source.TraceId;
            target.DomainScope = source.DomainScope;
            target.ClientAudioPlaybackTimeMs = source.ClientAudioPlaybackTimeMs;
            target.RequestFlags = (CAPI.QueryFlags)source.RequestFlags;
            target.EntityContext = source.EntityContext;
            target.EntityInput = Convert(source.EntityInput);
            target.RequestData = source.RequestData;
            target.PreferredAudioFormat = source.PreferredAudioFormat;
            return target;
        }

        public static BONDAPI.DialogRequest Convert(CAPI.DialogRequest source)
        {
            if (source == null) return null;
            BONDAPI.DialogRequest target = new BONDAPI.DialogRequest();
            target.ProtocolVersion = source.ProtocolVersion;
            target.ClientContext = Convert(source.ClientContext) ?? new BONDAPI.ClientContext();
            target.InteractionType = Convert(source.InteractionType);
            target.TextInput = source.TextInput;
            target.SpeechInput = Convert(source.SpeechInput);
            target.AuthTokens = Convert(source.AuthTokens);
            target.AudioInput = Convert(source.AudioInput);
            target.LanguageUnderstanding = Convert(source.LanguageUnderstanding);
            target.PreferredAudioCodec = source.PreferredAudioCodec;
            target.TraceId = source.TraceId;
            target.DomainScope = source.DomainScope;
            target.ClientAudioPlaybackTimeMs = source.ClientAudioPlaybackTimeMs;
            target.RequestFlags = (uint)source.RequestFlags;
            target.EntityContext = source.EntityContext;
            target.EntityInput = Convert(source.EntityInput);

            // FIXME it would be nice if Bond used abstract dictionaries
            if (source.RequestData == null || source.RequestData is Dictionary<string, string>)
            {
                target.RequestData = source.RequestData as Dictionary<string, string>;
            }
            else
            {
                target.RequestData = new Dictionary<string, string>(source.RequestData);
            }

            target.PreferredAudioFormat = source.PreferredAudioFormat;
            return target;
        }

        #endregion

        #region DialogResponse

        public static CAPI.DialogResponse Convert(BONDAPI.DialogResponse source)
        {
            if (source == null) return null;
            CAPI.DialogResponse target = new CAPI.DialogResponse();
            target.AugmentedFinalQuery = source.AugmentedFinalQuery;
            target.ContinueImmediately = source.ContinueImmediately;
            target.ConversationLifetimeSeconds = source.ConversationLifetimeSeconds;
            target.CustomAudioOrdering = Convert(source.CustomAudioOrdering);
            target.ErrorMessage = source.ErrorMessage;
            target.ExecutionResult = Convert(source.ExecutionResult);
            target.IsRetrying = source.IsRetrying;
            target.ProtocolVersion = source.ProtocolVersion;
            target.ResponseAction = source.ResponseAction;
            target.ResponseAudio = Convert(source.ResponseAudio);
            target.ResponseData = source.ResponseData;
            target.ResponseHtml = source.ResponseHtml;
            target.ResponseSsml = source.ResponseSsml;
            target.ResponseText = source.ResponseText;
            target.ResponseUrl = source.ResponseUrl;
            target.SelectedRecoResult = Convert(source.SelectedRecoResult);
            target.StreamingAudioUrl = source.StreamingAudioUrl;
            target.SuggestedQueries = source.SuggestedQueries;
            target.SuggestedRetryDelay = source.SuggestedRetryDelay;
            target.TraceId = source.TraceId;
            target.TraceInfo = Convert(source.TraceInfo);
            target.TriggerKeywords = Convert(source.TriggerKeywords);
            target.UrlScope = Convert(source.UrlScope);
            target.ExecutedPlugin = Convert(source.ExecutedPlugin);
            return target;
        }

        public static BONDAPI.DialogResponse Convert(CAPI.DialogResponse source)
        {
            if (source == null) return null;
            BONDAPI.DialogResponse target = new BONDAPI.DialogResponse();
            target.AugmentedFinalQuery = source.AugmentedFinalQuery;
            target.ContinueImmediately = source.ContinueImmediately;
            target.ConversationLifetimeSeconds = source.ConversationLifetimeSeconds;
            target.CustomAudioOrdering = Convert(source.CustomAudioOrdering);
            target.ErrorMessage = source.ErrorMessage;
            target.ExecutionResult = Convert(source.ExecutionResult);
            target.IsRetrying = source.IsRetrying;
            target.ProtocolVersion = source.ProtocolVersion;
            target.ResponseAction = source.ResponseAction;
            target.ResponseAudio = Convert(source.ResponseAudio);
            target.ResponseData = source.ResponseData;
            target.ResponseHtml = source.ResponseHtml;
            target.ResponseSsml = source.ResponseSsml;
            target.ResponseText = source.ResponseText;
            target.ResponseUrl = source.ResponseUrl;
            target.SelectedRecoResult = Convert(source.SelectedRecoResult);
            target.StreamingAudioUrl = source.StreamingAudioUrl;
            target.SuggestedQueries = source.SuggestedQueries;
            target.SuggestedRetryDelay = source.SuggestedRetryDelay;
            target.TraceId = source.TraceId;
            target.TraceInfo = Convert(source.TraceInfo);
            target.TriggerKeywords = Convert(source.TriggerKeywords);
            target.UrlScope = Convert(source.UrlScope);
            target.ExecutedPlugin = Convert(source.ExecutedPlugin);
            return target;
        }

        #endregion

        #region DomainScope

        public static CAPI.DomainScope Convert(BONDAPI.DomainScope source)
        {
            return (CAPI.DomainScope)source;
        }

        public static BONDAPI.DomainScope Convert(CAPI.DomainScope source)
        {
            return (BONDAPI.DomainScope)source;
        }

        #endregion

        #region EntityHistory

        public static InMemoryEntityHistory ConvertEntityHistory(ArraySegment<byte> source)
        {
            if (source != null && source.Array != null)
            {
                if (source.Count == 0)
                {
                    return new InMemoryEntityHistory();
                }
                else
                {
                    using (MemoryStream stream = new MemoryStream(source.Array, source.Offset, source.Count))
                    {
                        return InMemoryEntityHistory.Deserialize(stream, true);
                    }
                }
            }
            else
            {
                return null;
            }
        }

        public static ArraySegment<byte> ConvertEntityHistory(InMemoryEntityHistory source)
        {
            if (source == null)
            {
                return new ArraySegment<byte>(null);
            }
            else
            {
                using (PooledBuffer<byte> serializedHistory = source.Serialize())
                {
                    if (serializedHistory.Length == 0)
                    {
                        return new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
                    }
                    else
                    {
                        byte[] copiedData = new byte[serializedHistory.Length];
                        ArrayExtensions.MemCopy(serializedHistory.Buffer, 0, copiedData, 0, copiedData.Length);
                        return new ArraySegment<byte>(copiedData);
                    }
                }
            }
        }

        #endregion

        #region EntityReference

        public static List<CAPI.EntityReference> Convert(List<BONDAPI.EntityReference> source)
        {
            if (source == null) return null;
            List<CAPI.EntityReference> target = new List<CAPI.EntityReference>();
            foreach (BONDAPI.EntityReference s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.EntityReference> Convert(List<CAPI.EntityReference> source)
        {
            if (source == null) return null;
            List<BONDAPI.EntityReference> target = new List<BONDAPI.EntityReference>();
            foreach (CAPI.EntityReference s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.EntityReference Convert(BONDAPI.EntityReference source)
        {
            if (source == null) return null;
            CAPI.EntityReference target = new CAPI.EntityReference();
            target.EntityId = source.EntityId;
            target.Relevance = source.Relevance;
            return target;
        }

        public static BONDAPI.EntityReference Convert(CAPI.EntityReference source)
        {
            if (source == null) return null;
            BONDAPI.EntityReference target = new BONDAPI.EntityReference();
            target.EntityId = source.EntityId ?? string.Empty;
            target.Relevance = source.Relevance;
            return target;
        }

        #endregion

        #region HypothesisInt

        public static List<Durandal.Common.Statistics.Hypothesis<int>> Convert(List<BONDAPI.HypothesisInt> source)
        {
            if (source == null) return null;
            List<Durandal.Common.Statistics.Hypothesis<int>> target = new List<Durandal.Common.Statistics.Hypothesis<int>>();
            foreach (BONDAPI.HypothesisInt s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.HypothesisInt> Convert(List<Durandal.Common.Statistics.Hypothesis<int>> source)
        {
            if (source == null) return null;
            List<BONDAPI.HypothesisInt> target = new List<BONDAPI.HypothesisInt>();
            foreach (Durandal.Common.Statistics.Hypothesis<int> s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static Durandal.Common.Statistics.Hypothesis<int> Convert(BONDAPI.HypothesisInt source)
        {
            Durandal.Common.Statistics.Hypothesis<int> target = new Durandal.Common.Statistics.Hypothesis<int>(source.Value, source.Conf);
            return target;
        }

        public static BONDAPI.HypothesisInt Convert(Durandal.Common.Statistics.Hypothesis<int> source)
        {
            if (source == null) return null;
            BONDAPI.HypothesisInt target = new BONDAPI.HypothesisInt();
            target.Value = source.Value;
            target.Conf = source.Conf;
            return target;
        }

        #endregion

        #region InputMethod

        public static CAPI.InputMethod Convert(BONDAPI.InputMethod source)
        {
            return (CAPI.InputMethod)source;
        }

        public static BONDAPI.InputMethod Convert(CAPI.InputMethod source)
        {
            return (BONDAPI.InputMethod)source;
        }

        #endregion

        #region InMemoryDataStoreItem

        public static List<KeyValuePair<string, byte[]>> Convert(List<BONDAPI.InMemoryDataStoreItem> source)
        {
            if (source == null) return null;
            List<KeyValuePair<string, byte[]>> target = new List<KeyValuePair<string, byte[]>>();
            foreach (BONDAPI.InMemoryDataStoreItem s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.InMemoryDataStoreItem> Convert(IEnumerable<KeyValuePair<string, byte[]>> source)
        {
            if (source == null) return null;
            List<BONDAPI.InMemoryDataStoreItem> target = new List<BONDAPI.InMemoryDataStoreItem>();
            foreach (KeyValuePair<string, byte[]> s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static KeyValuePair<string, byte[]> Convert(BONDAPI.InMemoryDataStoreItem source)
        {
            byte[] trimmedArray = new byte[source.Value.Count];
            ArrayExtensions.MemCopy(source.Value.Array, source.Value.Offset, trimmedArray, 0, source.Value.Count);
            return new KeyValuePair<string, byte[]>(source.Key, trimmedArray);
        }

        public static BONDAPI.InMemoryDataStoreItem Convert(KeyValuePair<string, byte[]> source)
        {
            BONDAPI.InMemoryDataStoreItem target = new BONDAPI.InMemoryDataStoreItem();
            target.Key = source.Key;
            target.Value = new ArraySegment<byte>(source.Value);
            return target;
        }

        #endregion

        #region InMemoryDataStore

        public static Durandal.Common.Dialog.Services.InMemoryDataStore Convert(BONDAPI.InMemoryDataStore source)
        {
            if (source == null) return null;
            IDictionary<string, byte[]> values = new Dictionary<string, byte[]>();
            foreach (var kvp in Convert(source.Items))
            {
                values.Add(kvp);
            }

            return new Durandal.Common.Dialog.Services.InMemoryDataStore(values);
        }

        public static BONDAPI.InMemoryDataStore Convert(Durandal.Common.Dialog.Services.InMemoryDataStore source)
        {
            if (source == null) return null;
            BONDAPI.InMemoryDataStore target = new BONDAPI.InMemoryDataStore();
            target.Items = Convert(source.GetAllObjects());
            return target;
        }

        #endregion

        #region InMemoryDialogActionCache

        public static Durandal.Common.Dialog.Services.InMemoryDialogActionCache Convert(BONDAPI.InMemoryDialogActionCache source)
        {
            if (source == null) return null;
            IDictionary<string, CachedItem<CAPI.DialogAction>> values = new Dictionary<string, CachedItem<CAPI.DialogAction>>();
            foreach (var kvp in Convert(source.Items))
            {
                values.Add(kvp);
            }

            return new Durandal.Common.Dialog.Services.InMemoryDialogActionCache(values);
        }

        public static BONDAPI.InMemoryDialogActionCache Convert(Durandal.Common.Dialog.Services.InMemoryDialogActionCache source)
        {
            if (source == null) return null;
            BONDAPI.InMemoryDialogActionCache target = new BONDAPI.InMemoryDialogActionCache();
            target.Items = Convert(source.GetAllItems());
            return target;
        }

        #endregion

        #region InMemoryWebDataCache

        public static Durandal.Common.Dialog.Services.InMemoryWebDataCache Convert(BONDAPI.InMemoryWebDataCache source)
        {
            if (source == null) return null;
            IDictionary<string, CachedItem<CAPI.CachedWebData>> values = new Dictionary<string, CachedItem<CAPI.CachedWebData>>();
            foreach (var kvp in Convert(source.Items))
            {
                values.Add(kvp);
            }

            return new Durandal.Common.Dialog.Services.InMemoryWebDataCache(values);
        }

        public static BONDAPI.InMemoryWebDataCache Convert(Durandal.Common.Dialog.Services.InMemoryWebDataCache source)
        {
            if (source == null) return null;
            BONDAPI.InMemoryWebDataCache target = new BONDAPI.InMemoryWebDataCache();
            target.Items = Convert(source.GetAllItems());
            return target;
        }

        #endregion

        #region InstrumentationEvent

        public static List<CAPI.InstrumentationEvent> Convert(List<BONDAPI.InstrumentationEvent> source)
        {
            if (source == null) return null;
            List<CAPI.InstrumentationEvent> target = new List<CAPI.InstrumentationEvent>();
            foreach (BONDAPI.InstrumentationEvent s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.InstrumentationEvent> Convert(List<CAPI.InstrumentationEvent> source)
        {
            if (source == null) return null;
            List<BONDAPI.InstrumentationEvent> target = new List<BONDAPI.InstrumentationEvent>();
            foreach (CAPI.InstrumentationEvent s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.InstrumentationEvent Convert(BONDAPI.InstrumentationEvent source)
        {
            CAPI.InstrumentationEvent target = new CAPI.InstrumentationEvent();
            target.Component = source.Component;
            target.Level = source.Level;
            target.Message = source.Message;
            target.Timestamp = source.Timestamp;
            target.TraceId = source.TraceId;
            target.PrivacyClassification = source.PrivacyClassification;
            return target;
        }

        public static BONDAPI.InstrumentationEvent Convert(CAPI.InstrumentationEvent source)
        {
            BONDAPI.InstrumentationEvent target = new BONDAPI.InstrumentationEvent();
            target.Component = source.Component ?? string.Empty;
            target.Level = source.Level;
            target.Message = source.Message ?? string.Empty;
            target.Timestamp = source.Timestamp;
            target.TraceId = source.TraceId ?? string.Empty;
            target.PrivacyClassification = source.PrivacyClassification;
            return target;
        }

        #endregion

        #region InstrumentationEventList

        public static CAPI.InstrumentationEventList Convert(BONDAPI.InstrumentationEventList source)
        {
            if (source == null) return null;
            CAPI.InstrumentationEventList target = new CAPI.InstrumentationEventList();
            target.Events = Convert(source.Events);
            return target;
        }

        public static BONDAPI.InstrumentationEventList Convert(CAPI.InstrumentationEventList source)
        {
            if (source == null) return null;
            BONDAPI.InstrumentationEventList target = new BONDAPI.InstrumentationEventList();
            target.Events = Convert(source.Events) ?? new List<BONDAPI.InstrumentationEvent>();
            return target;
        }

        #endregion

        #region KnowledgeContext

        public static KnowledgeContext ConvertKnowledgeContext(ArraySegment<byte> source)
        {
            if (source != null && source.Array != null)
            {
                if (source.Count == 0)
                {
                    return new KnowledgeContext();
                }
                else
                {
                    using (MemoryStream stream = new MemoryStream(source.Array, source.Offset, source.Count))
                    {
                        return KnowledgeContext.Deserialize(stream, true);
                    }
                }
            }
            else
            {
                return null;
            }
        }

        public static ArraySegment<byte> ConvertKnowledgeContext(KnowledgeContext source)
        {
            if (source == null)
            {
                return new ArraySegment<byte>(null);
            }
            else if (source.IsEmpty)
            {
                return new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
            }
            else
            {
                using (PooledBuffer<byte> serialized = source.Serialize())
                {
                    // This is a bit wasteful, but hopefully it should save allocations on average for empty knowledge contexts.
                    byte[] copiedData = new byte[serialized.Length];
                    ArrayExtensions.MemCopy(serialized.Buffer, 0, copiedData, 0, copiedData.Length);
                    return new ArraySegment<byte>(copiedData);
                }
            }
        }

        #endregion

        #region LexicalNamedEntity

        public static List<CAPI.LexicalNamedEntity> Convert(List<BONDAPI.LexicalNamedEntity> source)
        {
            if (source == null) return null;
            List<CAPI.LexicalNamedEntity> target = new List<CAPI.LexicalNamedEntity>();
            foreach (BONDAPI.LexicalNamedEntity s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.LexicalNamedEntity> Convert(List<CAPI.LexicalNamedEntity> source)
        {
            if (source == null) return null;
            List<BONDAPI.LexicalNamedEntity> target = new List<BONDAPI.LexicalNamedEntity>();
            foreach (CAPI.LexicalNamedEntity s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.LexicalNamedEntity Convert(BONDAPI.LexicalNamedEntity source)
        {
            if (source == null) return null;
            CAPI.LexicalNamedEntity target = new CAPI.LexicalNamedEntity(source.Ordinal, Convert(source.KnownAs));
            return target;
        }

        public static BONDAPI.LexicalNamedEntity Convert(CAPI.LexicalNamedEntity source)
        {
            if (source == null) return null;
            BONDAPI.LexicalNamedEntity target = new BONDAPI.LexicalNamedEntity();
            target.Ordinal = source.Ordinal;
            target.KnownAs = Convert(source.KnownAs);
            return target;
        }

        #endregion

        #region LexicalString

        public static List<CAPI.LexicalString> Convert(List<BONDAPI.LexicalString> source)
        {
            if (source == null) return null;
            List<CAPI.LexicalString> target = new List<CAPI.LexicalString>();
            foreach (BONDAPI.LexicalString s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.LexicalString> Convert(List<CAPI.LexicalString> source)
        {
            if (source == null) return null;
            List<BONDAPI.LexicalString> target = new List<BONDAPI.LexicalString>();
            foreach (CAPI.LexicalString s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.LexicalString Convert(BONDAPI.LexicalString source)
        {
            if (source == null) return null;
            CAPI.LexicalString target = new CAPI.LexicalString(source.WrittenForm, source.SpokenForm);
            return target;
        }

        public static BONDAPI.LexicalString Convert(CAPI.LexicalString source)
        {
            if (source == null) return null;
            BONDAPI.LexicalString target = new BONDAPI.LexicalString();
            target.WrittenForm = source.WrittenForm;
            target.SpokenForm = source.SpokenForm;
            return target;
        }

        #endregion

        #region LoadedPluginInformation

        public static CAPI.LoadedPluginInformation Convert(BONDAPI.LoadedPluginInformation source)
        {
            if (source == null) return null;
            CAPI.LoadedPluginInformation target = new CAPI.LoadedPluginInformation();
            target.SerializedConversationTree = Convert(source.SerializedConversationTree);
            target.LUDomain = source.LUDomain;
            target.PluginId = source.PluginId;
            target.PluginInfo = Convert(source.PluginInfo);
            target.PluginStrongName = Convert(source.PluginStrongName);
            return target;
        }

        public static BONDAPI.LoadedPluginInformation Convert(CAPI.LoadedPluginInformation source)
        {
            if (source == null) return null;
            BONDAPI.LoadedPluginInformation target = new BONDAPI.LoadedPluginInformation();
            target.SerializedConversationTree = Convert(source.SerializedConversationTree);
            target.LUDomain = source.LUDomain;
            target.PluginId = source.PluginId;
            target.PluginInfo = Convert(source.PluginInfo);
            target.PluginStrongName = Convert(source.PluginStrongName);
            return target;
        }

        #endregion

        #region LocalizedInformation

        public static CAPI.LocalizedInformation Convert(BONDAPI.LocalizedInformation source)
        {
            if (source == null) return null;
            CAPI.LocalizedInformation target = new CAPI.LocalizedInformation();
            target.Creator = source.Creator;
            target.DisplayName = source.DisplayName;
            target.SampleQueries = source.SampleQueries;
            target.ShortDescription = source.ShortDescription;
            return target;
        }

        public static BONDAPI.LocalizedInformation Convert(CAPI.LocalizedInformation source)
        {
            if (source == null) return null;
            BONDAPI.LocalizedInformation target = new BONDAPI.LocalizedInformation();
            target.Creator = source.Creator;
            target.DisplayName = source.DisplayName;
            target.SampleQueries = source.SampleQueries;
            target.ShortDescription = source.ShortDescription;
            return target;
        }

        #endregion

        #region LURequest

        public static CAPI.LURequest Convert(BONDAPI.LURequest source)
        {
            if (source == null) return null;
            CAPI.LURequest target = new CAPI.LURequest();
            target.Context = Convert(source.Context);
            target.DoFullAnnotation = source.DoFullAnnotation;
            target.DomainScope = source.DomainScope;
            target.ContextualDomains = source.ContextualDomains;
            target.TextInput = source.TextInput;
            target.SpeechInput = Convert(source.SpeechInput);
            target.ProtocolVersion = source.ProtocolVersion;
            target.RequestFlags = (CAPI.QueryFlags)source.RequestFlags;
            target.TraceId = source.TraceId;
            return target;
        }

        public static BONDAPI.LURequest Convert(CAPI.LURequest source)
        {
            if (source == null) return null;
            BONDAPI.LURequest target = new BONDAPI.LURequest();
            target.Context = Convert(source.Context) ?? new BONDAPI.ClientContext();
            target.DoFullAnnotation = source.DoFullAnnotation;
            target.DomainScope = source.DomainScope;
            target.ContextualDomains = source.ContextualDomains;
            target.TextInput = source.TextInput;
            target.SpeechInput = Convert(source.SpeechInput);
            target.ProtocolVersion = source.ProtocolVersion;
            target.RequestFlags = (uint)source.RequestFlags;
            target.TraceId = source.TraceId;
            return target;
        }

        #endregion

        #region LUResponse

        public static CAPI.LUResponse Convert(BONDAPI.LUResponse source)
        {
            if (source == null) return null;
            CAPI.LUResponse target = new CAPI.LUResponse();
            target.ProtocolVersion = source.ProtocolVersion;
            target.Results = Convert(source.Results);
            target.TraceId = source.TraceId;
            target.TraceInfo = Convert(source.TraceInfo);
            return target;
        }

        public static BONDAPI.LUResponse Convert(CAPI.LUResponse source)
        {
            if (source == null) return null;
            BONDAPI.LUResponse target = new BONDAPI.LUResponse();
            target.ProtocolVersion = source.ProtocolVersion;
            target.Results = Convert(source.Results) ?? new List<BONDAPI.RecognizedPhrase>();
            target.TraceId = source.TraceId;
            target.TraceInfo = Convert(source.TraceInfo);
            return target;
        }

        #endregion

        #region OAuthConfig

        public static CAPI.OAuthConfig Convert(BONDAPI.OAuthConfig source)
        {
            if (source == null) return null;
            CAPI.OAuthConfig target = new CAPI.OAuthConfig();
            target.AuthorizationHeader = source.AuthorizationHeader;
            target.AuthUri = source.AuthUri;
            target.ClientId = source.ClientId;
            target.ClientSecret = source.ClientSecret;
            target.ConfigName = source.ConfigName;
            target.Scope = source.Scope;
            target.TokenUri = source.TokenUri;
            target.Type = Convert(source.Type);
            target.UsePKCE = source.UsePKCE;
            return target;
        }

        public static BONDAPI.OAuthConfig Convert(CAPI.OAuthConfig source)
        {
            if (source == null) return null;
            BONDAPI.OAuthConfig target = new BONDAPI.OAuthConfig();
            target.AuthorizationHeader = source.AuthorizationHeader;
            target.AuthUri = source.AuthUri;
            target.ClientId = source.ClientId;
            target.ClientSecret = source.ClientSecret;
            target.ConfigName = source.ConfigName;
            target.Scope = source.Scope;
            target.TokenUri = source.TokenUri;
            target.Type = Convert(source.Type);
            target.UsePKCE = source.UsePKCE;
            return target;
        }

        #endregion

        #region OAuthFlavor

        public static CAPI.OAuthFlavor Convert(BONDAPI.OAuthFlavor source)
        {
            return (CAPI.OAuthFlavor)source;
        }

        public static BONDAPI.OAuthFlavor Convert(CAPI.OAuthFlavor source)
        {
            return (BONDAPI.OAuthFlavor)source;
        }

        #endregion

        #region OAuthToken

        public static CAPI.OAuthToken Convert(BONDAPI.OAuthToken source)
        {
            if (source == null) return null;
            CAPI.OAuthToken target = new CAPI.OAuthToken();
            target.ExpiresAt = new DateTimeOffset(source.ExpiresAtUtcTicks, TimeSpan.Zero);
            target.IssuedAt = new DateTimeOffset(source.IssuedAtUtcTicks, TimeSpan.Zero);
            target.RefreshToken = source.RefreshToken;
            target.Token = source.Token;
            target.TokenType = source.TokenType;
            return target;
        }

        public static BONDAPI.OAuthToken Convert(CAPI.OAuthToken source)
        {
            if (source == null) return null;
            BONDAPI.OAuthToken target = new BONDAPI.OAuthToken();
            target.ExpiresAtUtcTicks = source.ExpiresAt.UtcTicks;
            target.IssuedAtUtcTicks = source.IssuedAt.UtcTicks;
            target.RefreshToken = source.RefreshToken;
            target.Token = source.Token;
            target.TokenType = source.TokenType;
            return target;
        }

        #endregion

        #region MultiTurnBehavior

        public static CAPI.MultiTurnBehavior Convert(BONDAPI.MultiTurnBehavior source)
        {
            if (source == null) return null;
            CAPI.MultiTurnBehavior target = new CAPI.MultiTurnBehavior();
            target.Continues = source.Continues;
            target.ConversationTimeoutSeconds = source.ConversationTimeoutSeconds;
            target.FullConversationControl = source.FullConversationControl;
            target.IsImmediate = source.IsImmediate;
            target.SuggestedPauseDelay = source.SuggestedPauseDelay;
            return target;
        }

        public static BONDAPI.MultiTurnBehavior Convert(CAPI.MultiTurnBehavior source)
        {
            if (source == null) return null;
            BONDAPI.MultiTurnBehavior target = new BONDAPI.MultiTurnBehavior();
            target.Continues = source.Continues;
            target.ConversationTimeoutSeconds = source.ConversationTimeoutSeconds;
            target.FullConversationControl = source.FullConversationControl;
            target.IsImmediate = source.IsImmediate;
            target.SuggestedPauseDelay = source.SuggestedPauseDelay;
            return target;
        }

        #endregion

        #region PluginInformation

        public static CAPI.PluginInformation Convert(BONDAPI.PluginInformation source)
        {
            if (source == null) return null;
            CAPI.PluginInformation target = new CAPI.PluginInformation();
            target.Configurable = source.Configurable;
            target.Creator = source.Creator;
            target.Hidden = source.Hidden;
            target.IconPngData = source.IconPngData;
            target.InternalName = source.InternalName;
            target.MajorVersion = source.MajorVersion;
            target.MinorVersion = source.MinorVersion;
            if (source.LocalizedInfo != null)
            {
                target.LocalizedInfo = new Dictionary<string, CAPI.LocalizedInformation>();
                foreach (var kvp in source.LocalizedInfo)
                {
                    target.LocalizedInfo[kvp.Key] = Convert(kvp.Value);
                }
            }

            return target;
        }

        public static BONDAPI.PluginInformation Convert(CAPI.PluginInformation source)
        {
            if (source == null) return null;
            BONDAPI.PluginInformation target = new BONDAPI.PluginInformation();
            target.Configurable = source.Configurable;
            target.Creator = source.Creator;
            target.Hidden = source.Hidden;
            target.IconPngData = source.IconPngData;
            target.InternalName = source.InternalName;
            target.MajorVersion = source.MajorVersion;
            target.MinorVersion = source.MinorVersion;
            if (source.LocalizedInfo != null)
            {
                target.LocalizedInfo = new Dictionary<string, BONDAPI.LocalizedInformation>();
                foreach (var kvp in source.LocalizedInfo)
                {
                    target.LocalizedInfo[kvp.Key] = Convert(kvp.Value);
                }
            }

            return target;
        }

        #endregion

        #region PluginResult

        public static CAPI.PluginResult Convert(BONDAPI.PluginResult source)
        {
            if (source == null) return null;
            CAPI.PluginResult target = new CAPI.PluginResult(Convert(source.ResponseCode));
            target.AugmentedQuery = source.AugmentedQuery;
            target.ClientAction = source.ClientAction;
            target.ContinuationFuncName = source.ContinuationFuncName;
            target.ErrorMessage = source.ErrorMessage;
            target.InvokedDialogAction = Convert(source.InvokedDialogAction);
            target.MultiTurnResult = Convert(source.MultiTurnResult);
            target.ResponseAudio = Convert(source.ResponseAudio);
            target.ResponseData = source.ResponseData;
            target.ResponseHtml = source.ResponseHtml;
            target.ResponseSsml = source.ResponseSsml;
            target.ResponseText = source.ResponseText;
            target.ResponseUrl = source.ResponseUrl;
            target.ResultConversationNode = source.ResultConversationNode;
            target.SuggestedQueries = source.SuggestedQueries;
            target.TriggerKeywords = Convert(source.TriggerKeywords);
            target.ResponsePrivacyClassification = (Durandal.Common.Logger.DataPrivacyClassification)source.ResponsePrivacyClassification.GetValueOrDefault(0);
            return target;
        }

        public static BONDAPI.PluginResult Convert(CAPI.PluginResult source)
        {
            if (source == null) return null;
            BONDAPI.PluginResult target = new BONDAPI.PluginResult();
            target.AugmentedQuery = source.AugmentedQuery;
            target.ClientAction = source.ClientAction;
            target.ContinuationFuncName = source.ContinuationFuncName;
            target.ErrorMessage = source.ErrorMessage;
            target.InvokedDialogAction = Convert(source.InvokedDialogAction);
            target.MultiTurnResult = Convert(source.MultiTurnResult);
            target.ResponseAudio = Convert(source.ResponseAudio);
            target.ResponseCode = Convert(source.ResponseCode);
            target.ResponseData = new Dictionary<string, string>(source.ResponseData);
            target.ResponseHtml = source.ResponseHtml;
            target.ResponseSsml = source.ResponseSsml;
            target.ResponseText = source.ResponseText;
            target.ResponseUrl = source.ResponseUrl;
            target.ResultConversationNode = source.ResultConversationNode;
            target.SuggestedQueries = source.SuggestedQueries;
            target.TriggerKeywords = Convert(source.TriggerKeywords);
            target.ResponsePrivacyClassification = (ushort)source.ResponsePrivacyClassification;
            return target;
        }

        #endregion

        #region PluginStrongName

        public static List<CAPI.PluginStrongName> Convert(List<BONDAPI.PluginStrongName> source)
        {
            if (source == null) return null;
            List<CAPI.PluginStrongName> target = new List<CAPI.PluginStrongName>();
            foreach (BONDAPI.PluginStrongName s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.PluginStrongName> Convert(List<CAPI.PluginStrongName> source)
        {
            if (source == null) return null;
            List<BONDAPI.PluginStrongName> target = new List<BONDAPI.PluginStrongName>();
            foreach (CAPI.PluginStrongName s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.PluginStrongName Convert(BONDAPI.PluginStrongName source)
        {
            if (source == null) return null;
            CAPI.PluginStrongName target = new CAPI.PluginStrongName(source.PluginId, source.MajorVersion, source.MinorVersion);
            return target;
        }

        public static BONDAPI.PluginStrongName Convert(CAPI.PluginStrongName source)
        {
            if (source == null) return null;
            BONDAPI.PluginStrongName target = new BONDAPI.PluginStrongName();
            target.PluginId = source.PluginId;
            target.MajorVersion = source.MajorVersion;
            target.MinorVersion = source.MinorVersion;
            return target;
        }

        #endregion

        #region QueryWithContext

        public static CAPI.QueryWithContext Convert(BONDAPI.QueryWithContext source)
        {
            if (source == null) return null;
            CAPI.QueryWithContext target = new CAPI.QueryWithContext();
            target.AuthenticationLevel = Convert(source.AuthenticationLevel);
            target.AuthScope = Convert(source.AuthScope);
            target.BargeInTimeMs = source.BargeInTimeMs;
            target.ClientContext = Convert(source.ClientContext);
            target.InputAudio = Convert(source.InputAudio);
            target.OriginalSpeechInput = Convert(source.OriginalSpeechInput);
            target.PastTurns = Convert(source.PastTurns);
            target.RequestFlags = (CAPI.QueryFlags)source.RequestFlags;
            target.RetryCount = source.RetryCount;
            target.Source = Convert(source.Source);
            target.TurnNum = source.TurnNum;
            target.Understanding = Convert(source.Understanding);
            target.RequestData = source.RequestData;
            return target;
        }

        public static BONDAPI.QueryWithContext Convert(CAPI.QueryWithContext source)
        {
            if (source == null) return null;
            BONDAPI.QueryWithContext target = new BONDAPI.QueryWithContext();
            target.AuthenticationLevel = Convert(source.AuthenticationLevel);
            target.AuthScope = Convert(source.AuthScope);
            target.BargeInTimeMs = source.BargeInTimeMs;
            target.ClientContext = Convert(source.ClientContext);
            target.InputAudio = Convert(source.InputAudio);
            target.OriginalSpeechInput = Convert(source.OriginalSpeechInput);
            target.PastTurns = Convert(source.PastTurns);
            target.RequestFlags = (uint)source.RequestFlags;
            target.RetryCount = source.RetryCount;
            target.Source = Convert(source.Source);
            target.TurnNum = source.TurnNum;
            target.Understanding = Convert(source.Understanding);

            // FIXME it would be nice if Bond used abstract dictionaries
            if (source.RequestData == null || source.RequestData is Dictionary<string, string>)
            {
                target.RequestData = source.RequestData as Dictionary<string, string>;
            }
            else
            {
                target.RequestData = new Dictionary<string, string>(source.RequestData);
            }

            return target;
        }

        #endregion

        #region RecognizedPhrase

        public static List<CAPI.RecognizedPhrase> Convert(List<BONDAPI.RecognizedPhrase> source)
        {
            if (source == null) return null;
            List<CAPI.RecognizedPhrase> target = new List<CAPI.RecognizedPhrase>();
            foreach (BONDAPI.RecognizedPhrase s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.RecognizedPhrase> Convert(List<CAPI.RecognizedPhrase> source)
        {
            if (source == null) return null;
            List<BONDAPI.RecognizedPhrase> target = new List<BONDAPI.RecognizedPhrase>();
            foreach (CAPI.RecognizedPhrase s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.RecognizedPhrase Convert(BONDAPI.RecognizedPhrase source)
        {
            if (source == null) return null;
            CAPI.RecognizedPhrase target = new CAPI.RecognizedPhrase();
            target.EntityContext = source.EntityContext;
            target.Recognition = Convert(source.Recognition);
            target.Sentiments = source.Sentiments;
            target.Utterance = source.Utterance;
            return target;
        }

        public static BONDAPI.RecognizedPhrase Convert(CAPI.RecognizedPhrase source)
        {
            if (source == null) return null;
            BONDAPI.RecognizedPhrase target = new BONDAPI.RecognizedPhrase();
            target.EntityContext = source.EntityContext;
            target.Recognition = Convert(source.Recognition) ?? new List<BONDAPI.RecoResult>();
            target.Sentiments = source.Sentiments;
            target.Utterance = source.Utterance ?? string.Empty;
            return target;
        }

        #endregion

        #region RecoResult

        public static List<CAPI.RecoResult> Convert(List<BONDAPI.RecoResult> source)
        {
            if (source == null) return null;
            List<CAPI.RecoResult> target = new List<CAPI.RecoResult>();
            foreach (BONDAPI.RecoResult s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.RecoResult> Convert(List<CAPI.RecoResult> source)
        {
            if (source == null) return null;
            List<BONDAPI.RecoResult> target = new List<BONDAPI.RecoResult>();
            foreach (CAPI.RecoResult s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.RecoResult Convert(BONDAPI.RecoResult source)
        {
            if (source == null) return null;
            CAPI.RecoResult target = new CAPI.RecoResult();
            target.Confidence = source.Confidence;
            target.Domain = source.Domain;
            target.Intent = source.Intent;
            target.Source = source.Source;
            target.TagHyps = Convert(source.TagHyps);
            target.Utterance = Convert(source.Utterance);
            return target;
        }

        public static BONDAPI.RecoResult Convert(CAPI.RecoResult source)
        {
            if (source == null) return null;
            BONDAPI.RecoResult target = new BONDAPI.RecoResult();
            target.Confidence = source.Confidence;
            target.Domain = source.Domain ?? string.Empty;
            target.Intent = source.Intent ?? string.Empty;
            target.Source = source.Source;
            target.TagHyps = Convert(source.TagHyps);
            target.Utterance = Convert(source.Utterance);
            return target;
        }

        #endregion

        #region Result

        public static CAPI.Result Convert(BONDAPI.Result source)
        {
            return (CAPI.Result)source;
        }

        public static BONDAPI.Result Convert(CAPI.Result source)
        {
            return (BONDAPI.Result)source;
        }

        #endregion

        #region SecurityToken

        public static List<CAPI.SecurityToken> Convert(List<BONDAPI.SecurityToken> source)
        {
            if (source == null) return null;
            List<CAPI.SecurityToken> target = new List<CAPI.SecurityToken>();
            foreach (BONDAPI.SecurityToken s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.SecurityToken> Convert(List<CAPI.SecurityToken> source)
        {
            if (source == null) return null;
            List<BONDAPI.SecurityToken> target = new List<BONDAPI.SecurityToken>();
            foreach (CAPI.SecurityToken s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.SecurityToken Convert(BONDAPI.SecurityToken source)
        {
            if (source == null) return null;
            CAPI.SecurityToken target = new CAPI.SecurityToken();
            target.Blue = source.Blue;
            target.Red = source.Red;
            target.Scope = Convert(source.Scope);
            return target;
        }

        public static BONDAPI.SecurityToken Convert(CAPI.SecurityToken source)
        {
            if (source == null) return null;
            BONDAPI.SecurityToken target = new BONDAPI.SecurityToken();
            target.Blue = source.Blue ?? string.Empty;
            target.Red = source.Red ?? string.Empty;
            target.Scope = Convert(source.Scope);
            return target;
        }

        #endregion

        #region Sentence

        public static CAPI.Sentence Convert(BONDAPI.Sentence source)
        {
            if (source == null) return null;
            CAPI.Sentence target = new CAPI.Sentence();
            target.Indices = source.Indices;
            target.LexicalForm = source.LexicalForm;
            target.NonTokens = source.NonTokens;
            target.OriginalText = source.OriginalText;
            target.Words = source.Words;
            return target;
        }

        public static BONDAPI.Sentence Convert(CAPI.Sentence source)
        {
            if (source == null) return null;
            BONDAPI.Sentence target = new BONDAPI.Sentence();
            target.Indices = source.Indices ?? new List<int>();
            target.LexicalForm = source.LexicalForm;
            target.NonTokens = source.NonTokens;
            target.OriginalText = source.OriginalText ?? string.Empty;
            target.Words = source.Words ?? new List<string>();
            return target;
        }

        #endregion

        #region SerializedConversationState

        public static List<CAPI.SerializedConversationState> Convert(List<BONDAPI.SerializedConversationState> source)
        {
            if (source == null) return null;
            List<CAPI.SerializedConversationState> target = new List<CAPI.SerializedConversationState>();
            foreach (BONDAPI.SerializedConversationState s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.SerializedConversationState> Convert(List<CAPI.SerializedConversationState> source)
        {
            if (source == null) return null;
            List<BONDAPI.SerializedConversationState> target = new List<BONDAPI.SerializedConversationState>();
            foreach (CAPI.SerializedConversationState s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.SerializedConversationState Convert(BONDAPI.SerializedConversationState source)
        {
            if (source == null) return null;
            CAPI.SerializedConversationState target = new CAPI.SerializedConversationState();
            target.ConversationExpireTime = source.ConversationExpireTime;
            target.CurrentConversationNode = source.CurrentConversationNode;
            target.CurrentPluginDomain = source.CurrentPluginDomain;
            target.LastMultiturnState = Convert(source.LastMultiturnState);
            target.NextContinuationFuncName = source.NextContinuationFuncName;
            target.PreviousConversationTurns = Convert(source.PreviousConversationTurns);
            target.RetryNum = source.RetryNum;
            target.SessionStore = Convert(source.SessionStore);
            target.TurnNum = source.TurnNum;
            target.CurrentPluginId = source.CurrentPluginId;
            target.CurrentPluginVersionMajor = source.CurrentPluginVersionMajor;
            target.CurrentPluginVersionMinor = source.CurrentPluginVersionMinor;
            return target;
        }

        public static BONDAPI.SerializedConversationState Convert(CAPI.SerializedConversationState source)
        {
            if (source == null) return null;
            BONDAPI.SerializedConversationState target = new BONDAPI.SerializedConversationState();
            target.ConversationExpireTime = source.ConversationExpireTime;
            target.CurrentConversationNode = source.CurrentConversationNode ?? string.Empty;
            target.CurrentPluginDomain = source.CurrentPluginDomain ?? string.Empty;
            target.LastMultiturnState = Convert(source.LastMultiturnState);
            target.NextContinuationFuncName = source.NextContinuationFuncName;
            target.PreviousConversationTurns = Convert(source.PreviousConversationTurns);
            target.RetryNum = source.RetryNum;
            target.SessionStore = Convert(source.SessionStore);
            target.TurnNum = source.TurnNum;
            target.CurrentPluginId = source.CurrentPluginId;
            target.CurrentPluginVersionMajor = source.CurrentPluginVersionMajor;
            target.CurrentPluginVersionMinor = source.CurrentPluginVersionMinor;
            return target;
        }

        #endregion

        #region SerializedConversationStateStack

        public static CAPI.SerializedConversationStateStack Convert(BONDAPI.SerializedConversationStateStack source)
        {
            if (source == null) return null;
            CAPI.SerializedConversationStateStack target = new CAPI.SerializedConversationStateStack();
            target.Stack = Convert(source.Stack);
            return target;
        }

        public static BONDAPI.SerializedConversationStateStack Convert(CAPI.SerializedConversationStateStack source)
        {
            if (source == null) return null;
            BONDAPI.SerializedConversationStateStack target = new BONDAPI.SerializedConversationStateStack();
            target.Stack = Convert(source.Stack);
            return target;
        }

        #endregion

        #region SerializedConversationEdge

        public static List<CAPI.SerializedConversationEdge> Convert(List<BONDAPI.SerializedConversationEdge> source)
        {
            if (source == null) return null;
            List<CAPI.SerializedConversationEdge> target = new List<CAPI.SerializedConversationEdge>();
            foreach (BONDAPI.SerializedConversationEdge s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.SerializedConversationEdge> Convert(List<CAPI.SerializedConversationEdge> source)
        {
            if (source == null) return null;
            List<BONDAPI.SerializedConversationEdge> target = new List<BONDAPI.SerializedConversationEdge>();
            foreach (CAPI.SerializedConversationEdge s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.SerializedConversationEdge Convert(BONDAPI.SerializedConversationEdge source)
        {
            if (source == null) return null;
            CAPI.SerializedConversationEdge target = new CAPI.SerializedConversationEdge();
            target.ExternalDomain = source.ExternalDomain;
            target.ExternalIntent = source.ExternalIntent;
            target.Intent = source.Intent;
            target.Scope = Convert(source.Scope);
            target.TargetNodeName = source.TargetNodeName;
            return target;
        }

        public static BONDAPI.SerializedConversationEdge Convert(CAPI.SerializedConversationEdge source)
        {
            if (source == null) return null;
            BONDAPI.SerializedConversationEdge target = new BONDAPI.SerializedConversationEdge();
            target.ExternalDomain = source.ExternalDomain;
            target.ExternalIntent = source.ExternalIntent;
            target.Intent = source.Intent;
            target.Scope = Convert(source.Scope);
            target.TargetNodeName = source.TargetNodeName;
            return target;
        }

        #endregion

        #region SerializedConversationNode

        public static List<CAPI.SerializedConversationNode> Convert(List<BONDAPI.SerializedConversationNode> source)
        {
            if (source == null) return null;
            List<CAPI.SerializedConversationNode> target = new List<CAPI.SerializedConversationNode>();
            foreach (BONDAPI.SerializedConversationNode s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.SerializedConversationNode> Convert(List<CAPI.SerializedConversationNode> source)
        {
            if (source == null) return null;
            List<BONDAPI.SerializedConversationNode> target = new List<BONDAPI.SerializedConversationNode>();
            foreach (CAPI.SerializedConversationNode s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.SerializedConversationNode Convert(BONDAPI.SerializedConversationNode source)
        {
            if (source == null) return null;
            CAPI.SerializedConversationNode target = new CAPI.SerializedConversationNode();
            target.Edges = Convert(source.Edges);
            target.HandlerFunction = source.HandlerFunction;
            target.NodeName = source.NodeName;
            target.RetryHandler = source.RetryHandler;
            return target;
        }

        public static BONDAPI.SerializedConversationNode Convert(CAPI.SerializedConversationNode source)
        {
            if (source == null) return null;
            BONDAPI.SerializedConversationNode target = new BONDAPI.SerializedConversationNode();
            target.Edges = Convert(source.Edges);
            target.HandlerFunction = source.HandlerFunction;
            target.NodeName = source.NodeName;
            target.RetryHandler = source.RetryHandler;
            return target;
        }

        #endregion

        #region SerializedConversationTree

        public static CAPI.SerializedConversationTree Convert(BONDAPI.SerializedConversationTree source)
        {
            if (source == null) return null;
            CAPI.SerializedConversationTree target = new CAPI.SerializedConversationTree();
            target.LocalDomain = source.LocalDomain;
            target.Nodes = Convert(source.Nodes);
            return target;
        }

        public static BONDAPI.SerializedConversationTree Convert(CAPI.SerializedConversationTree source)
        {
            if (source == null) return null;
            BONDAPI.SerializedConversationTree target = new BONDAPI.SerializedConversationTree();
            target.LocalDomain = source.LocalDomain;
            target.Nodes = Convert(source.Nodes);
            return target;
        }

        #endregion

        #region SerializedMetricEvent

        public static List<CAPI.SerializedMetricEvent> Convert(List<BONDAPI.SerializedMetricEvent> source)
        {
            if (source == null) return null;
            List<CAPI.SerializedMetricEvent> target = new List<CAPI.SerializedMetricEvent>();
            foreach (BONDAPI.SerializedMetricEvent s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.SerializedMetricEvent> Convert(List<CAPI.SerializedMetricEvent> source)
        {
            if (source == null) return null;
            List<BONDAPI.SerializedMetricEvent> target = new List<BONDAPI.SerializedMetricEvent>();
            foreach (CAPI.SerializedMetricEvent s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.SerializedMetricEvent Convert(BONDAPI.SerializedMetricEvent source)
        {
            if (source == null) return null;
            CAPI.SerializedMetricEvent target = new CAPI.SerializedMetricEvent();
            target.CounterName = source.CounterName;
            target.MetricType = source.MetricType;
            target.SerializedDimensions = source.SerializedDimensions;
            target.SerializedValues = source.SerializedValues;
            return target;
        }

        public static BONDAPI.SerializedMetricEvent Convert(CAPI.SerializedMetricEvent source)
        {
            if (source == null) return null;
            BONDAPI.SerializedMetricEvent target = new BONDAPI.SerializedMetricEvent();
            target.CounterName = source.CounterName;
            target.MetricType = source.MetricType;
            target.SerializedDimensions = source.SerializedDimensions;
            target.SerializedValues = source.SerializedValues;
            return target;
        }

        #endregion

        #region InstrumentationEventList

        public static CAPI.SerializedMetricEventList Convert(BONDAPI.SerializedMetricEventList source)
        {
            if (source == null) return null;
            CAPI.SerializedMetricEventList target = new CAPI.SerializedMetricEventList();
            target.Events = Convert(source.Events);
            return target;
        }

        public static BONDAPI.SerializedMetricEventList Convert(CAPI.SerializedMetricEventList source)
        {
            if (source == null) return null;
            BONDAPI.SerializedMetricEventList target = new BONDAPI.SerializedMetricEventList();
            target.Events = Convert(source.Events) ?? new List<BONDAPI.SerializedMetricEvent>();
            return target;
        }

        #endregion

        #region SlotValue

        public static List<CAPI.SlotValue> Convert(List<BONDAPI.SlotValue> source)
        {
            if (source == null) return null;
            List<CAPI.SlotValue> target = new List<CAPI.SlotValue>();
            foreach (BONDAPI.SlotValue s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.SlotValue> Convert(List<CAPI.SlotValue> source)
        {
            if (source == null) return null;
            List<BONDAPI.SlotValue> target = new List<BONDAPI.SlotValue>();
            foreach (CAPI.SlotValue s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.SlotValue Convert(BONDAPI.SlotValue source)
        {
            if (source == null) return null;
            CAPI.SlotValue target = new CAPI.SlotValue();
            target.Annotations = source.Annotations;
            target.Alternates = source.Alternates;
            target.Format = Convert(source.Format);
            target.LexicalForm = source.LexicalForm;
            target.Name = source.Name;
            target.Value = source.Value;
            return target;
        }

        public static BONDAPI.SlotValue Convert(CAPI.SlotValue source)
        {
            if (source == null) return null;
            BONDAPI.SlotValue target = new BONDAPI.SlotValue();
            target.Annotations = source.Annotations ?? new Dictionary<string, string>();
            target.Alternates = source.Alternates;
            target.Format = Convert(source.Format);
            target.LexicalForm = source.LexicalForm;
            target.Name = source.Name ?? string.Empty;
            target.Value = source.Value ?? string.Empty;
            return target;
        }

        #endregion

        #region SlotValueFormat

        public static CAPI.SlotValueFormat Convert(BONDAPI.SlotValueFormat source)
        {
            return (CAPI.SlotValueFormat)source;
        }

        public static BONDAPI.SlotValueFormat Convert(CAPI.SlotValueFormat source)
        {
            return (BONDAPI.SlotValueFormat)source;
        }

        #endregion

        #region SpeechHypothesis_v16

        public static List<CAPI.SpeechHypothesis_v16> Convert(List<BONDAPI.SpeechHypothesis_v16> source)
        {
            if (source == null) return null;
            List<CAPI.SpeechHypothesis_v16> target = new List<CAPI.SpeechHypothesis_v16>();
            foreach (BONDAPI.SpeechHypothesis_v16 s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.SpeechHypothesis_v16> Convert(List<CAPI.SpeechHypothesis_v16> source)
        {
            if (source == null) return null;
            List<BONDAPI.SpeechHypothesis_v16> target = new List<BONDAPI.SpeechHypothesis_v16>();
            foreach (CAPI.SpeechHypothesis_v16 s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.SpeechHypothesis_v16 Convert(BONDAPI.SpeechHypothesis_v16 source)
        {
            if (source == null) return null;
            CAPI.SpeechHypothesis_v16 target = new CAPI.SpeechHypothesis_v16();
            target.Confidence = source.Confidence;
            target.LexicalForm = source.LexicalForm;
            target.Utterance = source.Utterance;
            return target;
        }

        public static BONDAPI.SpeechHypothesis_v16 Convert(CAPI.SpeechHypothesis_v16 source)
        {
            if (source == null) return null;
            BONDAPI.SpeechHypothesis_v16 target = new BONDAPI.SpeechHypothesis_v16();
            target.Confidence = source.Confidence;
            target.LexicalForm = source.LexicalForm;
            target.Utterance = source.Utterance ?? string.Empty;
            return target;
        }

        #endregion

        #region SpeechPhraseElement

        public static List<CAPI.SpeechPhraseElement> Convert(List<BONDAPI.SpeechPhraseElement> source)
        {
            if (source == null) return null;
            List<CAPI.SpeechPhraseElement> target = new List<CAPI.SpeechPhraseElement>();
            foreach (BONDAPI.SpeechPhraseElement s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.SpeechPhraseElement> Convert(List<CAPI.SpeechPhraseElement> source)
        {
            if (source == null) return null;
            List<BONDAPI.SpeechPhraseElement> target = new List<BONDAPI.SpeechPhraseElement>();
            foreach (CAPI.SpeechPhraseElement s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.SpeechPhraseElement Convert(BONDAPI.SpeechPhraseElement source)
        {
            if (source == null) return null;
            CAPI.SpeechPhraseElement target = new CAPI.SpeechPhraseElement();
            target.AudioTimeLength = TimeSpan.FromMilliseconds(source.AudioTimeLength);
            target.AudioTimeOffset = TimeSpan.FromMilliseconds(source.AudioTimeOffset);
            target.DisplayText = source.DisplayText;
            target.LexicalForm = source.LexicalForm;
            target.Pronunciation = source.Pronunciation;
            target.SREngineConfidence = source.SREngineConfidence;
            return target;
        }

        public static BONDAPI.SpeechPhraseElement Convert(CAPI.SpeechPhraseElement source)
        {
            if (source == null) return null;
            BONDAPI.SpeechPhraseElement target = new BONDAPI.SpeechPhraseElement();
            target.AudioTimeLength = (uint)source.AudioTimeLength.GetValueOrDefault().TotalMilliseconds;
            target.AudioTimeOffset = (uint)source.AudioTimeOffset.GetValueOrDefault().TotalMilliseconds;
            target.DisplayText = source.DisplayText ?? string.Empty;
            target.LexicalForm = source.LexicalForm ?? string.Empty;
            target.Pronunciation = source.Pronunciation ?? string.Empty;
            target.SREngineConfidence = source.SREngineConfidence;
            return target;
        }

        #endregion

        #region SpeechRecognitionResult

        public static CAPI.SpeechRecognitionResult Convert(BONDAPI.SpeechRecognitionResult source)
        {
            if (source == null) return null;
            CAPI.SpeechRecognitionResult target = new CAPI.SpeechRecognitionResult();
            target.ConfusionNetworkData = Convert(source.ConfusionNetworkData);
            target.RecognitionStatus = Convert(source.RecognitionStatus);
            target.RecognizedPhrases = Convert(source.RecognizedPhrases);
            return target;
        }

        public static BONDAPI.SpeechRecognitionResult Convert(CAPI.SpeechRecognitionResult source)
        {
            if (source == null) return null;
            BONDAPI.SpeechRecognitionResult target = new BONDAPI.SpeechRecognitionResult();
            target.ConfusionNetworkData = Convert(source.ConfusionNetworkData);
            target.RecognitionStatus = Convert(source.RecognitionStatus);
            target.RecognizedPhrases = Convert(source.RecognizedPhrases);
            return target;
        }

        #endregion

        #region SpeechRecognizedPhrase

        public static List<CAPI.SpeechRecognizedPhrase> Convert(List<BONDAPI.SpeechRecognizedPhrase> source)
        {
            if (source == null) return null;
            List<CAPI.SpeechRecognizedPhrase> target = new List<CAPI.SpeechRecognizedPhrase>();
            foreach (BONDAPI.SpeechRecognizedPhrase s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.SpeechRecognizedPhrase> Convert(List<CAPI.SpeechRecognizedPhrase> source)
        {
            if (source == null) return null;
            List<BONDAPI.SpeechRecognizedPhrase> target = new List<BONDAPI.SpeechRecognizedPhrase>();
            foreach (CAPI.SpeechRecognizedPhrase s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.SpeechRecognizedPhrase Convert(BONDAPI.SpeechRecognizedPhrase source)
        {
            if (source == null) return null;
            CAPI.SpeechRecognizedPhrase target = new CAPI.SpeechRecognizedPhrase();
            target.AudioTimeLength = TimeSpan.FromMilliseconds(source.AudioTimeLength);
            target.AudioTimeOffset = TimeSpan.FromMilliseconds(source.AudioTimeOffset);
            target.DisplayText = source.DisplayText;
            target.InverseTextNormalizationResults = source.InverseTextNormalizationResults;
            target.IPASyllables = source.LexicalForm;
            target.Locale = source.Locale;
            target.MaskedInverseTextNormalizationResults = source.MaskedInverseTextNormalizationResults;
            target.PhraseElements = Convert(source.PhraseElements);
            target.ProfanityTags = Convert(source.ProfanityTags);
            target.SREngineConfidence = source.SREngineConfidence;
            return target;
        }

        public static BONDAPI.SpeechRecognizedPhrase Convert(CAPI.SpeechRecognizedPhrase source)
        {
            if (source == null) return null;
            BONDAPI.SpeechRecognizedPhrase target = new BONDAPI.SpeechRecognizedPhrase();
            target.AudioTimeLength = (uint)source.AudioTimeLength.GetValueOrDefault().TotalMilliseconds;
            target.AudioTimeOffset = (uint)source.AudioTimeOffset.GetValueOrDefault().TotalMilliseconds;
            target.DisplayText = source.DisplayText;
            target.InverseTextNormalizationResults = source.InverseTextNormalizationResults;
            target.LexicalForm = source.IPASyllables;
            target.Locale = source.Locale;
            target.MaskedInverseTextNormalizationResults = source.MaskedInverseTextNormalizationResults;
            target.PhraseElements = Convert(source.PhraseElements);
            target.ProfanityTags = Convert(source.ProfanityTags);
            target.SREngineConfidence = source.SREngineConfidence;
            return target;
        }

        #endregion

        #region SpeechRecognitionStatus

        public static CAPI.SpeechRecognitionStatus Convert(BONDAPI.SpeechRecognitionStatus source)
        {
            switch (source)
            {
                case BONDAPI.SpeechRecognitionStatus.BabbleTimeout:
                    return CAPI.SpeechRecognitionStatus.BabbleTimeout;
                case BONDAPI.SpeechRecognitionStatus.Cancelled:
                    return CAPI.SpeechRecognitionStatus.Cancelled;
                case BONDAPI.SpeechRecognitionStatus.Error:
                    return CAPI.SpeechRecognitionStatus.Error;
                case BONDAPI.SpeechRecognitionStatus.InitialSilenceTimeout:
                    return CAPI.SpeechRecognitionStatus.InitialSilenceTimeout;
                case BONDAPI.SpeechRecognitionStatus.NoMatch:
                    return CAPI.SpeechRecognitionStatus.NoMatch;
                case BONDAPI.SpeechRecognitionStatus.None:
                    return CAPI.SpeechRecognitionStatus.None;
                case BONDAPI.SpeechRecognitionStatus.Success:
                    return CAPI.SpeechRecognitionStatus.Success;
                default:
                    return CAPI.SpeechRecognitionStatus.None;
            }
        }

        public static BONDAPI.SpeechRecognitionStatus Convert(CAPI.SpeechRecognitionStatus source)
        {
            switch (source)
            {
                case CAPI.SpeechRecognitionStatus.BabbleTimeout:
                    return BONDAPI.SpeechRecognitionStatus.BabbleTimeout;
                case CAPI.SpeechRecognitionStatus.Cancelled:
                    return BONDAPI.SpeechRecognitionStatus.Cancelled;
                case CAPI.SpeechRecognitionStatus.Error:
                    return BONDAPI.SpeechRecognitionStatus.Error;
                case CAPI.SpeechRecognitionStatus.InitialSilenceTimeout:
                    return BONDAPI.SpeechRecognitionStatus.InitialSilenceTimeout;
                case CAPI.SpeechRecognitionStatus.NoMatch:
                    return BONDAPI.SpeechRecognitionStatus.NoMatch;
                case CAPI.SpeechRecognitionStatus.None:
                    return BONDAPI.SpeechRecognitionStatus.None;
                case CAPI.SpeechRecognitionStatus.Success:
                    return BONDAPI.SpeechRecognitionStatus.Success;
                default:
                    return BONDAPI.SpeechRecognitionStatus.None;
            }
        }

        #endregion

        #region SpeechSynthesisRequest

        public static CAPI.SpeechSynthesisRequest Convert(BONDAPI.SpeechSynthesisRequest source)
        {
            if (source == null) return null;
            CAPI.SpeechSynthesisRequest target = new CAPI.SpeechSynthesisRequest();
            target.Ssml = source.Ssml;
            target.Plaintext = source.Plaintext;
            target.Locale = LanguageCode.TryParse(source.Locale);
            target.VoiceGender = Convert(source.VoiceGender);
            return target;
        }

        public static BONDAPI.SpeechSynthesisRequest Convert(CAPI.SpeechSynthesisRequest source)
        {
            if (source == null) return null;
            BONDAPI.SpeechSynthesisRequest target = new BONDAPI.SpeechSynthesisRequest();
            target.Ssml = source.Ssml;
            target.Plaintext = source.Plaintext;
            target.Locale = source.Locale.ToBcp47Alpha2String();
            target.VoiceGender = Convert(source.VoiceGender);
            return target;
        }

        #endregion

        #region SynthesizedSpeech

        public static CAPI.SynthesizedSpeech Convert(BONDAPI.SynthesizedSpeech source)
        {
            if (source == null) return null;
            CAPI.SynthesizedSpeech target = new CAPI.SynthesizedSpeech();
            target.Audio = Convert(source.Audio);
            target.Locale = source.Locale;
            target.PlainText = source.PlainText;
            target.Ssml = source.Ssml;
            target.Words = Convert(source.Words);
            return target;
        }

        public static BONDAPI.SynthesizedSpeech Convert(CAPI.SynthesizedSpeech source)
        {
            if (source == null) return null;
            BONDAPI.SynthesizedSpeech target = new BONDAPI.SynthesizedSpeech();
            target.Audio = Convert(source.Audio);
            target.Locale = source.Locale;
            target.PlainText = source.PlainText;
            target.Ssml = source.Ssml;
            target.Words = Convert(source.Words);
            return target;
        }

        #endregion

        #region SynthesizedWord

        public static List<CAPI.SynthesizedWord> Convert(List<BONDAPI.SynthesizedWord> source)
        {
            if (source == null) return null;
            List<CAPI.SynthesizedWord> target = new List<CAPI.SynthesizedWord>();
            foreach (BONDAPI.SynthesizedWord s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.SynthesizedWord> Convert(IList<CAPI.SynthesizedWord> source)
        {
            if (source == null) return null;
            List<BONDAPI.SynthesizedWord> target = new List<BONDAPI.SynthesizedWord>();
            foreach (CAPI.SynthesizedWord s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.SynthesizedWord Convert(BONDAPI.SynthesizedWord source)
        {
            if (source == null) return null;
            CAPI.SynthesizedWord target = new CAPI.SynthesizedWord();
            target.Word = source.Word;
            target.Offset = TimeSpan.FromMilliseconds(source.Offset);
            target.ApproximateLength = TimeSpan.FromMilliseconds(source.ApproximateLength);
            return target;
        }

        public static BONDAPI.SynthesizedWord Convert(CAPI.SynthesizedWord source)
        {
            if (source == null) return null;
            BONDAPI.SynthesizedWord target = new BONDAPI.SynthesizedWord();
            target.Word = source.Word;
            target.Offset = (long)source.Offset.TotalMilliseconds;
            target.ApproximateLength = (long)source.ApproximateLength.TotalMilliseconds;
            return target;
        }

        #endregion

        #region Tag

        public static List<CAPI.Tag> Convert(List<BONDAPI.Tag> source)
        {
            if (source == null) return null;
            List<CAPI.Tag> target = new List<CAPI.Tag>();
            foreach (BONDAPI.Tag s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.Tag> Convert(List<CAPI.Tag> source)
        {
            if (source == null) return null;
            List<BONDAPI.Tag> target = new List<BONDAPI.Tag>();
            foreach (CAPI.Tag s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.Tag Convert(BONDAPI.Tag source)
        {
            if (source == null) return null;
            CAPI.Tag target = new CAPI.Tag();
            target.Index = source.Index;
            target.Length = source.Length;
            return target;
        }

        public static BONDAPI.Tag Convert(CAPI.Tag source)
        {
            if (source == null) return null;
            BONDAPI.Tag target = new BONDAPI.Tag();
            target.Index = source.Index;
            target.Length = source.Length;
            return target;
        }

        #endregion

        #region TaggedData

        public static List<CAPI.TaggedData> Convert(List<BONDAPI.TaggedData> source)
        {
            if (source == null) return null;
            List<CAPI.TaggedData> target = new List<CAPI.TaggedData>();
            foreach (BONDAPI.TaggedData s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.TaggedData> Convert(List<CAPI.TaggedData> source)
        {
            if (source == null) return null;
            List<BONDAPI.TaggedData> target = new List<BONDAPI.TaggedData>();
            foreach (CAPI.TaggedData s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.TaggedData Convert(BONDAPI.TaggedData source)
        {
            if (source == null) return null;
            CAPI.TaggedData target = new CAPI.TaggedData();
            target.Annotations = source.Annotations;
            target.Confidence = source.Confidence;
            target.Slots = Convert(source.Slots);
            target.Utterance = source.Utterance;
            return target;
        }

        public static BONDAPI.TaggedData Convert(CAPI.TaggedData source)
        {
            if (source == null) return null;
            BONDAPI.TaggedData target = new BONDAPI.TaggedData();
            target.Annotations = source.Annotations ?? new Dictionary<string, string>();
            target.Confidence = source.Confidence;
            target.Slots = Convert(source.Slots) ?? new List<BONDAPI.SlotValue>();
            target.Utterance = source.Utterance ?? string.Empty;
            return target;
        }

        #endregion

        #region TriggerKeyword

        public static List<CAPI.TriggerKeyword> Convert(List<BONDAPI.TriggerKeyword> source)
        {
            if (source == null) return null;
            List<CAPI.TriggerKeyword> target = new List<CAPI.TriggerKeyword>();
            foreach (BONDAPI.TriggerKeyword s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static List<BONDAPI.TriggerKeyword> Convert(List<CAPI.TriggerKeyword> source)
        {
            if (source == null) return null;
            List<BONDAPI.TriggerKeyword> target = new List<BONDAPI.TriggerKeyword>();
            foreach (CAPI.TriggerKeyword s in source)
            {
                target.Add(Convert(s));
            }

            return target;
        }

        public static CAPI.TriggerKeyword Convert(BONDAPI.TriggerKeyword source)
        {
            if (source == null) return null;
            CAPI.TriggerKeyword target = new CAPI.TriggerKeyword();
            target.AllowBargeIn = source.AllowBargeIn;
            target.ExpireTimeSeconds = source.ExpireTimeSeconds;
            target.TriggerPhrase = source.TriggerPhrase;
            return target;
        }

        public static BONDAPI.TriggerKeyword Convert(CAPI.TriggerKeyword source)
        {
            if (source == null) return null;
            BONDAPI.TriggerKeyword target = new BONDAPI.TriggerKeyword();
            target.AllowBargeIn = source.AllowBargeIn;
            target.ExpireTimeSeconds = source.ExpireTimeSeconds;
            target.TriggerPhrase = source.TriggerPhrase ?? string.Empty;
            return target;
        }

        #endregion

        #region TriggerProcessingResponse

        public static CAPI.TriggerProcessingResponse Convert(BONDAPI.TriggerProcessingResponse source)
        {
            if (source == null) return null;
            CAPI.TriggerResult convertedTriggerResult = Convert(source.PluginOutput);
            InMemoryDataStore convertedSessionStore = Convert(source.UpdatedSessionStore);
            CAPI.TriggerProcessingResponse target = new CAPI.TriggerProcessingResponse(convertedTriggerResult, convertedSessionStore);
            return target;
        }

        public static BONDAPI.TriggerProcessingResponse Convert(CAPI.TriggerProcessingResponse source)
        {
            if (source == null) return null;
            BONDAPI.TriggerProcessingResponse target = new BONDAPI.TriggerProcessingResponse();
            target.PluginOutput = Convert(source.PluginOutput);
            target.UpdatedSessionStore = Convert(source.UpdatedSessionStore);
            return target;
        }

        #endregion

        #region TriggerResult

        public static CAPI.TriggerResult Convert(BONDAPI.TriggerResult source)
        {
            if (source == null) return null;
            CAPI.TriggerResult target = new CAPI.TriggerResult();
            target.ActionDescription = source.ActionDescription;
            target.ActionKnownAs = Convert(source.ActionKnownAs);
            target.ActionName = source.ActionName;
            target.ActionNameSsml = source.ActionNameSsml;
            target.BoostResult = Convert(source.BoostResult);
            return target;
        }

        public static BONDAPI.TriggerResult Convert(CAPI.TriggerResult source)
        {
            if (source == null) return null;
            BONDAPI.TriggerResult target = new BONDAPI.TriggerResult();
            target.ActionDescription = source.ActionDescription;
            target.ActionKnownAs = Convert(source.ActionKnownAs);
            target.ActionName = source.ActionName;
            target.ActionNameSsml = source.ActionNameSsml;
            target.BoostResult = Convert(source.BoostResult);
            return target;
        }

        #endregion

        #region UrlScope

        public static CAPI.UrlScope Convert(BONDAPI.UrlScope source)
        {
            return (CAPI.UrlScope)source;
        }

        public static BONDAPI.UrlScope Convert(CAPI.UrlScope source)
        {
            return (BONDAPI.UrlScope)source;
        }

        #endregion

        #region VoiceGender

        public static CAPI.VoiceGender Convert(BONDAPI.VoiceGender source)
        {
            switch (source)
            {
                case BONDAPI.VoiceGender.Unspecified:
                    return CAPI.VoiceGender.Unspecified;
                case BONDAPI.VoiceGender.Male:
                    return CAPI.VoiceGender.Male;
                case BONDAPI.VoiceGender.Female:
                    return CAPI.VoiceGender.Female;
                default:
                    return CAPI.VoiceGender.Unspecified;
            }
        }

        public static BONDAPI.VoiceGender Convert(CAPI.VoiceGender source)
        {
            switch (source)
            {
                case CAPI.VoiceGender.Unspecified:
                    return BONDAPI.VoiceGender.Unspecified;
                case CAPI.VoiceGender.Male:
                    return BONDAPI.VoiceGender.Male;
                case CAPI.VoiceGender.Female:
                    return BONDAPI.VoiceGender.Female;
                default:
                    return BONDAPI.VoiceGender.Unspecified;
            }
        }

        #endregion

        #region Remoting.KeepAliveRequest

        public static CREMOTING.KeepAliveRequest Convert(BONDREMOTING.KeepAliveRequest source)
        {
            if (source == null) return null;
            CREMOTING.KeepAliveRequest target = new CREMOTING.KeepAliveRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.IntervalMs = source.IntervalMs;
            return target;
        }

        public static BONDREMOTING.KeepAliveRequest Convert(CREMOTING.KeepAliveRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.KeepAliveRequest target = new BONDREMOTING.KeepAliveRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.IntervalMs = source.IntervalMs;
            return target;
        }

        #endregion

        #region Remoting.RemoteBlobResponse

        public static CREMOTING.RemoteProcedureResponse<ArraySegment<byte>> Convert(BONDREMOTING.RemoteBlobResponse source)
        {
            if (source == null) return null;
            string methodName = source.MethodName;
            CREMOTING.RemoteProcedureResponse<ArraySegment<byte>> target = new CREMOTING.RemoteProcedureResponse<ArraySegment<byte>>(source.MethodName, source.ReturnVal);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteBlobResponse Convert(CREMOTING.RemoteProcedureResponse<ArraySegment<byte>> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteBlobResponse target = new BONDREMOTING.RemoteBlobResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = source.ReturnVal;
            return target;
        }

        #endregion

        #region Remoting.RemoteBoolResponse

        public static CREMOTING.RemoteProcedureResponse<bool> Convert(BONDREMOTING.RemoteBoolResponse source)
        {
            if (source == null) return null;
            string methodName = source.MethodName;
            CREMOTING.RemoteProcedureResponse<bool> target = new CREMOTING.RemoteProcedureResponse<bool>(source.MethodName, source.ReturnVal);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteBoolResponse Convert(CREMOTING.RemoteProcedureResponse<bool> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteBoolResponse target = new BONDREMOTING.RemoteBoolResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = source.ReturnVal;

            return target;
        }

        #endregion

        #region Remoting.RemoteCachedWebDataResponse

        public static CREMOTING.RemoteProcedureResponse<CAPI.CachedWebData> Convert(BONDREMOTING.RemoteCachedWebDataResponse source)
        {
            if (source == null) return null;
            string methodName = source.MethodName;
            CAPI.CachedWebData webData = Convert(source.ReturnVal);
            CREMOTING.RemoteProcedureResponse<CAPI.CachedWebData> target = new CREMOTING.RemoteProcedureResponse<CAPI.CachedWebData>(source.MethodName, webData);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteCachedWebDataResponse Convert(CREMOTING.RemoteProcedureResponse<CAPI.CachedWebData> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteCachedWebDataResponse target = new BONDREMOTING.RemoteCachedWebDataResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = Convert(source.ReturnVal);

            return target;
        }

        #endregion

        #region Remoting.RemoteCrashContainerRequest

        public static CREMOTING.RemoteCrashContainerRequest Convert(BONDREMOTING.RemoteCrashContainerRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteCrashContainerRequest target = new CREMOTING.RemoteCrashContainerRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            return target;
        }

        public static BONDREMOTING.RemoteCrashContainerRequest Convert(CREMOTING.RemoteCrashContainerRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteCrashContainerRequest target = new BONDREMOTING.RemoteCrashContainerRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            return target;
        }

        #endregion

        #region Remoting.RemoteCreateOAuthUriRequest

        public static CREMOTING.RemoteCreateOAuthUriRequest Convert(BONDREMOTING.RemoteCreateOAuthUriRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteCreateOAuthUriRequest target = new CREMOTING.RemoteCreateOAuthUriRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.OAuthConfig = Convert(source.OAuthConfig);
            target.PluginId = Convert(source.PluginId);
            target.UserId = source.UserId;
            return target;
        }

        public static BONDREMOTING.RemoteCreateOAuthUriRequest Convert(CREMOTING.RemoteCreateOAuthUriRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteCreateOAuthUriRequest target = new BONDREMOTING.RemoteCreateOAuthUriRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.OAuthConfig = Convert(source.OAuthConfig);
            target.PluginId = Convert(source.PluginId);
            target.UserId = source.UserId;
            return target;
        }

        #endregion

        #region Remoting.RemoteCrossDomainResponseRequest

        public static CREMOTING.RemoteCrossDomainResponseRequest Convert(BONDREMOTING.RemoteCrossDomainResponseRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteCrossDomainResponseRequest target = new CREMOTING.RemoteCrossDomainResponseRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            target.Context = Convert(source.Context);
            target.TraceId = source.TraceId;
            target.ValidLogLevels = source.ValidLogLevels;
            target.SessionStore = Convert(source.SessionStore);
            return target;
        }

        public static BONDREMOTING.RemoteCrossDomainResponseRequest Convert(CREMOTING.RemoteCrossDomainResponseRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteCrossDomainResponseRequest target = new BONDREMOTING.RemoteCrossDomainResponseRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            target.Context = Convert(source.Context);
            target.TraceId = source.TraceId;
            target.ValidLogLevels = source.ValidLogLevels;
            target.SessionStore = Convert(source.SessionStore);
            return target;
        }

        #endregion

        #region Remoting.RemoteCrossDomainResponseResponseResponse

        public static CREMOTING.RemoteProcedureResponse<CAPI.CrossDomainResponseResponse> Convert(BONDREMOTING.RemoteCrossDomainResponseResponseResponse source)
        {
            if (source == null) return null;
            string methodName = source.MethodName;
            CAPI.CrossDomainResponseResponse data = Convert(source.ReturnVal);
            CREMOTING.RemoteProcedureResponse<CAPI.CrossDomainResponseResponse> target = new CREMOTING.RemoteProcedureResponse<CAPI.CrossDomainResponseResponse>(source.MethodName, data);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteCrossDomainResponseResponseResponse Convert(CREMOTING.RemoteProcedureResponse<CAPI.CrossDomainResponseResponse> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteCrossDomainResponseResponseResponse target = new BONDREMOTING.RemoteCrossDomainResponseResponseResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = Convert(source.ReturnVal);
            return target;
        }

        #endregion

        #region Remoting.RemoteCrossDomainRequestRequest

        public static CREMOTING.RemoteCrossDomainRequestRequest Convert(BONDREMOTING.RemoteCrossDomainRequestRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteCrossDomainRequestRequest target = new CREMOTING.RemoteCrossDomainRequestRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            target.TargetIntent = source.TargetIntent;
            target.TraceId = source.TraceId;
            target.ValidLogLevels = source.ValidLogLevels;
            return target;
        }

        public static BONDREMOTING.RemoteCrossDomainRequestRequest Convert(CREMOTING.RemoteCrossDomainRequestRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteCrossDomainRequestRequest target = new BONDREMOTING.RemoteCrossDomainRequestRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            target.TargetIntent = source.TargetIntent;
            target.TraceId = source.TraceId;
            target.ValidLogLevels = source.ValidLogLevels;
            return target;
        }

        #endregion

        #region Remoting.RemoteCrossDomainRequestDataResponse

        public static CREMOTING.RemoteProcedureResponse<CAPI.CrossDomainRequestData> Convert(BONDREMOTING.RemoteCrossDomainRequestDataResponse source)
        {
            if (source == null) return null;
            string methodName = source.MethodName;
            CAPI.CrossDomainRequestData data = Convert(source.ReturnVal);
            CREMOTING.RemoteProcedureResponse<CAPI.CrossDomainRequestData> target = new CREMOTING.RemoteProcedureResponse<CAPI.CrossDomainRequestData>(source.MethodName, data);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteCrossDomainRequestDataResponse Convert(CREMOTING.RemoteProcedureResponse<CAPI.CrossDomainRequestData> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteCrossDomainRequestDataResponse target = new BONDREMOTING.RemoteCrossDomainRequestDataResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = Convert(source.ReturnVal);
            return target;
        }

        #endregion

        #region Remoting.RemoteDeleteOAuthTokenRequest

        public static CREMOTING.RemoteDeleteOAuthTokenRequest Convert(BONDREMOTING.RemoteDeleteOAuthTokenRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteDeleteOAuthTokenRequest target = new CREMOTING.RemoteDeleteOAuthTokenRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.OAuthConfig = Convert(source.OAuthConfig);
            target.PluginId = Convert(source.PluginId);
            target.UserId = source.UserId;
            return target;
        }

        public static BONDREMOTING.RemoteDeleteOAuthTokenRequest Convert(CREMOTING.RemoteDeleteOAuthTokenRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteDeleteOAuthTokenRequest target = new BONDREMOTING.RemoteDeleteOAuthTokenRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.OAuthConfig = Convert(source.OAuthConfig);
            target.PluginId = Convert(source.PluginId);
            target.UserId = source.UserId;
            return target;
        }

        #endregion

        #region Remoting.RemoteListPluginStrongNameResponse

        public static CREMOTING.RemoteProcedureResponse<List<CAPI.PluginStrongName>> Convert(BONDREMOTING.RemoteListPluginStrongNameResponse source)
        {
            if (source == null) return null;
            string methodName = source.MethodName;
            List<CAPI.PluginStrongName> list = Convert(source.ReturnVal);
            CREMOTING.RemoteProcedureResponse<List<CAPI.PluginStrongName>> target = new CREMOTING.RemoteProcedureResponse<List<CAPI.PluginStrongName>>(source.MethodName, list);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteListPluginStrongNameResponse Convert(CREMOTING.RemoteProcedureResponse<List<CAPI.PluginStrongName>> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteListPluginStrongNameResponse target = new BONDREMOTING.RemoteListPluginStrongNameResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = Convert(source.ReturnVal);
            return target;
        }

        #endregion

        #region Remoting.RemoteException

        public static CREMOTING.RemoteException Convert(BONDREMOTING.RemoteException source)
        {
            if (source == null) return null;
            CREMOTING.RemoteException target = new CREMOTING.RemoteException();
            target.ExceptionType = source.ExceptionType;
            target.Message = source.Message;
            target.StackTrace = source.StackTrace;
            return target;
        }

        public static BONDREMOTING.RemoteException Convert(CREMOTING.RemoteException source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteException target = new BONDREMOTING.RemoteException();
            target.ExceptionType = source.ExceptionType;
            target.Message = source.Message;
            target.StackTrace = source.StackTrace;
            return target;
        }

        #endregion

        #region Remoting.RemoteExecutePluginRequest

        public static CREMOTING.RemoteExecutePluginRequest Convert(BONDREMOTING.RemoteExecutePluginRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteExecutePluginRequest target = new CREMOTING.RemoteExecutePluginRequest();
            
            target.EntryPoint = source.EntryPoint;
            target.GlobalUserProfile = Convert(source.GlobalUserProfile);
            target.IsRetry = source.IsRetry;
            target.LocalUserProfile = Convert(source.LocalUserProfile);
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            target.Query = Convert(source.Query);
            target.TraceId = source.TraceId;
            target.ValidLogLevels = source.ValidLogLevels;
            target.EntityContext = ConvertKnowledgeContext(source.EntityContext);
            target.EntityHistory = ConvertEntityHistory(source.EntityHistory);
            target.SessionStore = Convert(source.SessionStore);
            target.GlobalUserProfileIsWritable = source.GlobalUserProfileIsWritable;
            if (target.EntityContext != null)
            {
                target.ContextualEntities = Convert(source.ContextualEntities, target.EntityContext);
            }

            return target;
        }

        public static BONDREMOTING.RemoteExecutePluginRequest Convert(CREMOTING.RemoteExecutePluginRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteExecutePluginRequest target = new BONDREMOTING.RemoteExecutePluginRequest();
            target.ContextualEntities = Convert(source.ContextualEntities);
            target.EntryPoint = source.EntryPoint;
            target.GlobalUserProfile = Convert(source.GlobalUserProfile);
            target.IsRetry = source.IsRetry;
            target.LocalUserProfile = Convert(source.LocalUserProfile);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            target.Query = Convert(source.Query);
            target.TraceId = source.TraceId;
            target.ValidLogLevels = source.ValidLogLevels;
            target.EntityContext = ConvertKnowledgeContext(source.EntityContext);
            target.EntityHistory = ConvertEntityHistory(source.EntityHistory);
            target.SessionStore = Convert(source.SessionStore);
            target.GlobalUserProfileIsWritable = source.GlobalUserProfileIsWritable;
            return target;
        }

        #endregion

        #region Remoting.RemoteFetchPluginViewDataRequest

        public static CREMOTING.RemoteFetchPluginViewDataRequest Convert(BONDREMOTING.RemoteFetchPluginViewDataRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFetchPluginViewDataRequest target = new CREMOTING.RemoteFetchPluginViewDataRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            target.FilePath = source.FilePath;
            if (source.IfModifiedSinceUtcTicks.HasValue)
            {
                target.IfModifiedSince = new DateTimeOffset(source.IfModifiedSinceUtcTicks.Value, TimeSpan.Zero);
            }

            return target;
        }

        public static BONDREMOTING.RemoteFetchPluginViewDataRequest Convert(CREMOTING.RemoteFetchPluginViewDataRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFetchPluginViewDataRequest target = new BONDREMOTING.RemoteFetchPluginViewDataRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            target.FilePath = source.FilePath;
            if (source.IfModifiedSince.HasValue)
            {
                target.IfModifiedSinceUtcTicks = source.IfModifiedSince.Value.UtcTicks;
            }

            return target;
        }

        #endregion

        #region Remoting.RemoteHttpRequest

        public static CREMOTING.RemoteHttpRequest Convert(BONDREMOTING.RemoteHttpRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteHttpRequest target = new CREMOTING.RemoteHttpRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.TargetHost = source.TargetHost;
            target.TargetPort = source.TargetPort;
            target.UseSSL = source.UseSSL;
            target.WireRequest = source.WireRequest;
            return target;
        }

        public static BONDREMOTING.RemoteHttpRequest Convert(CREMOTING.RemoteHttpRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteHttpRequest target = new BONDREMOTING.RemoteHttpRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.TargetHost = source.TargetHost;
            target.TargetPort = source.TargetPort;
            target.UseSSL = source.UseSSL;
            target.WireRequest = source.WireRequest;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileCreateDirectoryRequest

        public static CREMOTING.RemoteFileCreateDirectoryRequest Convert(BONDREMOTING.RemoteFileCreateDirectoryRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileCreateDirectoryRequest target = new CREMOTING.RemoteFileCreateDirectoryRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.DirectoryPath = source.DirectoryPath;
            return target;
        }

        public static BONDREMOTING.RemoteFileCreateDirectoryRequest Convert(CREMOTING.RemoteFileCreateDirectoryRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileCreateDirectoryRequest target = new BONDREMOTING.RemoteFileCreateDirectoryRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.DirectoryPath = source.DirectoryPath;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileDeleteRequest

        public static CREMOTING.RemoteFileDeleteRequest Convert(BONDREMOTING.RemoteFileDeleteRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileDeleteRequest target = new CREMOTING.RemoteFileDeleteRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.TargetPath = source.TargetPath;
            return target;
        }

        public static BONDREMOTING.RemoteFileDeleteRequest Convert(CREMOTING.RemoteFileDeleteRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileDeleteRequest target = new BONDREMOTING.RemoteFileDeleteRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.TargetPath = source.TargetPath;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileListRequest

        public static CREMOTING.RemoteFileListRequest Convert(BONDREMOTING.RemoteFileListRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileListRequest target = new CREMOTING.RemoteFileListRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.SourcePath = source.SourcePath;
            target.ListDirectories = source.ListDirectories;
            return target;
        }

        public static BONDREMOTING.RemoteFileListRequest Convert(CREMOTING.RemoteFileListRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileListRequest target = new BONDREMOTING.RemoteFileListRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.SourcePath = source.SourcePath;
            target.ListDirectories = source.ListDirectories;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileMoveRequest

        public static CREMOTING.RemoteFileMoveRequest Convert(BONDREMOTING.RemoteFileMoveRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileMoveRequest target = new CREMOTING.RemoteFileMoveRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.SourcePath = source.SourcePath;
            target.TargetPath = source.TargetPath;
            return target;
        }

        public static BONDREMOTING.RemoteFileMoveRequest Convert(CREMOTING.RemoteFileMoveRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileMoveRequest target = new BONDREMOTING.RemoteFileMoveRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.SourcePath = source.SourcePath;
            target.TargetPath = source.TargetPath;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileReadContentsRequest

        public static CREMOTING.RemoteFileReadContentsRequest Convert(BONDREMOTING.RemoteFileReadContentsRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileReadContentsRequest target = new CREMOTING.RemoteFileReadContentsRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.FilePath = source.FilePath;
            return target;
        }

        public static BONDREMOTING.RemoteFileReadContentsRequest Convert(CREMOTING.RemoteFileReadContentsRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileReadContentsRequest target = new BONDREMOTING.RemoteFileReadContentsRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.FilePath = source.FilePath;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileStat

        public static CREMOTING.RemoteFileStat Convert(BONDREMOTING.RemoteFileStat source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileStat target = new CREMOTING.RemoteFileStat();
            target.Exists = source.Exists;
            target.IsDirectory = source.IsDirectory;
            if (source.CreationTime.HasValue) target.CreationTime = new DateTimeOffset(source.CreationTime.Value, TimeSpan.Zero);
            if (source.LastAccessTime.HasValue) target.LastAccessTime = new DateTimeOffset(source.LastAccessTime.Value, TimeSpan.Zero);
            if (source.LastWriteTime.HasValue) target.LastWriteTime = new DateTimeOffset(source.LastWriteTime.Value, TimeSpan.Zero);
            target.Size = source.Size;
            return target;
        }

        public static BONDREMOTING.RemoteFileStat Convert(CREMOTING.RemoteFileStat source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileStat target = new BONDREMOTING.RemoteFileStat();
            target.Exists = source.Exists;
            target.IsDirectory = source.IsDirectory;
            if (source.CreationTime.HasValue) target.CreationTime = source.CreationTime.Value.UtcTicks;
            if (source.LastAccessTime.HasValue) target.LastAccessTime = source.LastAccessTime.Value.UtcTicks;
            if (source.LastWriteTime.HasValue) target.LastWriteTime = source.LastWriteTime.Value.UtcTicks;
            target.Size = source.Size;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileStatRequest

        public static CREMOTING.RemoteFileStatRequest Convert(BONDREMOTING.RemoteFileStatRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileStatRequest target = new CREMOTING.RemoteFileStatRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.TargetPath = source.TargetPath;
            return target;
        }

        public static BONDREMOTING.RemoteFileStatRequest Convert(CREMOTING.RemoteFileStatRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileStatRequest target = new BONDREMOTING.RemoteFileStatRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.TargetPath = source.TargetPath;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileStreamAccessMode

        public static CREMOTING.RemoteFileStreamAccessMode Convert(BONDREMOTING.RemoteFileStreamAccessMode source)
        {
            return (CREMOTING.RemoteFileStreamAccessMode)source;
        }

        public static BONDREMOTING.RemoteFileStreamAccessMode Convert(CREMOTING.RemoteFileStreamAccessMode source)
        {
            return (BONDREMOTING.RemoteFileStreamAccessMode)source;
        }

        #endregion

        #region Remoting.RemoteFileStreamCloseRequest

        public static CREMOTING.RemoteFileStreamCloseRequest Convert(BONDREMOTING.RemoteFileStreamCloseRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileStreamCloseRequest target = new CREMOTING.RemoteFileStreamCloseRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.StreamId = source.StreamId;
            return target;
        }

        public static BONDREMOTING.RemoteFileStreamCloseRequest Convert(CREMOTING.RemoteFileStreamCloseRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileStreamCloseRequest target = new BONDREMOTING.RemoteFileStreamCloseRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.StreamId = source.StreamId;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileStreamOpenMode

        public static CREMOTING.RemoteFileStreamOpenMode Convert(BONDREMOTING.RemoteFileStreamOpenMode source)
        {
            return (CREMOTING.RemoteFileStreamOpenMode)source;
        }

        public static BONDREMOTING.RemoteFileStreamOpenMode Convert(CREMOTING.RemoteFileStreamOpenMode source)
        {
            return (BONDREMOTING.RemoteFileStreamOpenMode)source;
        }

        #endregion

        #region Remoting.RemoteFileStreamOpenRequest

        public static CREMOTING.RemoteFileStreamOpenRequest Convert(BONDREMOTING.RemoteFileStreamOpenRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileStreamOpenRequest target = new CREMOTING.RemoteFileStreamOpenRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.FilePath = source.FilePath;
            target.AccessMode = Convert(source.AccessMode);
            target.OpenMode = Convert(source.OpenMode);
            target.ShareMode = Convert(source.ShareMode);
            return target;
        }

        public static BONDREMOTING.RemoteFileStreamOpenRequest Convert(CREMOTING.RemoteFileStreamOpenRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileStreamOpenRequest target = new BONDREMOTING.RemoteFileStreamOpenRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.FilePath = source.FilePath;
            target.AccessMode = Convert(source.AccessMode);
            target.OpenMode = Convert(source.OpenMode);
            target.ShareMode = Convert(source.ShareMode);
            return target;
        }

        #endregion

        #region Remoting.RemoteFileStreamOpenResponse

        public static CREMOTING.RemoteProcedureResponse<CREMOTING.RemoteFileStreamOpenResult> Convert(BONDREMOTING.RemoteFileStreamOpenResponse source)
        {
            if (source == null) return null;
            string methodName = source.MethodName;
            CREMOTING.RemoteFileStreamOpenResult result = Convert(source.ReturnVal);
            CREMOTING.RemoteProcedureResponse<CREMOTING.RemoteFileStreamOpenResult> target = new CREMOTING.RemoteProcedureResponse<CREMOTING.RemoteFileStreamOpenResult>(source.MethodName, result);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteFileStreamOpenResponse Convert(CREMOTING.RemoteProcedureResponse<CREMOTING.RemoteFileStreamOpenResult> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileStreamOpenResponse target = new BONDREMOTING.RemoteFileStreamOpenResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = Convert(source.ReturnVal);
            return target;
        }

        #endregion

        #region Remoting.RemoteFileStreamOpenResult

        public static CREMOTING.RemoteFileStreamOpenResult Convert(BONDREMOTING.RemoteFileStreamOpenResult source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileStreamOpenResult target = new CREMOTING.RemoteFileStreamOpenResult();
            target.StreamId = source.StreamId;
            target.CanSeek = source.CanSeek;
            target.CanWrite = source.CanWrite;
            target.CanRead = source.CanRead;
            target.InitialFileLength = source.InitialFileLength;
            return target;
        }

        public static BONDREMOTING.RemoteFileStreamOpenResult Convert(CREMOTING.RemoteFileStreamOpenResult source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileStreamOpenResult target = new BONDREMOTING.RemoteFileStreamOpenResult();
            target.StreamId = source.StreamId;
            target.CanSeek = source.CanSeek;
            target.CanWrite = source.CanWrite;
            target.CanRead = source.CanRead;
            target.InitialFileLength = source.InitialFileLength;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileStreamReadRequest

        public static CREMOTING.RemoteFileStreamReadRequest Convert(BONDREMOTING.RemoteFileStreamReadRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileStreamReadRequest target = new CREMOTING.RemoteFileStreamReadRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.StreamId = source.StreamId;
            target.Position = source.Position;
            target.Length = source.Length;
            return target;
        }

        public static BONDREMOTING.RemoteFileStreamReadRequest Convert(CREMOTING.RemoteFileStreamReadRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileStreamReadRequest target = new BONDREMOTING.RemoteFileStreamReadRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.StreamId = source.StreamId;
            target.Position = source.Position;
            target.Length = source.Length;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileStreamSeekOrigin

        public static BONDREMOTING.RemoteFileStreamSeekOrigin Convert(CREMOTING.RemoteFileStreamSeekOrigin source)
        {
            switch (source)
            {
                case CREMOTING.RemoteFileStreamSeekOrigin.Unknown:
                default:
                    return BONDREMOTING.RemoteFileStreamSeekOrigin.Unknown;
                case CREMOTING.RemoteFileStreamSeekOrigin.Begin:
                    return BONDREMOTING.RemoteFileStreamSeekOrigin.Begin;
                case CREMOTING.RemoteFileStreamSeekOrigin.Current:
                    return BONDREMOTING.RemoteFileStreamSeekOrigin.Current;
                case CREMOTING.RemoteFileStreamSeekOrigin.End:
                    return BONDREMOTING.RemoteFileStreamSeekOrigin.End;
            }
        }

        public static CREMOTING.RemoteFileStreamSeekOrigin Convert(BONDREMOTING.RemoteFileStreamSeekOrigin source)
        {
            switch (source)
            {
                case BONDREMOTING.RemoteFileStreamSeekOrigin.Unknown:
                default:
                    return CREMOTING.RemoteFileStreamSeekOrigin.Unknown;
                case BONDREMOTING.RemoteFileStreamSeekOrigin.Begin:
                    return CREMOTING.RemoteFileStreamSeekOrigin.Begin;
                case BONDREMOTING.RemoteFileStreamSeekOrigin.Current:
                    return CREMOTING.RemoteFileStreamSeekOrigin.Current;
                case BONDREMOTING.RemoteFileStreamSeekOrigin.End:
                    return CREMOTING.RemoteFileStreamSeekOrigin.End;
            }
        }

        #endregion

        #region Remoting.RemoteFileStreamSeekRequest

        public static CREMOTING.RemoteFileStreamSeekRequest Convert(BONDREMOTING.RemoteFileStreamSeekRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileStreamSeekRequest target = new CREMOTING.RemoteFileStreamSeekRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.StreamId = source.StreamId;
            target.Offset = source.Offset;
            target.Origin = Convert(source.Origin);
            return target;
        }

        public static BONDREMOTING.RemoteFileStreamSeekRequest Convert(CREMOTING.RemoteFileStreamSeekRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileStreamSeekRequest target = new BONDREMOTING.RemoteFileStreamSeekRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.StreamId = source.StreamId;
            target.Offset = source.Offset;
            target.Origin = Convert(source.Origin);
            return target;
        }

        #endregion

        #region Remoting.RemoteFileStreamSetLengthRequest

        public static CREMOTING.RemoteFileStreamSetLengthRequest Convert(BONDREMOTING.RemoteFileStreamSetLengthRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileStreamSetLengthRequest target = new CREMOTING.RemoteFileStreamSetLengthRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.StreamId = source.StreamId;
            target.NewLength = source.NewLength;
            return target;
        }

        public static BONDREMOTING.RemoteFileStreamSetLengthRequest Convert(CREMOTING.RemoteFileStreamSetLengthRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileStreamSetLengthRequest target = new BONDREMOTING.RemoteFileStreamSetLengthRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.StreamId = source.StreamId;
            target.NewLength = source.NewLength;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileStreamShareMode

        public static CREMOTING.RemoteFileStreamShareMode Convert(BONDREMOTING.RemoteFileStreamShareMode source)
        {
            return (CREMOTING.RemoteFileStreamShareMode)source;
        }

        public static BONDREMOTING.RemoteFileStreamShareMode Convert(CREMOTING.RemoteFileStreamShareMode source)
        {
            return (BONDREMOTING.RemoteFileStreamShareMode)source;
        }

        #endregion

        #region Remoting.RemoteFileStreamWriteRequest

        public static CREMOTING.RemoteFileStreamWriteRequest Convert(BONDREMOTING.RemoteFileStreamWriteRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileStreamWriteRequest target = new CREMOTING.RemoteFileStreamWriteRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.StreamId = source.StreamId;
            target.Position = source.Position;
            target.Data = source.Data;
            return target;
        }

        public static BONDREMOTING.RemoteFileStreamWriteRequest Convert(CREMOTING.RemoteFileStreamWriteRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileStreamWriteRequest target = new BONDREMOTING.RemoteFileStreamWriteRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.StreamId = source.StreamId;
            target.Position = source.Position;
            target.Data = source.Data;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileWriteStatRequest

        public static CREMOTING.RemoteFileWriteStatRequest Convert(BONDREMOTING.RemoteFileWriteStatRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileWriteStatRequest target = new CREMOTING.RemoteFileWriteStatRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.TargetPath = source.TargetPath;
            if (source.NewCreationTime.HasValue) target.NewCreationTime = new DateTimeOffset(source.NewCreationTime.Value, TimeSpan.Zero);
            if (source.NewWriteTime.HasValue) target.NewModificationTime = new DateTimeOffset(source.NewWriteTime.Value, TimeSpan.Zero);
            return target;
        }

        public static BONDREMOTING.RemoteFileWriteStatRequest Convert(CREMOTING.RemoteFileWriteStatRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileWriteStatRequest target = new BONDREMOTING.RemoteFileWriteStatRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.TargetPath = source.TargetPath;
            if (source.NewCreationTime.HasValue) target.NewCreationTime = source.NewCreationTime.Value.UtcTicks;
            if (source.NewModificationTime.HasValue) target.NewWriteTime = source.NewModificationTime.Value.UtcTicks;
            return target;
        }

        #endregion

        #region Remoting.RemoteFileStatResponse

        public static CREMOTING.RemoteProcedureResponse<CREMOTING.RemoteFileStat> Convert(BONDREMOTING.RemoteFileStatResponse source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileStat convertedReturnVal = Convert(source.ReturnVal);
            CREMOTING.RemoteProcedureResponse<CREMOTING.RemoteFileStat> target = new CREMOTING.RemoteProcedureResponse<CREMOTING.RemoteFileStat>(source.MethodName, convertedReturnVal);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteFileStatResponse Convert(CREMOTING.RemoteProcedureResponse<CREMOTING.RemoteFileStat> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileStatResponse target = new BONDREMOTING.RemoteFileStatResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            if (source.ReturnVal != null)
            {
                target.ReturnVal = Convert(source.ReturnVal);
            }

            return target;
        }

        #endregion

        #region Remoting.RemoteFileWriteContentsRequest

        public static CREMOTING.RemoteFileWriteContentsRequest Convert(BONDREMOTING.RemoteFileWriteContentsRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteFileWriteContentsRequest target = new CREMOTING.RemoteFileWriteContentsRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.FilePath = source.FilePath;
            target.NewContents = source.NewContents;
            return target;
        }

        public static BONDREMOTING.RemoteFileWriteContentsRequest Convert(CREMOTING.RemoteFileWriteContentsRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteFileWriteContentsRequest target = new BONDREMOTING.RemoteFileWriteContentsRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.FilePath = source.FilePath;
            target.NewContents = source.NewContents;
            return target;
        }

        #endregion

        #region Remoting.RemoteGetOAuthTokenRequest

        public static CREMOTING.RemoteGetOAuthTokenRequest Convert(BONDREMOTING.RemoteGetOAuthTokenRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteGetOAuthTokenRequest target = new CREMOTING.RemoteGetOAuthTokenRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.OAuthConfig = Convert(source.OAuthConfig);
            target.PluginId = Convert(source.PluginId);
            target.UserId = source.UserId;
            return target;
        }

        public static BONDREMOTING.RemoteGetOAuthTokenRequest Convert(CREMOTING.RemoteGetOAuthTokenRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteGetOAuthTokenRequest target = new BONDREMOTING.RemoteGetOAuthTokenRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.OAuthConfig = Convert(source.OAuthConfig);
            target.PluginId = Convert(source.PluginId);
            target.UserId = source.UserId;
            return target;
        }

        #endregion

        #region Remoting.RemoteDialogProcessingResponse

        public static CREMOTING.RemoteProcedureResponse<CAPI.DialogProcessingResponse> Convert(BONDREMOTING.RemoteDialogProcessingResponse source)
        {
            if (source == null) return null;
            CAPI.DialogProcessingResponse convertedProcessingResponse = Convert(source.ReturnVal);
            CREMOTING.RemoteProcedureResponse<CAPI.DialogProcessingResponse> target = new CREMOTING.RemoteProcedureResponse<CAPI.DialogProcessingResponse>(source.MethodName, convertedProcessingResponse);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteDialogProcessingResponse Convert(CREMOTING.RemoteProcedureResponse<CAPI.DialogProcessingResponse> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteDialogProcessingResponse target = new BONDREMOTING.RemoteDialogProcessingResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = Convert(source.ReturnVal);
            return target;
        }

        #endregion

        #region Remoting.RemoteInt64Response

        public static CREMOTING.RemoteProcedureResponse<long> Convert(BONDREMOTING.RemoteInt64Response source)
        {
            if (source == null) return null;
            CREMOTING.RemoteProcedureResponse<long> target = new CREMOTING.RemoteProcedureResponse<long>(source.MethodName, source.ReturnVal);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteInt64Response Convert(CREMOTING.RemoteProcedureResponse<long> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteInt64Response target = new BONDREMOTING.RemoteInt64Response();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = source.ReturnVal;

            return target;
        }

        #endregion

        #region Remoting.RemoteLoadPluginRequest

        public static CREMOTING.RemoteLoadPluginRequest Convert(BONDREMOTING.RemoteLoadPluginRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteLoadPluginRequest target = new CREMOTING.RemoteLoadPluginRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            return target;
        }

        public static BONDREMOTING.RemoteLoadPluginRequest Convert(CREMOTING.RemoteLoadPluginRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteLoadPluginRequest target = new BONDREMOTING.RemoteLoadPluginRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            return target;
        }

        #endregion

        #region Remoting.RemoteLoadPluginResponse

        public static CREMOTING.RemoteProcedureResponse<CAPI.LoadedPluginInformation> Convert(BONDREMOTING.RemoteLoadPluginResponse source)
        {
            if (source == null) return null;
            CAPI.LoadedPluginInformation convertedPluginInfo = Convert(source.Info);
            CREMOTING.RemoteProcedureResponse<CAPI.LoadedPluginInformation> target = new CREMOTING.RemoteProcedureResponse<CAPI.LoadedPluginInformation>(source.MethodName, convertedPluginInfo);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteLoadPluginResponse Convert(CREMOTING.RemoteProcedureResponse<CAPI.LoadedPluginInformation> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteLoadPluginResponse target = new BONDREMOTING.RemoteLoadPluginResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            if (source.ReturnVal != null)
            {
                target.Info = Convert(source.ReturnVal);
            }

            return target;
        }

        #endregion

        #region Remoting.RemoteLogMessageRequest

        public static CREMOTING.RemoteLogMessageRequest Convert(BONDREMOTING.RemoteLogMessageRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteLogMessageRequest target = new CREMOTING.RemoteLogMessageRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.LogEvents = Convert(source.LogEvents);
            return target;
        }

        public static BONDREMOTING.RemoteLogMessageRequest Convert(CREMOTING.RemoteLogMessageRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteLogMessageRequest target = new BONDREMOTING.RemoteLogMessageRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.LogEvents = Convert(source.LogEvents);
            return target;
        }

        #endregion

        #region Remoting.RemoteMessageType

        public static CREMOTING.RemoteMessageType Convert(BONDREMOTING.RemoteMessageType source)
        {
            return (CREMOTING.RemoteMessageType)source;
        }

        public static BONDREMOTING.RemoteMessageType Convert(CREMOTING.RemoteMessageType source)
        {
            return (BONDREMOTING.RemoteMessageType)source;
        }

        #endregion

        #region Remoting.RemoteOAuthTokenResponse

        public static CREMOTING.RemoteProcedureResponse<CAPI.OAuthToken> Convert(BONDREMOTING.RemoteOAuthTokenResponse source)
        {
            if (source == null) return null;
            string methodName = source.MethodName;
            CAPI.OAuthToken token = Convert(source.ReturnVal);
            CREMOTING.RemoteProcedureResponse<CAPI.OAuthToken> target = new CREMOTING.RemoteProcedureResponse<CAPI.OAuthToken>(source.MethodName, token);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteOAuthTokenResponse Convert(CREMOTING.RemoteProcedureResponse<CAPI.OAuthToken> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteOAuthTokenResponse target = new BONDREMOTING.RemoteOAuthTokenResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = Convert(source.ReturnVal);

            return target;
        }

        #endregion

        #region Remoting.RemoteRecognizeSpeechRequest

        public static CREMOTING.RemoteRecognizeSpeechRequest Convert(BONDREMOTING.RemoteRecognizeSpeechRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteRecognizeSpeechRequest target = new CREMOTING.RemoteRecognizeSpeechRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.Locale = LanguageCode.TryParse(source.Locale);
            target.Audio = Convert(source.Audio);
            return target;
        }

        public static BONDREMOTING.RemoteRecognizeSpeechRequest Convert(CREMOTING.RemoteRecognizeSpeechRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteRecognizeSpeechRequest target = new BONDREMOTING.RemoteRecognizeSpeechRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.Locale = source.Locale.ToBcp47Alpha2String();
            target.Audio = Convert(source.Audio);
            return target;
        }

        #endregion

        #region Remoting.RemoteResolveEntityRequest

        public static CREMOTING.RemoteResolveEntityRequest Convert(BONDREMOTING.RemoteResolveEntityRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteResolveEntityRequest target = new CREMOTING.RemoteResolveEntityRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.Input = Convert(source.Input);
            target.Locale = source.Locale;
            target.Possibilities = Convert(source.Possibilities);
            return target;
        }

        public static BONDREMOTING.RemoteResolveEntityRequest Convert(CREMOTING.RemoteResolveEntityRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteResolveEntityRequest target = new BONDREMOTING.RemoteResolveEntityRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.Input = Convert(source.Input);
            target.Locale = source.Locale;
            target.Possibilities = Convert(source.Possibilities);
            return target;
        }

        #endregion

        #region Remoting.RemoteResolveEntityResponse

        public static CREMOTING.RemoteResolveEntityResponse Convert(BONDREMOTING.RemoteResolveEntityResponse source)
        {
            if (source == null) return null;
            CREMOTING.RemoteResolveEntityResponse target = new CREMOTING.RemoteResolveEntityResponse();
            target.Hypotheses = Convert(source.Hypotheses);
            target.LogEvents = Convert(source.LogEvents);
            return target;
        }

        public static BONDREMOTING.RemoteResolveEntityResponse Convert(CREMOTING.RemoteResolveEntityResponse source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteResolveEntityResponse target = new BONDREMOTING.RemoteResolveEntityResponse();
            target.Hypotheses = Convert(source.Hypotheses);
            target.LogEvents = Convert(source.LogEvents);
            return target;
        }

        #endregion

        #region Remoting.RemoteResolveEntityResponseResponse

        public static CREMOTING.RemoteProcedureResponse<CREMOTING.RemoteResolveEntityResponse> Convert(BONDREMOTING.RemoteResolveEntityResponseResponse source)
        {
            if (source == null) return null;
            string methodName = source.MethodName;
            CREMOTING.RemoteProcedureResponse<CREMOTING.RemoteResolveEntityResponse> target = new CREMOTING.RemoteProcedureResponse<CREMOTING.RemoteResolveEntityResponse>(source.MethodName, Convert(source.ReturnVal));
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteResolveEntityResponseResponse Convert(CREMOTING.RemoteProcedureResponse<CREMOTING.RemoteResolveEntityResponse> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteResolveEntityResponseResponse target = new BONDREMOTING.RemoteResolveEntityResponseResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = Convert(source.ReturnVal);

            return target;
        }

        #endregion

        #region Remoting.RemoteStringResponse

        public static CREMOTING.RemoteProcedureResponse<string> Convert(BONDREMOTING.RemoteStringResponse source)
        {
            if (source == null) return null;
            CREMOTING.RemoteProcedureResponse<string> target = new CREMOTING.RemoteProcedureResponse<string>(source.MethodName, source.ReturnVal);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteStringResponse Convert(CREMOTING.RemoteProcedureResponse<string> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteStringResponse target = new BONDREMOTING.RemoteStringResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = source.ReturnVal;

            return target;
        }

        #endregion

        #region Remoting.RemoteStringListResponse

        public static CREMOTING.RemoteProcedureResponse<List<string>> Convert(BONDREMOTING.RemoteStringListResponse source)
        {
            if (source == null) return null;
            string methodName = source.MethodName;
            CREMOTING.RemoteProcedureResponse<List<string>> target = new CREMOTING.RemoteProcedureResponse<List<string>>(source.MethodName, source.ReturnVal);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteStringListResponse Convert(CREMOTING.RemoteProcedureResponse<List<string>> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteStringListResponse target = new BONDREMOTING.RemoteStringListResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = source.ReturnVal;

            return target;
        }

        #endregion

        #region Remoting.RemoteSpeechRecognitionResultResponse

        public static CREMOTING.RemoteProcedureResponse<CAPI.SpeechRecognitionResult> Convert(BONDREMOTING.RemoteSpeechRecognitionResultResponse source)
        {
            if (source == null) return null;
            CAPI.SpeechRecognitionResult convertedReturnVal = Convert(source.ReturnVal);
            CREMOTING.RemoteProcedureResponse<CAPI.SpeechRecognitionResult> target = new CREMOTING.RemoteProcedureResponse<CAPI.SpeechRecognitionResult>(source.MethodName, convertedReturnVal);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteSpeechRecognitionResultResponse Convert(CREMOTING.RemoteProcedureResponse<CAPI.SpeechRecognitionResult> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteSpeechRecognitionResultResponse target = new BONDREMOTING.RemoteSpeechRecognitionResultResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            if (source.ReturnVal != null)
            {
                target.ReturnVal = Convert(source.ReturnVal);
            }

            return target;
        }

        #endregion

        #region Remoting.RemoteSynthesizeSpeechRequest

        public static CREMOTING.RemoteSynthesizeSpeechRequest Convert(BONDREMOTING.RemoteSynthesizeSpeechRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteSynthesizeSpeechRequest target = new CREMOTING.RemoteSynthesizeSpeechRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.SynthRequest = Convert(source.SynthRequest);
            return target;
        }

        public static BONDREMOTING.RemoteSynthesizeSpeechRequest Convert(CREMOTING.RemoteSynthesizeSpeechRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteSynthesizeSpeechRequest target = new BONDREMOTING.RemoteSynthesizeSpeechRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.SynthRequest = Convert(source.SynthRequest);
            return target;
        }

        #endregion

        #region Remoting.RemoteSynthesizedSpeechResponse

        public static CREMOTING.RemoteProcedureResponse<CAPI.SynthesizedSpeech> Convert(BONDREMOTING.RemoteSynthesizedSpeechResponse source)
        {
            if (source == null) return null;
            CAPI.SynthesizedSpeech convertedReturnVal = Convert(source.ReturnVal);
            CREMOTING.RemoteProcedureResponse<CAPI.SynthesizedSpeech> target = new CREMOTING.RemoteProcedureResponse<CAPI.SynthesizedSpeech>(source.MethodName, convertedReturnVal);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteSynthesizedSpeechResponse Convert(CREMOTING.RemoteProcedureResponse<CAPI.SynthesizedSpeech> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteSynthesizedSpeechResponse target = new BONDREMOTING.RemoteSynthesizedSpeechResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            if (source.ReturnVal != null)
            {
                target.ReturnVal = Convert(source.ReturnVal);
            }

            return target;
        }

        #endregion

        #region Remoting.RemoteTriggerPluginRequest

        public static CREMOTING.RemoteTriggerPluginRequest Convert(BONDREMOTING.RemoteTriggerPluginRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteTriggerPluginRequest target = new CREMOTING.RemoteTriggerPluginRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            target.Query = Convert(source.Query);
            target.TraceId = source.TraceId;
            target.ValidLogLevels = source.ValidLogLevels;
            return target;
        }

        public static BONDREMOTING.RemoteTriggerPluginRequest Convert(CREMOTING.RemoteTriggerPluginRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteTriggerPluginRequest target = new BONDREMOTING.RemoteTriggerPluginRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            target.Query = Convert(source.Query);
            target.TraceId = source.TraceId;
            target.ValidLogLevels = source.ValidLogLevels;
            return target;
        }

        #endregion

        #region Remoting.RemoteTriggerProcessingResponse

        public static CREMOTING.RemoteProcedureResponse<CAPI.TriggerProcessingResponse> Convert(BONDREMOTING.RemoteTriggerProcessingResponse source)
        {
            if (source == null) return null;
            CAPI.TriggerProcessingResponse convertedProcessingResponse = Convert(source.ReturnVal);
            CREMOTING.RemoteProcedureResponse<CAPI.TriggerProcessingResponse> target = new CREMOTING.RemoteProcedureResponse<CAPI.TriggerProcessingResponse>(source.MethodName, convertedProcessingResponse);
            target.Exception = Convert(source.Exception);
            return target;
        }

        public static BONDREMOTING.RemoteTriggerProcessingResponse Convert(CREMOTING.RemoteProcedureResponse<CAPI.TriggerProcessingResponse> source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteTriggerProcessingResponse target = new BONDREMOTING.RemoteTriggerProcessingResponse();
            target.Exception = Convert(source.Exception);
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.ReturnVal = Convert(source.ReturnVal);
            return target;
        }

        #endregion

        #region Remoting.RemoteUnloadPluginRequest

        public static CREMOTING.RemoteUnloadPluginRequest Convert(BONDREMOTING.RemoteUnloadPluginRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteUnloadPluginRequest target = new CREMOTING.RemoteUnloadPluginRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            return target;
        }

        public static BONDREMOTING.RemoteUnloadPluginRequest Convert(CREMOTING.RemoteUnloadPluginRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteUnloadPluginRequest target = new BONDREMOTING.RemoteUnloadPluginRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.PluginId = Convert(source.PluginId);
            return target;
        }

        #endregion

        #region Remoting.RemoteUploadMetricsRequest

        public static CREMOTING.RemoteUploadMetricsRequest Convert(BONDREMOTING.RemoteUploadMetricsRequest source)
        {
            if (source == null) return null;
            CREMOTING.RemoteUploadMetricsRequest target = new CREMOTING.RemoteUploadMetricsRequest();
            //target.MessageType = Convert(source.MessageType);
            //target.MethodName = source.MethodName;
            target.Metrics = Convert(source.Metrics);
            return target;
        }

        public static BONDREMOTING.RemoteUploadMetricsRequest Convert(CREMOTING.RemoteUploadMetricsRequest source)
        {
            if (source == null) return null;
            BONDREMOTING.RemoteUploadMetricsRequest target = new BONDREMOTING.RemoteUploadMetricsRequest();
            target.MessageType = Convert(source.MessageType);
            target.MethodName = source.MethodName;
            target.Metrics = Convert(source.Metrics);
            return target;
        }

        #endregion
    }
}
