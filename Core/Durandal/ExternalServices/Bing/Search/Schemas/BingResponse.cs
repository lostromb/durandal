using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class BingResponse
    {
        public List<BingFactResponse> Facts { get; set; }

        public KnowledgeContext KnowledgeContext { get; set; }
        public List<string> EntityReferences { get; set; }
        public ComputationResult Computation { get; set; }
        public CurrencyResult Currency { get; set; }

        public BingResponse()
        {
            Facts = new List<BingFactResponse>();
            EntityReferences = new List<string>();
        }
    }
}
