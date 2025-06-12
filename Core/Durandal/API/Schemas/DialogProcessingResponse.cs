using Durandal.Common.Dialog.Services;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.API
{
    public class DialogProcessingResponse
    {
        public PluginResult PluginOutput { get; set; }
        public bool WasRetrying { get; set; }
        public InMemoryDataStore UpdatedSessionStore { get; set; }
        public InMemoryDataStore UpdatedLocalUserProfile { get; set; }
        public InMemoryDataStore UpdatedGlobalUserProfile { get; set; }
        public KnowledgeContext UpdatedEntityContext { get; set; }
        public InMemoryEntityHistory UpdatedEntityHistory { get; set; }
        public InMemoryDialogActionCache UpdatedDialogActions { get; set; }
        public InMemoryWebDataCache UpdatedWebDataCache { get; set; }

        public DialogProcessingResponse(
            PluginResult result,
            bool wasRetry)
        {
            PluginOutput = result;
            WasRetrying = wasRetry;
        }

        public void ApplyPluginServiceSideEffects(IPluginServicesInternal services)
        {
            if (services == null)
            {
                return;
            }

            if (services.SessionStore != null && services.SessionStore.Touched)
            {
                UpdatedSessionStore = services.SessionStore;
                UpdatedSessionStore.Touched = true;
            }
            if (services.LocalUserProfile != null && services.LocalUserProfile.Touched)
            {
                UpdatedLocalUserProfile = services.LocalUserProfile;
                UpdatedLocalUserProfile.Touched = true;
            }
            if (services.GlobalUserProfile != null && services.GlobalUserProfile.Touched)
            {
                UpdatedGlobalUserProfile = services.GlobalUserProfile;
                UpdatedGlobalUserProfile.Touched = true;
            }
            
            // OPT apply the same conditional here: only return a value if it was modified by the plugin
            UpdatedEntityContext = services.EntityContext;
            UpdatedEntityHistory = services.EntityHistory as InMemoryEntityHistory; // FIXME also we need a better handler for null references that may come out of this
            UpdatedDialogActions = services.DialogActionCache;
            UpdatedWebDataCache = services.WebDataCache;
        }
    }
}
