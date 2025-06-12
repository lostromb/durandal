using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class DictToPID
    {
        internal static void compress_table(Pointer<ushort> uncomp_tab, Pointer<ushort> com_tab,
               Pointer<short> ci_map, int n_ci)
        {
            int found;
            int r;
            int tmp_r;

            for (r = 0; r < n_ci; r++)
            {
                com_tab[r] = SphinxTypes.BAD_S3SSID;
                ci_map[r] = SphinxTypes.BAD_S3CIPID;
            }

            // Compress this map
            for (r = 0; r < n_ci; r++)
            {

                found = 0;
                for (tmp_r = 0; tmp_r < r && com_tab[tmp_r] != SphinxTypes.BAD_S3SSID; tmp_r++)
                {   /* If it appears before, just filled in cimap; */
                    if (uncomp_tab[r] == com_tab[tmp_r])
                    {
                        found = 1;
                        ci_map[r] = checked((short)tmp_r);
                        break;
                    }
                }

                if (found == 0)
                {
                    com_tab[tmp_r] = uncomp_tab[r];
                    ci_map[r] = checked((short)tmp_r);
                }
            }
        }


        internal static void compress_right_context_tree(dict2pid_t d2p,
                                    Pointer<Pointer<Pointer<ushort>>> rdiph_rc)
        {
            int n_ci;
            int b, l, r;
            Pointer<ushort> rmap;
            Pointer<ushort> tmpssid;
            Pointer<short> tmpcimap;
            bin_mdef_t mdef = d2p.mdef;
            uint alloc;

            n_ci = mdef.n_ciphone;

            tmpssid = CKDAlloc.ckd_calloc<ushort>(n_ci);
            tmpcimap = CKDAlloc.ckd_calloc<short>(n_ci);

            d2p.rssid =
                (Pointer < Pointer < xwdssid_t >>)CKDAlloc.ckd_calloc<Pointer<xwdssid_t>>(mdef.n_ciphone);
            alloc = (uint)(mdef.n_ciphone * 8);

            for (b = 0; b < n_ci; b++)
            {
                d2p.rssid[b] =
                    (Pointer<xwdssid_t>)CKDAlloc.ckd_calloc_struct<xwdssid_t>(mdef.n_ciphone);
                alloc += (uint)(mdef.n_ciphone * 20);

                for (l = 0; l < n_ci; l++)
                {
                    rmap = rdiph_rc[b][l];
                    compress_table(rmap, tmpssid, tmpcimap, mdef.n_ciphone);

                    for (r = 0; r < mdef.n_ciphone && tmpssid[r] != SphinxTypes.BAD_S3SSID;
                         r++) ;

                    if (tmpssid[0] != SphinxTypes.BAD_S3SSID)
                    {
                        d2p.rssid[b][l].ssid = CKDAlloc.ckd_calloc<ushort>(r);
                        tmpssid.MemCopyTo(d2p.rssid[b][l].ssid, r);
                        d2p.rssid[b][l].cimap =
                            CKDAlloc.ckd_calloc<short>(mdef.n_ciphone);
                        tmpcimap.MemCopyTo(d2p.rssid[b][l].cimap, (mdef.n_ciphone));
                        d2p.rssid[b][l].n_ssid = r;
                    }
                    else
                    {
                        d2p.rssid[b][l].ssid = PointerHelpers.NULL<ushort>();
                        d2p.rssid[b][l].cimap = PointerHelpers.NULL<short>();
                        d2p.rssid[b][l].n_ssid = 0;
                    }
                }
            }

            d2p.logger.E_INFO(string.Format("Allocated {0} bytes ({1} KiB) for word-final triphones\n",
                   (int)alloc, (int)alloc / 1024));
        }

        internal static void compress_left_right_context_tree(dict2pid_t d2p)
        {
            int n_ci;
            int b, l, r;
            Pointer<ushort> rmap;
            Pointer<ushort> tmpssid;
            Pointer<short> tmpcimap;
            bin_mdef_t mdef = d2p.mdef;
            uint alloc;

            n_ci = mdef.n_ciphone;

            tmpssid = CKDAlloc.ckd_calloc<ushort>(n_ci);
            tmpcimap = CKDAlloc.ckd_calloc<short>(n_ci);

            SphinxAssert.assert(d2p.lrdiph_rc.IsNonNull);

            d2p.lrssid =
                (Pointer<Pointer<xwdssid_t>>)CKDAlloc.ckd_calloc<Pointer<xwdssid_t>>(mdef.n_ciphone);
            alloc = (uint)(mdef.n_ciphone * 8);

            for (b = 0; b < n_ci; b++)
            {

                d2p.lrssid[b] =
                    (Pointer<xwdssid_t>)CKDAlloc.ckd_calloc_struct<xwdssid_t>(mdef.n_ciphone);
                alloc += (uint)(mdef.n_ciphone * 20);
                
                for (l = 0; l < n_ci; l++)
                {
                    rmap = d2p.lrdiph_rc[b][l];

                    compress_table(rmap, tmpssid, tmpcimap, mdef.n_ciphone);

                    for (r = 0; r < mdef.n_ciphone && tmpssid[r] != SphinxTypes.BAD_S3SSID;
                         r++) ;

                    if (tmpssid[0] != SphinxTypes.BAD_S3SSID)
                    {
                        d2p.lrssid[b][l].ssid = CKDAlloc.ckd_calloc<ushort>(r);
                        tmpssid.MemCopyTo(d2p.lrssid[b][l].ssid, r);
                        d2p.lrssid[b][l].cimap =
                            CKDAlloc.ckd_calloc<short>(mdef.n_ciphone);
                        tmpcimap.MemCopyTo(d2p.lrssid[b][l].cimap, mdef.n_ciphone);
                        d2p.lrssid[b][l].n_ssid = r;
                    }
                    else
                    {
                        d2p.lrssid[b][l].ssid = PointerHelpers.NULL<ushort>();
                        d2p.lrssid[b][l].cimap = PointerHelpers.NULL<short>();
                        d2p.lrssid[b][l].n_ssid = 0;
                    }
                }
            }

            /* Try to compress lrdiph_rc into lrdiph_rc_compressed */

            d2p.logger.E_INFO(string.Format("Allocated {0} bytes ({1} KiB) for single-phone word triphones\n",
                   (int)alloc, (int)alloc / 1024));
        }

        internal static void populate_lrdiph(dict2pid_t d2p, Pointer<Pointer<Pointer<ushort>>> rdiph_rc, short b)
        {
            bin_mdef_t mdef = d2p.mdef;
            short l, r;

            for (l = 0; l < BinaryModelDef.bin_mdef_n_ciphone(mdef); l++)
            {
                for (r = 0; r < BinaryModelDef.bin_mdef_n_ciphone(mdef); r++)
                {
                    int p;
                    p = BinaryModelDef.bin_mdef_phone_id_nearest(mdef, (short)b,
                                                  (short)l,
                                                  (short)r,
                                                  word_posn_t.WORD_POSN_SINGLE);
                    d2p.lrdiph_rc[b][l].Set(r, checked((ushort)BinaryModelDef.bin_mdef_pid2ssid(mdef, p)));
                    if (r == BinaryModelDef.bin_mdef_silphone(mdef))
                    {
                        d2p.ldiph_lc[b][r].Set(l, checked((ushort)BinaryModelDef.bin_mdef_pid2ssid(mdef, p)));
                    }
                    if (rdiph_rc.IsNonNull && l == BinaryModelDef.bin_mdef_silphone(mdef))
                    {
                        rdiph_rc[b][l].Set(r, checked((ushort)BinaryModelDef.bin_mdef_pid2ssid(mdef, p)));
                    }
                    SphinxAssert.assert(SphinxTypes.IS_S3SSID(BinaryModelDef.bin_mdef_pid2ssid(mdef, p)) != 0);
                    // LOGAN this dumped way too much
                    //Logger.E_DEBUG(string.Format("{0}({1},{2}) => {3} / {4}\n",
                    //        cstring.FromCString(bin_mdef.bin_mdef_ciphone_str(mdef, b)),
                    //        cstring.FromCString(bin_mdef.bin_mdef_ciphone_str(mdef, l)),
                    //        cstring.FromCString(bin_mdef.bin_mdef_ciphone_str(mdef, r)),
                    //        p, bin_mdef.bin_mdef_pid2ssid(mdef, p)));
                }
            }
        }

        internal static ushort dict2pid_internal(dict2pid_t d2p,
                          int wid,
                          int pos)
        {
            int b, l, r, p;
            dict_t dictionary = d2p.dict;
            bin_mdef_t mdef = d2p.mdef;

            if (pos == 0 || pos == Dictionary.dict_pronlen(dictionary, wid))
                return SphinxTypes.BAD_S3SSID;

            b = Dictionary.dict_pron(dictionary, wid, pos);
            l = Dictionary.dict_pron(dictionary, wid, pos - 1);
            r = Dictionary.dict_pron(dictionary, wid, pos + 1);
            p = BinaryModelDef.bin_mdef_phone_id_nearest(mdef, (short)b,
                                          (short)l, (short)r,
                                          word_posn_t.WORD_POSN_INTERNAL);
            return checked((ushort)BinaryModelDef.bin_mdef_pid2ssid(mdef, p));
        }

        internal static dict2pid_t dict2pid_build(bin_mdef_t mdef, dict_t dictionary, SphinxLogger logger)
        {
            dict2pid_t returnVal;
            Pointer<Pointer<Pointer <ushort>>> rdiph_rc;
            Pointer<uint> ldiph;
            Pointer<uint> rdiph;
            Pointer<uint> single;
            int pronlen;
            int b, l, r, w, p;

            logger.E_INFO("Building PID tables for dictionary\n");
            SphinxAssert.assert(mdef != null);
            SphinxAssert.assert(dictionary != null);

            returnVal = new dict2pid_t();
            returnVal.logger = logger;
            returnVal.mdef = mdef;
            returnVal.dict = dictionary;
            logger.E_INFO(string.Format("Allocating {0}^3 * {1} bytes ({2} KiB) for word-initial triphones\n",
                   mdef.n_ciphone, sizeof(ushort),
                   mdef.n_ciphone * mdef.n_ciphone * mdef.n_ciphone * sizeof(ushort) / 1024));
            returnVal.ldiph_lc = CKDAlloc.ckd_calloc_3d<ushort>((uint)mdef.n_ciphone, (uint)mdef.n_ciphone,
                                             (uint)mdef.n_ciphone);
            /* Only used internally to generate rssid */
            rdiph_rc = CKDAlloc.ckd_calloc_3d<ushort>((uint)mdef.n_ciphone, (uint)mdef.n_ciphone,
                                             (uint)mdef.n_ciphone);

            returnVal.lrdiph_rc = CKDAlloc.ckd_calloc_3d<ushort>((uint)mdef.n_ciphone,
                                                                       (uint)mdef.n_ciphone,
                                                                       (uint)mdef.n_ciphone);
            /* Actually could use memset for this, if s3types.BAD_S3SSID is guaranteed
             * to be 65535... */
            for (b = 0; b < mdef.n_ciphone; ++b)
            {
                for (r = 0; r < mdef.n_ciphone; ++r)
                {
                    for (l = 0; l < mdef.n_ciphone; ++l)
                    {
                        returnVal.ldiph_lc[b][r].Set(l, SphinxTypes.BAD_S3SSID);
                        returnVal.lrdiph_rc[b][l].Set(r, SphinxTypes.BAD_S3SSID);
                        rdiph_rc[b][l].Set(r, SphinxTypes.BAD_S3SSID);
                    }
                }
            }

            /* Track which diphones / ciphones have been seen. */
            ldiph = BitVector.bitvec_alloc(mdef.n_ciphone * mdef.n_ciphone);
            rdiph = BitVector.bitvec_alloc(mdef.n_ciphone * mdef.n_ciphone);
            single = BitVector.bitvec_alloc(mdef.n_ciphone);

            for (w = 0; w < Dictionary.dict_size(returnVal.dict); w++)
            {
                pronlen = Dictionary.dict_pronlen(dictionary, w);

                if (pronlen >= 2)
                {
                    b = Dictionary.dict_first_phone(dictionary, w);
                    r = Dictionary.dict_second_phone(dictionary, w);
                    /* Populate ldiph_lc */
                    if (BitVector.bitvec_is_clear(ldiph, b * mdef.n_ciphone + r) != 0)
                    {
                        /* Mark this diphone as done */
                        BitVector.bitvec_set(ldiph, b * mdef.n_ciphone + r);

                        /* Record all possible ssids for b(?,r) */
                        for (l = 0; l < BinaryModelDef.bin_mdef_n_ciphone(mdef); l++)
                        {
                            p = BinaryModelDef.bin_mdef_phone_id_nearest(mdef, (short)b,
                                                      (short)l, (short)r,
                                                      word_posn_t.WORD_POSN_BEGIN);
                            returnVal.ldiph_lc[b][r].Set(l, checked((ushort)BinaryModelDef.bin_mdef_pid2ssid(mdef, p)));
                        }
                    }


                    /* Populate rdiph_rc */
                    l = Dictionary.dict_second_last_phone(dictionary, w);
                    b = Dictionary.dict_last_phone(dictionary, w);
                    if (BitVector.bitvec_is_clear(rdiph, b * mdef.n_ciphone + l) != 0)
                    {
                        /* Mark this diphone as done */
                        BitVector.bitvec_set(rdiph, b * mdef.n_ciphone + l);

                        for (r = 0; r < BinaryModelDef.bin_mdef_n_ciphone(mdef); r++)
                        {
                            p = BinaryModelDef.bin_mdef_phone_id_nearest(mdef, (short)b,
                                                      (short)l, (short)r,
                                                      word_posn_t.WORD_POSN_END);
                            rdiph_rc[b][l].Set(r, checked((ushort)BinaryModelDef.bin_mdef_pid2ssid(mdef, p)));
                        }
                    }
                }
                else if (pronlen == 1)
                {
                    b = Dictionary.dict_pron(dictionary, w, 0);
                    logger.E_DEBUG(string.Format("Building tables for single phone word {0} phone {1} = {2}\n",
                               cstring.FromCString(Dictionary.dict_wordstr(dictionary, w)), b, cstring.FromCString(BinaryModelDef.bin_mdef_ciphone_str(mdef, b))));
                    /* Populate lrdiph_rc (and also ldiph_lc, rdiph_rc if needed) */
                    if (BitVector.bitvec_is_clear(single, b) != 0)
                    {
                        populate_lrdiph(returnVal, rdiph_rc, checked((short)b));
                        BitVector.bitvec_set(single, b);
                    }
                }
            }

            /* Try to compress rdiph_rc into rdiph_rc_compressed */
            compress_right_context_tree(returnVal, rdiph_rc);
            compress_left_right_context_tree(returnVal);
            
            return returnVal;
        }
    }
}
