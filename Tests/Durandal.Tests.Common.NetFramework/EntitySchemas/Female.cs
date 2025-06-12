using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Ontology;
using Durandal.Tests.EntitySchemas;

namespace Durandal.Tests.EntitySchemas
{
    /// <summary>
    /// <para>Female</para>
    /// <para>The female gender.</para>
    /// </summary>
    internal class Female : Durandal.Tests.EntitySchemas.GenderType
    {
        public Female(KnowledgeContext context, string entityId = null) : base(context, "enum://http://schema.org/Female") { }
    }
}
