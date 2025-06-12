using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class ps_search_t
    {
        public ps_searchfuncs_t vt;  /*< V-table of search methods. */

        public Pointer<byte> type;
        public Pointer<byte> name;

        public ps_search_t pls;      /*< Phoneme loop for lookahead. */
        public cmd_ln_t config;      /*< Configuration. */
        public acmod_t acmod;        /*< Acoustic model. */
        public dict_t dict;        /*< Pronunciation dictionary. */
        public dict2pid_t d2p;       /*< Dictionary to senone mappings. */
        public Pointer<byte> hyp_str;         /*< Current hypothesis string. */
        public int post;            /*< Utterance posterior probability. */
        public int n_words;         /*< Number of words known to search (may
                              be less than in the dictionary) */

        /* Magical word IDs that must exist in the dictionary: */
        public int start_wid;       /*< Start word ID. */
        public int silence_wid;     /*< Silence word ID. */
        public int finish_wid;      /*< Finish word ID. */
        public SphinxLogger logger;
    }
}
