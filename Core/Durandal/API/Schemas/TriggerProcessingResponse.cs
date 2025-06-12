using Durandal.Common.Dialog.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public class TriggerProcessingResponse
    {
        public TriggerResult PluginOutput { get; set; }
        public InMemoryDataStore UpdatedSessionStore { get; set; }

        public TriggerProcessingResponse(TriggerResult result, InMemoryDataStore updatedSessionStore)
        {
            PluginOutput = result;
            UpdatedSessionStore = updatedSessionStore;
        }
    }
}
