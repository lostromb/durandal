using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteExecutePluginRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "ExecutePlugin";

        public override string MethodName => METHOD_NAME;

        public PluginStrongName PluginId { get; set; }
        public string EntryPoint { get; set; }
        public bool IsRetry { get; set; }
        public QueryWithContext Query { get; set; }
        public InMemoryDataStore SessionStore { get; set; }
        public InMemoryDataStore LocalUserProfile { get; set; }
        public InMemoryDataStore GlobalUserProfile { get; set; }
        public InMemoryEntityHistory EntityHistory { get; set; }
        public KnowledgeContext EntityContext { get; set; }
        public IList<ContextualEntity> ContextualEntities { get; set; }
        public string TraceId { get; set; }
        public int ValidLogLevels { get; set; }
        public bool GlobalUserProfileIsWritable { get; set; }
    }
}
