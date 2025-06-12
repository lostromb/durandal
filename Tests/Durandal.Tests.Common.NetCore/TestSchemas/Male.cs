using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Ontology;
using Durandal.Tests.TestSchemas;

namespace Durandal.Tests.TestSchemas
{
    /// <summary>
    /// <para>Male</para>
    /// <para>The male gender.</para>
    /// </summary>
    public class Male : Durandal.Tests.TestSchemas.GenderType
    {
        public Male(KnowledgeContext context, string entityId = null) : base(context, "enum://http://schema.org/Male") { }
    }
}
