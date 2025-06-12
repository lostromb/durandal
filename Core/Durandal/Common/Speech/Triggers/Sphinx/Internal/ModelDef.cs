using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class ModelDef
    {
        internal static readonly Pointer<byte> MODEL_DEF_VERSION = cstring.ToCString("0.3");
        public const int N_WORD_POSN = 4;
        internal static readonly Pointer<byte> WPOS_NAME = cstring.ToCString("ibesu");
        internal static readonly Pointer<byte> S3_SILENCE_CIPHONE = cstring.ToCString("SIL");

        internal static void ciphone_add(mdef_t m, Pointer<byte> ci, int p)
        {
            SphinxAssert.assert(p < m.n_ciphone);

            m.ciphone[p].name = CKDAlloc.ckd_salloc(ci);       /* freed in mdef_free */
            if (HashTable.hash_table_enter(m.ciphone_ht, m.ciphone[p].name, p) != p)
                m.logger.E_FATAL(string.Format("hash_table_enter({0}) failed; duplicate CIphone?\n",
                    cstring.FromCString(m.ciphone[p].name)));
        }


        internal static ph_lc_t find_ph_lc(ph_lc_t lclist, int lc)
        {
            ph_lc_t lcptr;

            for (lcptr = lclist; lcptr != null && (lcptr.lc != lc); lcptr = lcptr.next) ;
            return lcptr;
        }


        internal static ph_rc_t find_ph_rc(ph_rc_t rclist, int rc)
        {
            ph_rc_t rcptr;

            for (rcptr = rclist; rcptr != null && (rcptr.rc != rc); rcptr = rcptr.next) ;
            return rcptr;
        }


        internal static void triphone_add(mdef_t m,
                short ci, short lc, short rc, int wpos,
                int p)
        {
            ph_lc_t lcptr;
            ph_rc_t rcptr;

            SphinxAssert.assert(p < m.n_phone);

            /* Fill in phone[p] information (state and tmat mappings added later) */
            m.phone[p].ci = ci;
            m.phone[p].lc = lc;
            m.phone[p].rc = rc;
            m.phone[p].wpos = wpos;

            /* Create <ci,lc,rc,wpos> .Deref. p mapping if not a CI phone */
            if (p >= m.n_ciphone)
            {
                if ((lcptr = find_ph_lc(m.wpos_ci_lclist[wpos][(int)ci], lc)) == null)
                {
                    lcptr = new ph_lc_t(); /* freed at mdef_free, I believe */
                    lcptr.lc = lc;
                    lcptr.next = m.wpos_ci_lclist[wpos][(int)ci];
                    Pointer<ph_lc_t> tmp = m.wpos_ci_lclist[wpos];
                    tmp[(int)ci] = lcptr;  /* This is what needs to be freed */
                }
                if ((rcptr = find_ph_rc(lcptr.rclist, rc)) != null)
                {
                    Pointer<byte> buf = PointerHelpers.Malloc<byte>(4096);
                    mdef_phone_str(m, rcptr.pid, buf);
                    m.logger.E_FATAL(string.Format("Duplicate triphone: {0}\n", cstring.FromCString(buf)));
                }

                rcptr = new ph_rc_t();     /* freed in mdef_free, I believe */
                rcptr.rc = rc;
                rcptr.pid = p;
                rcptr.next = lcptr.rclist;
                lcptr.rclist = rcptr;
            }
        }

        internal static int mdef_ciphone_id(mdef_t m, Pointer<byte> ci)
        {
            BoxedValueInt id = new BoxedValueInt();
            if (HashTable.hash_table_lookup_int32(m.ciphone_ht, ci, id) < 0)
                return -1;
            return id.Val;
        }

        internal static Pointer<byte> mdef_ciphone_str(mdef_t m, int id)
        {
            SphinxAssert.assert(m != null);
            SphinxAssert.assert((id >= 0) && (id < m.n_ciphone));

            return (m.ciphone[id].name);
        }

        internal static int mdef_phone_str(mdef_t m, int pid, Pointer<byte> buf)
        {
            Pointer<byte> wpos_name;

            SphinxAssert.assert(m != null);
            SphinxAssert.assert((pid >= 0) && (pid < m.n_phone));
            wpos_name = WPOS_NAME;

            buf[0] = (byte)'\0';
            if (pid < m.n_ciphone)
            {
                stdio.sprintf(buf, string.Format("{0}", cstring.FromCString(mdef_ciphone_str(m, pid))));
            }
            else
            {
                stdio.sprintf(buf, string.Format("{0} {1} {2} {3}",
                    cstring.FromCString(mdef_ciphone_str(m, m.phone[pid].ci)),
                    cstring.FromCString(mdef_ciphone_str(m, m.phone[pid].lc)),
                    cstring.FromCString(mdef_ciphone_str(m, m.phone[pid].rc)),
                    (char)wpos_name[m.phone[pid].wpos]));
            }
            return 0;
        }

        /* Parse tmat and state.Deref.senone mappings for phone p and fill in structure */
        internal static void parse_tmat_senmap(mdef_t m, Pointer<byte> line, long off, int p)
        {
            int wlen, n, s;
            Pointer<byte> lp;
            Pointer<byte> word = PointerHelpers.Malloc<byte>(1024);

            lp = line + (int)off;

            /* Read transition matrix id */
            if ((stdio.sscanf_d_n(lp, out n, out wlen) != 1) || (n < 0))
                m.logger.E_FATAL(string.Format("Missing or bad transition matrix id: {0}\n", cstring.FromCString(line)));
            m.phone[p].tmat = n;
            if (m.n_tmat <= n)
                m.logger.E_FATAL(string.Format("tmat-id({0}) > #tmat in header({1}): {2}\n", n, m.n_tmat,
                    cstring.FromCString(line)));
            lp += wlen;

            /* Read senone mappings for each emitting state */
            for (n = 0; n < m.n_emit_state; n++)
            {
                if ((stdio.sscanf_d_n(lp, out s, out wlen) != 1) || (s < 0))
                    m.logger.E_FATAL(string.Format("Missing or bad state[{0}].Deref.senone mapping: {1}\n", n,
                        cstring.FromCString(line)));

                if ((p < m.n_ciphone) && (m.n_ci_sen <= s))
                    m.logger.E_FATAL(string.Format("CI-senone-id({0}) > #CI-senones({1}): {2}\n", s,
                        m.n_ci_sen, cstring.FromCString(line)));
                if (m.n_sen <= s)
                    m.logger.E_FATAL(string.Format("Senone-id({0}) > #senones({1}): {2}\n", s, m.n_sen,
                        cstring.FromCString(line)));

                Pointer<ushort> tmp = m.sseq[p];
                tmp[n] = (ushort)s;
                lp += wlen;
            }

            /* Check for the last non-emitting state N */
            if ((stdio.sscanf_s_n(lp, word, out wlen) != 1) || (cstring.strcmp(word, cstring.ToCString("N")) != 0))
                m.logger.E_FATAL(string.Format("Missing non-emitting state spec: {0}\n", cstring.FromCString(line)));
            lp += wlen;

            /* Check for end of line */
            if (stdio.sscanf_s_n(lp, word, out wlen) == 1)
                m.logger.E_FATAL(string.Format("Non-empty beyond non-emitting final state: {0}\n", cstring.FromCString(line)));
        }


        internal static void parse_base_line(mdef_t m, Pointer<byte> line, int p)
        {
            int wlen, n;
            Pointer<byte> word = PointerHelpers.Malloc<byte>(1024);
            Pointer<byte> lp;
            int ci;

            lp = line;

            /* Read base phone name */
            if (stdio.sscanf_s_n(lp, word, out wlen) != 1)
                m.logger.E_FATAL(string.Format("Missing base phone name: {0}\n", cstring.FromCString(line)));
            lp += wlen;

            /* Make sure it's not a duplicate */
            ci = mdef_ciphone_id(m, word);
            if (ci >= 0)
                m.logger.E_FATAL(string.Format("Duplicate base phone: {0}\n", cstring.FromCString(line)));

            /* Add ciphone to ciphone table with id p */
            ciphone_add(m, word, p);
            ci = (int)p;

            /* Read and skip "-" for lc, rc, wpos */
            for (n = 0; n < 3; n++)
            {
                if ((stdio.sscanf_s_n(lp, word, out wlen) != 1)
                    || (cstring.strcmp(word, cstring.ToCString("-")) != 0))
                    m.logger.E_FATAL(string.Format("Bad context info for base phone: {0}\n", cstring.FromCString(line)));
                lp += wlen;
            }

            /* Read filler attribute, if present */
            if (stdio.sscanf_s_n(lp, word, out wlen) != 1)
                m.logger.E_FATAL(string.Format("Missing filler attribute field: {0}\n", cstring.FromCString(line)));
            lp += wlen;
            if (cstring.strcmp(word, cstring.ToCString("filler")) == 0)
                m.ciphone[(int)ci].filler = 1;
            else if (cstring.strcmp(word, cstring.ToCString("n/a")) == 0)
                m.ciphone[(int)ci].filler = 0;
            else
                m.logger.E_FATAL(string.Format("Bad filler attribute field: {0}\n", cstring.FromCString(line)));

            triphone_add(m, (short)ci, -1, -1, word_posn_t.WORD_POSN_UNDEFINED, p);

            /* Parse remainder of line: transition matrix and state.Deref.senone mappings */
            parse_tmat_senmap(m, line, lp - line, p);
        }


        internal static void parse_tri_line(mdef_t m, Pointer<byte> line, int p)
        {
            int wlen;
            Pointer<byte> word = PointerHelpers.Malloc<byte>(1024);
            Pointer<byte> lp;
            int ci, lc, rc;
            int wpos = word_posn_t.WORD_POSN_BEGIN;

            lp = line;

            /* Read base phone name */
            if (stdio.sscanf_s_n(lp, word, out wlen) != 1)
                m.logger.E_FATAL(string.Format("Missing base phone name: {0}\n", cstring.FromCString(line)));
            lp += wlen;

            ci = mdef_ciphone_id(m, word);
            if (ci < 0)
                m.logger.E_FATAL(string.Format("Unknown base phone: {0}\n", cstring.FromCString(line)));

            /* Read lc */
            if (stdio.sscanf_s_n(lp, word, out wlen) != 1)
                m.logger.E_FATAL(string.Format("Missing left context: {0}\n", cstring.FromCString(line)));
            lp += wlen;
            lc = mdef_ciphone_id(m, word);
            if (lc < 0)
                m.logger.E_FATAL(string.Format("Unknown left context: {0}\n", cstring.FromCString(line)));

            /* Read rc */
            if (stdio.sscanf_s_n(lp, word, out wlen) != 1)
                m.logger.E_FATAL(string.Format("Missing right context: {0}\n", cstring.FromCString(line)));
            lp += wlen;
            rc = mdef_ciphone_id(m, word);
            if (rc < 0)
                m.logger.E_FATAL(string.Format("Unknown right  context: {0}\n", cstring.FromCString(line)));

            /* Read tripone word-position within word */
            if ((stdio.sscanf_s_n(lp, word, out wlen) != 1) || (word[1] != '\0'))
                m.logger.E_FATAL(string.Format("Missing or bad word-position spec: {0}\n", cstring.FromCString(line)));
            lp += wlen;
            switch (word[0])
            {
                case (byte)'b':
                    wpos = word_posn_t.WORD_POSN_BEGIN;
                    break;
                case (byte)'e':
                    wpos = word_posn_t.WORD_POSN_END;
                    break;
                case (byte)'s':
                    wpos = word_posn_t.WORD_POSN_SINGLE;
                    break;
                case (byte)'i':
                    wpos = word_posn_t.WORD_POSN_INTERNAL;
                    break;
                default:
                    m.logger.E_FATAL(string.Format("Bad word-position spec: {0}\n", cstring.FromCString(line)));
                    break;
            }

            /* Read filler attribute, if present.  Must match base phone attribute */
            if (stdio.sscanf_s_n(lp, word, out wlen) != 1)
                m.logger.E_FATAL(string.Format("Missing filler attribute field: {0}\n", cstring.FromCString(line)));
            lp += wlen;
            if (((cstring.strcmp(word, cstring.ToCString("filler")) == 0) && (m.ciphone[(int)ci].filler != 0)) ||
                ((cstring.strcmp(word, cstring.ToCString("n/a")) == 0) && (m.ciphone[(int)ci].filler == 0)))
            {
                /* Everything is fine */
            }
            else
                m.logger.E_FATAL(string.Format("Bad filler attribute field: {0}\n", cstring.FromCString(line)));

            triphone_add(m, (short)ci, (short)lc, (short)rc, wpos, p);

            /* Parse remainder of line: transition matrix and state.Deref.senone mappings */
            parse_tmat_senmap(m, line, lp - line, p);
        }


        internal static void sseq_compress(mdef_t m)
        {
            hash_table_t h;
            Pointer<Pointer<ushort>> sseq;
            int n_sseq;
            int p;
            uint k;
            BoxedValueInt j = new BoxedValueInt();
            List<hash_entry_t> g;

            k = (uint)(m.n_emit_state);

            h = HashTable.hash_table_new(m.n_phone, HashTable.HASH_CASE_YES, m.logger);
            n_sseq = 0;

            /* Identify unique senone-sequence IDs.  BUG: tmat-id not being considered!! */
            for (p = 0; p < m.n_phone; p++)
            {
                /* Add senone sequence to hash table */
                Pointer<byte> bytePtr = m.sseq[p].ReinterpretCast<byte>();
                var returnVal = (j.Val = HashTable.hash_table_enter_bkey_int32(h, bytePtr, k * 2 /*sizeof(short)*/, n_sseq));
                if (n_sseq == (returnVal))
                    n_sseq++;

                m.phone[p].ssid = j.Val;
            }

            /* Generate compacted sseq table */
            sseq = CKDAlloc.ckd_calloc_2d<ushort>((uint)n_sseq, (uint)m.n_emit_state); /* freed in mdef_free() */

            g = HashTable.hash_table_tolist(h, j);
            SphinxAssert.assert(j.Val == n_sseq);

            foreach (hash_entry_t he in g)
            {
                j.Val = (int)HashTable.hash_entry_val(he);
                HashTable.hash_entry_key(he).ReinterpretCast<ushort>().MemCopyTo(sseq[j.Val], (int)k);
            }

            /* Free the old, temporary senone sequence table, replace with compacted one */
            m.sseq = sseq;
            m.n_sseq = n_sseq;
        }

        internal static int noncomment_line(Pointer<byte> line, int size, FILE fp)
        {
            while (fp.fgets(line, size).IsNonNull)
            {
                if (line[0] != '#')
                    return 0;
            }
            return -1;
        }

        /*
		* Initialize phones (ci and triphones) and state.Deref.senone mappings from .mdef file.
		*/
        internal static mdef_t mdef_init(Pointer<byte> mdeffile, FileAdapter fileAdapter, int breport, SphinxLogger logger)
        {
            FILE fp;
            int n_ci, n_tri, n_map, n;
            Pointer<byte> tag = PointerHelpers.Malloc<byte>(1024);
            Pointer<byte> buf = PointerHelpers.Malloc<byte>(1024);
            Pointer<Pointer<ushort>> senmap;
            int p;
            int s, ci, cd;
            mdef_t m;

            if (mdeffile.IsNull)
                logger.E_FATAL("No mdef-file\n");

            if (breport != 0)
                logger.E_INFO(string.Format("Reading model definition: {0}\n", cstring.FromCString(mdeffile)));

            m = new mdef_t();       /* freed in mdef_free */
            m.logger = logger;

            if ((fp = fileAdapter.fopen(mdeffile, "r")) == null)
                logger.E_FATAL_SYSTEM(string.Format("Failed to open mdef file '{0} for reading", cstring.FromCString(mdeffile)));

            if (noncomment_line(buf, 1024, fp) < 0)
                logger.E_FATAL(string.Format("Empty file: {0}\n", cstring.FromCString(mdeffile)));

            if (cstring.strncmp(buf, cstring.ToCString("BMDF"), 4) == 0)
            {
                logger.E_INFO("Found byte-order mark BMDF; assuming this is a binary mdef file\n");
                fp.fclose();
                return null;
            }
            if (cstring.strncmp(buf, cstring.ToCString("FDMB"), 4) == 0)
            {
                logger.E_INFO("Found byte-order mark FDMB; assuming this is a binary mdef file\n");
                fp.fclose();
                return null;
            }

            if (cstring.strncmp(buf, MODEL_DEF_VERSION, cstring.strlen(MODEL_DEF_VERSION)) != 0)
                logger.E_FATAL(string.Format("Version error: Expecing {0}, but read {1}\n",
                    cstring.FromCString(MODEL_DEF_VERSION), cstring.FromCString(buf)));

            /* Read #base phones, #triphones, #senone mappings defined in header */
            n_ci = -1;
            n_tri = -1;
            n_map = -1;
            m.n_ci_sen = -1;
            m.n_sen = -1;
            m.n_tmat = -1;
            do
            {
                if (noncomment_line(buf, 1024, fp) < 0)
                    logger.E_FATAL("Incomplete header\n");

                if ((stdio.sscanf_d_s(buf, out n, tag) != 2) || (n < 0))
                    logger.E_FATAL(string.Format("Error in header: {0}\n", cstring.FromCString(buf)));

                if (cstring.strcmp(tag, cstring.ToCString("n_base")) == 0)
                    n_ci = n;
                else if (cstring.strcmp(tag, cstring.ToCString("n_tri")) == 0)
                    n_tri = n;
                else if (cstring.strcmp(tag, cstring.ToCString("n_state_map")) == 0)
                    n_map = n;
                else if (cstring.strcmp(tag, cstring.ToCString("n_tied_ci_state")) == 0)
                    m.n_ci_sen = n;
                else if (cstring.strcmp(tag, cstring.ToCString("n_tied_state")) == 0)
                    m.n_sen = n;
                else if (cstring.strcmp(tag, cstring.ToCString("n_tied_tmat")) == 0)
                    m.n_tmat = n;
                else
                    logger.E_FATAL(string.Format("Unknown header line: {0}\n", cstring.FromCString(buf)));
            } while ((n_ci < 0) || (n_tri < 0) || (n_map < 0) ||
                (m.n_ci_sen < 0) || (m.n_sen < 0) || (m.n_tmat < 0));

            if ((n_ci == 0) || (m.n_ci_sen == 0) || (m.n_tmat == 0)
                || (m.n_ci_sen > m.n_sen))
                logger.E_FATAL(string.Format("{0}: Error in header\n", cstring.FromCString(mdeffile)));

            /* Check typesize limits */
            if (n_ci >= short.MaxValue)
                logger.E_FATAL(string.Format("{0}: #CI phones ({1}) exceeds limit ({2})\n", cstring.FromCString(mdeffile), n_ci,
                    short.MaxValue));
            if (n_ci + n_tri >= int.MaxValue) /* Comparison is always false... */
                logger.E_FATAL(string.Format("{0}: #Phones ({1}) exceeds limit ({2})\n", cstring.FromCString(mdeffile),
                    n_ci + n_tri, int.MaxValue));
            if (m.n_sen >= short.MaxValue)
                logger.E_FATAL(string.Format("{0}: #senones ({1}) exceeds limit ({2})\n", cstring.FromCString(mdeffile),
                    m.n_sen, short.MaxValue));
            if (m.n_tmat >= int.MaxValue) /* Comparison is always false... */
                logger.E_FATAL(string.Format("{0}: #tmats ({1}) exceeds limit ({2})\n", cstring.FromCString(mdeffile),
                    m.n_tmat, int.MaxValue));

            m.n_emit_state = (n_map / (n_ci + n_tri)) - 1;
            if ((m.n_emit_state + 1) * (n_ci + n_tri) != n_map)
                logger.E_FATAL
                ("Header error: n_state_map not a multiple of n_ci*n_tri\n");

            /* Initialize ciphone info */
            m.n_ciphone = n_ci;
            m.ciphone_ht = HashTable.hash_table_new(n_ci, HashTable.HASH_CASE_YES, logger);  /* With case-insensitive string names *//* freed in mdef_free */
            m.ciphone = CKDAlloc.ckd_calloc_struct<ciphone_t>(n_ci);     /* freed in mdef_free */

            /* Initialize phones info (ciphones + triphones) */
            m.n_phone = n_ci + n_tri;
            m.phone = CKDAlloc.ckd_calloc_struct<phone_t>(m.n_phone);     /* freed in mdef_free */

            /* Allocate space for state.Deref.senone map for each phone */
            senmap = CKDAlloc.ckd_calloc_2d<ushort>((uint)m.n_phone, (uint)m.n_emit_state);      /* freed in mdef_free */
            m.sseq = senmap;           /* TEMPORARY; until it is compressed into just the unique ones */

            /* Allocate initial space for <ci,lc,rc,wpos> -> pid mapping */
            m.wpos_ci_lclist = CKDAlloc.ckd_calloc_struct_2d<ph_lc_t>((uint)N_WORD_POSN, (uint)m.n_ciphone);      /* freed in mdef_free */ // LOGAN TODO: Are the objects initialized here just overwritten later? The original code allocated an array of null pointers, I believe

            /*
            * Read base phones and triphones.  They'll simply be assigned a running sequence
            * number as their "phone-id".  If the phone-id < n_ci, it's a ciphone.
            */

            /* Read base phones */
            for (p = 0; p < n_ci; p++)
            {
                if (noncomment_line(buf, 1024, fp) < 0)
                    logger.E_FATAL(string.Format("Premature EOF reading CIphone {0}\n", p));
                parse_base_line(m, buf, p);
            }
            m.sil = (short)mdef_ciphone_id(m, S3_SILENCE_CIPHONE);

            /* Read triphones, if any */
            for (; p < m.n_phone; p++)
            {
                if (noncomment_line(buf, 1024, fp) < 0)
                    logger.E_FATAL(string.Format("Premature EOF reading phone {0}\n", p));
                parse_tri_line(m, buf, p);
            }

            if (noncomment_line(buf, 1024, fp) >= 0)
                logger.E_ERROR(string.Format("Non-empty file beyond expected #phones ({0})\n",
                    m.n_phone));

            /* Build CD senones to CI senones map */
            if (m.n_ciphone * m.n_emit_state != m.n_ci_sen)
                logger.E_FATAL
                (string.Format("#CI-senones({0}) != #CI-phone({1}) x #emitting-states({2})\n",
                    m.n_ci_sen, m.n_ciphone, m.n_emit_state));
            m.cd2cisen = CKDAlloc.ckd_calloc<short>(m.n_sen); /* freed in mdef_free */

            m.sen2cimap = CKDAlloc.ckd_calloc<short>(m.n_sen); /* freed in mdef_free */

            for (s = 0; s < m.n_sen; s++)
                m.sen2cimap[s] = -1;
            for (s = 0; s < m.n_ci_sen; s++)
            { /* CI senones */
                m.cd2cisen[s] = (short)s;
                m.sen2cimap[s] = (short)(s / m.n_emit_state);
            }
            for (p = n_ci; p < m.n_phone; p++)
            {       /* CD senones */
                for (s = 0; s < m.n_emit_state; s++)
                {
                    cd = m.sseq[p][s];
                    ci = m.sseq[m.phone[p].ci][s];
                    m.cd2cisen[cd] = (short)ci;
                    m.sen2cimap[cd] = m.phone[p].ci;
                }
            }

            sseq_compress(m);
            fp.fclose();

            return m;
        }
    };
}
