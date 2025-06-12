using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class Dictionary
    {
        internal static readonly Pointer<byte> DELIM = cstring.ToCString(" \t\n");
        internal static readonly Pointer<byte> S3_START_WORD = cstring.ToCString("<s>");
        internal static readonly Pointer<byte> S3_FINISH_WORD = cstring.ToCString("</s>");
        internal static readonly Pointer<byte> S3_SILENCE_WORD = cstring.ToCString("<sil>");
        internal static readonly Pointer<byte> S3_UNKNOWN_WORD = cstring.ToCString("<UNK>");
        internal static readonly Pointer<byte> HASHES = cstring.ToCString("##");
        internal static readonly Pointer<byte> SEMICOLONS = cstring.ToCString(";;");

        public const int DEFAULT_NUM_PHONE = SphinxTypes.MAX_S3CIPID + 1;
        public const int S3DICT_INC_SZ = 4096;

        internal static int dict_size(dict_t d)
        {
            return d.n_word;
        }

        internal static int dict_num_fillers(dict_t d)
        {
            return dict_filler_end(d) - dict_filler_start(d);
        }

        internal static int dict_num_real_words(dict_t d)
        {
            return (dict_size(d) - (dict_filler_end(d) - dict_filler_start(d)) - 2);
        }

        internal static int dict_basewid(dict_t d, int w)
        {
            return (d.word[w].basewid);
        }

        internal static Pointer<byte> dict_wordstr(dict_t d, int w)
        {
            return ((w) < 0 ? PointerHelpers.NULL<byte>() : d.word[w].word);
        }

        internal static Pointer<byte> dict_basestr(dict_t d, int w)
        {
            return (d.word[dict_basewid(d, w)].word);
        }

        internal static int dict_nextalt(dict_t d, int w)
        {
            return (d.word[w].alt);
        }

        internal static int dict_pronlen(dict_t d, int w)
        {
            return (d.word[w].pronlen);
        }

        internal static short dict_pron(dict_t d, int w, int p)
        {
            return (d.word[w].ciphone[p]);
        }

        internal static int dict_filler_start(dict_t d)
        {
            return (d.filler_start);
        }

        internal static int dict_filler_end(dict_t d)
        {
            return (d.filler_end);
        }

        internal static int dict_startwid(dict_t d)
        {
            return (d.startwid);
        }

        internal static int dict_finishwid(dict_t d)
        {
            return (d.finishwid);
        }

        internal static int dict_silwid(dict_t d)
        {
            return (d.silwid);
        }

        internal static int dict_is_single_phone(dict_t d, int w)
        {
            return (d.word[w].pronlen == 1) ? 1 : 0;
        }

        internal static short dict_first_phone(dict_t d, int w)
        {
            return (d.word[w].ciphone[0]);
        }

        internal static short dict_second_phone(dict_t d, int w)
        {
            return (d.word[w].ciphone[1]);
        }

        internal static short dict_second_last_phone(dict_t d, int w)
        {
            return (d.word[w].ciphone[d.word[w].pronlen - 2]);
        }

        internal static short dict_last_phone(dict_t d, int w)
        {
            return (d.word[w].ciphone[d.word[w].pronlen - 1]);
        }

        internal static short dict_ciphone_id(dict_t d, Pointer<byte> str)
        {
            if (d.nocase != 0)
                return checked((short)BinaryModelDef.bin_mdef_ciphone_id_nocase(d.mdef, str));
            else
                return checked((short)BinaryModelDef.bin_mdef_ciphone_id(d.mdef, str));
        }

        internal static int dict_add_word(dict_t d, Pointer<byte> word, Pointer<short> p, int np)
        {
            int len;
            Pointer<dictword_t> wordp;
            int newwid;
            Pointer<byte> wword;

            if (d.n_word >= d.max_words)
            {
                d.logger.E_INFO(string.Format("Reallocating to {0} KiB for word entries\n",
                       (d.max_words + S3DICT_INC_SZ) * 28 / 1024));
                d.word = CKDAlloc.ckd_realloc(d.word, (d.max_words + S3DICT_INC_SZ));
                d.max_words = d.max_words + S3DICT_INC_SZ;
            }

            wordp = d.word + d.n_word;
            wordp.Deref.word = (Pointer<byte>)CKDAlloc.ckd_salloc(word);    /* Freed in dict_free */

            /* Determine base/alt wids */
            wword = CKDAlloc.ckd_salloc(word);
            if ((len = dict_word2basestr(wword)) > 0)
            {
                BoxedValueInt w = new BoxedValueInt();

                /* Truncated to a baseword string; find its ID */
                if (HashTable.hash_table_lookup_int32(d.ht, wword, w) < 0)
                {
                    d.logger.E_ERROR(string.Format("Missing base word for: {0}\n", cstring.FromCString(word)));
                    wordp.Deref.word = PointerHelpers.NULL<byte>();
                    return SphinxTypes.BAD_S3WID;
                }

                /* Link into alt list */
                wordp.Deref.basewid = w.Val;
                wordp.Deref.alt = d.word[w.Val].alt;
                d.word[w.Val].alt = d.n_word;
            }
            else
            {
                wordp.Deref.alt = SphinxTypes.BAD_S3WID;
                wordp.Deref.basewid = d.n_word;
            }

            /* Associate word string with d.n_word in hash table */
            if (HashTable.hash_table_enter_int32(d.ht, wordp.Deref.word, d.n_word) != d.n_word)
            {
                wordp.Deref.word = PointerHelpers.NULL<byte>();
                return SphinxTypes.BAD_S3WID;
            }

            /* Fill in word entry, and set defaults */
            if (p.IsNonNull && (np > 0))
            {
                wordp.Deref.ciphone = CKDAlloc.ckd_malloc<short>(np);      /* Freed in dict_free */
                p.MemCopyTo(wordp.Deref.ciphone, np);
                wordp.Deref.pronlen = np;
            }
            else
            {
                wordp.Deref.ciphone = PointerHelpers.NULL<short>();
                wordp.Deref.pronlen = 0;
            }

            newwid = d.n_word++;

            return newwid;
        }

        internal static int dict_read(FILE fp, dict_t d)
        {
            lineiter_t li;
            Pointer<Pointer<byte>> wptr;
            Pointer<short> p;
            int lineno, nwd;
            int w;
            int i, maxwd;
            uint stralloc, phnalloc;

            maxwd = 512;
            p = CKDAlloc.ckd_calloc<short>(maxwd + 4);
            wptr = CKDAlloc.ckd_calloc<Pointer<byte>>(maxwd); /* Freed below */

            lineno = 0;
            stralloc = phnalloc = 0;
            for (li = PackagedIO.lineiter_start(fp); li != null; li = PackagedIO.lineiter_next(li))
            {
                lineno++;
                if (0 == cstring.strncmp(li.buf, HASHES, 2)
                    || 0 == cstring.strncmp(li.buf, SEMICOLONS, 2))
                    continue;

                if ((nwd = StringFuncs.str2words(li.buf, wptr, maxwd)) < 0)
                {
                    /* Increase size of p, wptr. */
                    nwd = StringFuncs.str2words(li.buf, PointerHelpers.NULL<Pointer<byte>>(), 0);
                    SphinxAssert.assert(nwd > maxwd); /* why else would it fail? */
                    maxwd = nwd;
                    p = CKDAlloc.ckd_realloc(p, (uint)(maxwd + 4));
                    wptr = (Pointer<Pointer<byte>>)CKDAlloc.ckd_realloc(wptr, maxwd/* * sizeof(*wptr)*/);
                }

                if (nwd == 0)           /* Empty line */
                    continue;
                /* wptr[0] is the word-string and wptr[1..nwd-1] the pronunciation sequence */
                if (nwd == 1)
                {
                    d.logger.E_ERROR(string.Format("Line {0}: No pronunciation for word '{1}'; ignored\n",
                            lineno, cstring.FromCString(wptr[0])));
                    continue;
                }


                /* Convert pronunciation string to CI-phone-ids */
                for (i = 1; i < nwd; i++)
                {
                    p[i - 1] = dict_ciphone_id(d, wptr[i]);
                    if (SphinxTypes.NOT_S3CIPID(p[i - 1]) != 0)
                    {
                        d.logger.E_ERROR(string.Format("Line {0}: Phone '{1}' is mising in the acoustic model; word '{2}' ignored\n",
                                lineno, cstring.FromCString(wptr[i]), cstring.FromCString(wptr[0])));
                        break;
                    }
                }

                if (i == nwd)
                {         /* All CI-phones successfully converted to IDs */
                    w = dict_add_word(d, wptr[0], p, nwd - 1);
                    if (SphinxTypes.NOT_S3WID(w) != 0)
                        d.logger.E_ERROR
                            (string.Format("Line {0}: Failed to add the word '{1}' (duplicate?); ignored\n",
                             lineno, cstring.FromCString(wptr[0])));
                    else
                    {
                        stralloc += cstring.strlen(d.word[w].word);
                        phnalloc += (uint)d.word[w].pronlen * 2;
                    }
                }
            }

            d.logger.E_INFO(string.Format("Dictionary size {0}, allocated {1} KiB for strings, {2} KiB for phones\n",
                   dict_size(d), (int)stralloc / 1024, (int)phnalloc / 1024));

            return 0;
        }

        internal static dict_t dict_init(cmd_ln_t config, bin_mdef_t mdef, FileAdapter fileAdapter, SphinxLogger logger)
        {
            FILE fp, fp2;
            int n;
            lineiter_t li;
            dict_t d;
            Pointer<short> sil = PointerHelpers.Malloc<short>(1);
            Pointer<byte> dictfile = PointerHelpers.NULL<byte>();
            Pointer<byte> fillerfile = PointerHelpers.NULL<byte>();

            if (config != null)
            {
                dictfile = CommandLine.cmd_ln_str_r(config, cstring.ToCString("-dict"));
                fillerfile = CommandLine.cmd_ln_str_r(config, cstring.ToCString("_fdict"));
            }

            /*
             * First obtain #words in dictionary (for hash table allocation).
             * Reason: The PC NT system doesn't like to grow memory gradually.  Better to allocate
             * all the required memory in one go.
             */
            fp = null;
            n = 0;
            if (dictfile.IsNonNull)
            {
                if ((fp = fileAdapter.fopen(dictfile, "r")) == null)
                {
                    logger.E_ERROR_SYSTEM(string.Format("Failed to open dictionary file '{0}' for reading", cstring.FromCString(dictfile)));
                    return null;
                }
                for (li = PackagedIO.lineiter_start(fp); li != null; li = PackagedIO.lineiter_next(li))
                {
                    if (0 != cstring.strncmp(li.buf, HASHES, 2)
                        && 0 != cstring.strncmp(li.buf, SEMICOLONS, 2))
                        n++;
                }

                fp.fseek(0L, FILE.SEEK_SET);
            }

            fp2 = null;
            if (fillerfile.IsNonNull)
            {
                if ((fp2 = fileAdapter.fopen(fillerfile, "r")) == null)
                {
                    logger.E_ERROR_SYSTEM(string.Format("Failed to open filler dictionary file '{0}' for reading", cstring.FromCString(fillerfile)));
                    fp.fclose();
                    return null;
                }
                for (li = PackagedIO.lineiter_start(fp2); li != null; li = PackagedIO.lineiter_next(li))
                {
                    if (0 != cstring.strncmp(li.buf, HASHES, 2)
                            && 0 != cstring.strncmp(li.buf, SEMICOLONS, 2))
                        n++;
                }

                fp2.fseek(0L, FILE.SEEK_SET);
            }

            /*
             * Allocate dict entries.  HACK!!  Allow some extra entries for words not in file.
             * Also check for type size restrictions.
             */
            d = new dict_t();
            d.logger = logger;
            d.max_words =
                (n + S3DICT_INC_SZ < SphinxTypes.MAX_S3WID) ? n + S3DICT_INC_SZ : SphinxTypes.MAX_S3WID;
            if (n >= SphinxTypes.MAX_S3WID)
            {
                logger.E_ERROR(string.Format("Number of words in dictionaries ({0}) exceeds limit ({1})\n", n,
                        SphinxTypes.MAX_S3WID));
                if (fp != null) fp.fclose();
                if (fp2 != null) fp2.fclose();
                return null;
            }

            logger.E_INFO(string.Format("Allocating {0} * {1} bytes ({2} KiB) for word entries\n",
                   d.max_words, 28,
                   d.max_words * 28 / 1024));
            d.word = CKDAlloc.ckd_calloc_struct<dictword_t>(d.max_words);      /* freed in dict_free() */
            d.n_word = 0;
            if (mdef != null)
                d.mdef = mdef;

            /* Create new hash table for word strings; case-insensitive word strings */
            if (config != null && CommandLine.cmd_ln_exists_r(config, cstring.ToCString("-dictcase")) != 0)
                d.nocase = CommandLine.cmd_ln_boolean_r(config, cstring.ToCString("-dictcase"));
            d.ht = HashTable.hash_table_new(d.max_words, d.nocase, logger);

            /* Digest main dictionary file */
            if (fp != null)
            {
                logger.E_INFO(string.Format("Reading main dictionary: {0}\n", cstring.FromCString(dictfile)));
                dict_read(fp, d);
                fp.fclose();
                logger.E_INFO(string.Format("{0} words read\n", d.n_word));
            }

            if (dict_wordid(d, S3_START_WORD) != SphinxTypes.BAD_S3WID)
            {
                logger.E_ERROR("Remove sentence start word '<s>' from the dictionary\n");
                return null;
            }
            if (dict_wordid(d, S3_FINISH_WORD) != SphinxTypes.BAD_S3WID)
            {
                logger.E_ERROR("Remove sentence start word '</s>' from the dictionary\n");
                return null;
            }
            if (dict_wordid(d, S3_SILENCE_WORD) != SphinxTypes.BAD_S3WID)
            {
                logger.E_ERROR("Remove silence word '<sil>' from the dictionary\n");
                return null;
            }

            /* Now the filler dictionary file, if it exists */
            d.filler_start = d.n_word;
            if (fp2 != null)
            {
                logger.E_INFO(string.Format("Reading filler dictionary: {0}\n", cstring.FromCString(fillerfile)));
                dict_read(fp2, d);
                fp2.fclose();
                logger.E_INFO(string.Format("{0} words read\n", d.n_word - d.filler_start));
            }
            if (mdef != null)
                sil.Deref = checked((short)BinaryModelDef.bin_mdef_silphone(mdef));
            else
                sil.Deref = 0;
            if (dict_wordid(d, S3_START_WORD) == SphinxTypes.BAD_S3WID)
            {
                dict_add_word(d, S3_START_WORD, sil, 1);
            }
            if (dict_wordid(d, S3_FINISH_WORD) == SphinxTypes.BAD_S3WID)
            {
                dict_add_word(d, S3_FINISH_WORD, sil, 1);
            }
            if (dict_wordid(d, S3_SILENCE_WORD) == SphinxTypes.BAD_S3WID)
            {
                dict_add_word(d, S3_SILENCE_WORD, sil, 1);
            }

            d.filler_end = d.n_word - 1;

            /* Initialize distinguished word-ids */
            d.startwid = dict_wordid(d, S3_START_WORD);
            d.finishwid = dict_wordid(d, S3_FINISH_WORD);
            d.silwid = dict_wordid(d, S3_SILENCE_WORD);

            if ((d.filler_start > d.filler_end)
                || (dict_filler_word(d, d.silwid) == 0))
            {
                logger.E_ERROR(string.Format("Word '{0}' must occur (only) in filler dictionary\n",
                        cstring.FromCString(S3_SILENCE_WORD)));
                return null;
            }

            /* No check that alternative pronunciations for filler words are in filler range!! */

            return d;
        }

        internal static int dict_wordid(dict_t d, Pointer<byte> word)
        {
            BoxedValueInt w = new BoxedValueInt();

            SphinxAssert.assert(d != null);
            SphinxAssert.assert(word.IsNonNull);

            if (HashTable.hash_table_lookup_int32(d.ht, word, w) < 0)
                return (SphinxTypes.BAD_S3WID);
            return w.Val;
        }

        internal static int dict_filler_word(dict_t d, int w)
        {
            SphinxAssert.assert(d != null);
            SphinxAssert.assert((w >= 0) && (w < d.n_word));

            w = dict_basewid(d, w);
            if ((w == d.startwid) || (w == d.finishwid))
                return 0;
            if ((w >= d.filler_start) && (w <= d.filler_end))
                return 1;
            return 0;
        }

        internal static int dict_word2basestr(Pointer<byte> word)
        {
            int i, len;

            len = (int)cstring.strlen(word);
            if (word[len - 1] == ')')
            {
                for (i = len - 2; (i > 0) && (word[i] != '('); --i) ;

                if (i > 0)
                {
                    /* The word is of the form <baseword>(...); strip from left-paren */
                    word[i] = (byte)'\0';
                    return i;
                }
            }

            return -1;
        }
    }
}
