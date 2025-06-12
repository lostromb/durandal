using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class hash_iter_t
    {
        public hash_table_t ht;  /*< Hash table we are iterating over. */
        public hash_entry_t ent; /*< Current entry in that table. */
        public uint idx;        /*< Index of next bucket to search. */
    };
}
