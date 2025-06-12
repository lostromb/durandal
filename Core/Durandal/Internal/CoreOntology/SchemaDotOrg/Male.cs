using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Ontology;

namespace Durandal.Internal.CoreOntology.SchemaDotOrg
{
    /// <summary>
    /// <para>Male</para>
    /// <para>The male gender.</para>
    /// </summary>
    internal class Male : Durandal.Internal.CoreOntology.SchemaDotOrg.GenderType
    {
        public Male(KnowledgeContext context, string entityId = null) : base(context, "enum://http://schema.org/Male") { }
    }
}
