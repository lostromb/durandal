using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public class CrossDomainResponseResponse
    {
        public CrossDomainResponseData PluginResponse { get; set; }
        public KnowledgeContext OutEntityContext { get; set; }
    }
}
