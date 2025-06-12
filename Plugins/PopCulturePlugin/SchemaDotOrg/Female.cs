using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Ontology;

namespace Durandal.Plugins.PopCulture.SchemaDotOrg
{
    /// <summary>
    /// <para>Female</para>
    /// <para>The female gender.</para>
    /// </summary>
    internal class Female : Durandal.Plugins.PopCulture.SchemaDotOrg.GenderType
    {
        public Female(KnowledgeContext context, string entityId = null) : base(context, "enum://http://schema.org/Female") { }
    }
}
