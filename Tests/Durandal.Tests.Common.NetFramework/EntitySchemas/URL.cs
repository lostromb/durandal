using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Ontology;

namespace Durandal.Tests.EntitySchemas
{
    /// <summary>
    /// <para>URL</para>
    /// <para>Data type: URL.</para>
    /// </summary>
    internal class URL : Entity
    {
        private static HashSet<string> _inheritance = new HashSet<string>() { "http://schema.org/Text" };

        public URL(KnowledgeContext context, string entityId = null) : base(context, "http://schema.org/URL", entityId) { Initialize(); }

        /// <summary>
        /// Casting constructor
        /// </summary>
        /// <param name="castFrom">The entity that this one is being cast from</param>
        public URL(Entity castFrom) : base(castFrom, "http://schema.org/URL") { Initialize(); }

        protected override ISet<string> InheritsFromInternal
        {
            get
            {
                return _inheritance;
            }
        }

        private void Initialize()
        {
        }

    }
}
