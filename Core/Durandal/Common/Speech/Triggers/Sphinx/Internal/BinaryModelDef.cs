using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class BinaryModelDef
    {
        public const ushort BAD_SSID = 0xFFFF;
        public const ushort BAD_SENID = 0xFFFF;
        public const int BIN_MDEF_FORMAT_VERSION = 1;
        public const int BIN_MDEF_NATIVE_ENDIAN = 0x46444d42 /* 'BMDF' in little-endian order */;
        public const int BIN_MDEF_OTHER_ENDIAN = 0x424d4446 /* 'BMDF' in big-endian order */;

        internal static int bin_mdef_is_fillerphone(bin_mdef_t m, int p)
        {
            return (((p) < (m).n_ciphone)
                                 ? (m).phone[p].info_ci_filler
                     : (m).phone[(m).phone[p].info_cd_ctx[0]].info_ci_filler);
        }

        internal static int bin_mdef_is_ciphone(bin_mdef_t m, int p)
        {
            return ((p) < (m).n_ciphone) ? 1 : 0;
        }

        internal static int bin_mdef_n_ciphone(bin_mdef_t m)
        {
            return (m).n_ciphone;
        }

        internal static int bin_mdef_n_phone(bin_mdef_t m)
        {
            return ((m).n_phone);
        }

        internal static int bin_mdef_n_sseq(bin_mdef_t m)
        {
            return ((m).n_sseq);
        }

        internal static int bin_mdef_n_emit_state(bin_mdef_t m)
        {
            return ((m).n_emit_state);
        }

        internal static int bin_mdef_n_emit_state_phone(bin_mdef_t m, int p)
        {
            return ((m).n_emit_state != 0 ? (m).n_emit_state
                              : (m).sseq_len[(m).phone[p].ssid]);
        }

        internal static int bin_mdef_n_sen(bin_mdef_t m)
        {
            return ((m).n_sen);
        }

        internal static int bin_mdef_n_tmat(bin_mdef_t m)
        {
            return ((m).n_tmat);
        }

        internal static int bin_mdef_pid2ssid(bin_mdef_t m, int p)
        {
            return ((m).phone[p].ssid);
        }

        internal static int bin_mdef_pid2tmatid(bin_mdef_t m, int p)
        {
            return ((m).phone[p].tmat);
        }

        internal static int bin_mdef_silphone(bin_mdef_t m)
        {
            return ((m).sil);
        }

        internal static int bin_mdef_sen2cimap(bin_mdef_t m, int s)
        {
            return ((m).sen2cimap[s]);
        }

        internal static ushort bin_mdef_sseq2sen(bin_mdef_t m, int ss, int pos)
        {
            return ((m).sseq[ss][pos]);
        }

        internal static int bin_mdef_pid2ci(bin_mdef_t m, int p)
        {
            return (((p) < (m).n_ciphone) ? (p) : (m).phone[p].info_cd_ctx[0]);
        }

        internal static Pointer<byte> bin_mdef_ciphone_str(bin_mdef_t m, int ci)
        {
            SphinxAssert.assert(m != null);
            SphinxAssert.assert(ci < m.n_ciphone);
            return m.ciname[ci];
        }

        internal static bin_mdef_t bin_mdef_read_text(cmd_ln_t config, Pointer<byte> filename, FileAdapter fileAdapter, SphinxLogger logger)
        {
            bin_mdef_t bmdef;
            mdef_t mdef;
            int i, nodes, ci_idx, lc_idx, rc_idx;
            int nchars;

            if ((mdef = ModelDef.mdef_init((Pointer<byte>)filename, fileAdapter, 1, logger)) == null)
                return null;

            /* Enforce some limits.  */
            if (mdef.n_sen > BAD_SENID)
            {
                logger.E_ERROR(string.Format("Number of senones exceeds limit: {0} > {1}\n",
                        mdef.n_sen, BAD_SENID));
                return null;
            }
            if (mdef.n_sseq > BAD_SSID)
            {
                logger.E_ERROR(string.Format("Number of senone sequences exceeds limit: {0} > {1}\n",
                        mdef.n_sseq, BAD_SSID));
                return null;
            }
            /* We use uint8 for ciphones */
            if (mdef.n_ciphone > 255)
            {
                logger.E_ERROR(string.Format("Number of phones exceeds limit: {0} > {1}\n",
                        mdef.n_ciphone, 255));
                return null;
            }

            bmdef = new bin_mdef_t();

            /* Easy stuff.  The mdef.c code has done the heavy lifting for us. */
            bmdef.n_ciphone = mdef.n_ciphone;
            bmdef.n_phone = mdef.n_phone;
            bmdef.n_emit_state = mdef.n_emit_state;
            bmdef.n_ci_sen = mdef.n_ci_sen;
            bmdef.n_sen = mdef.n_sen;
            bmdef.n_tmat = mdef.n_tmat;
            bmdef.n_sseq = mdef.n_sseq;
            bmdef.sseq = mdef.sseq;
            bmdef.cd2cisen = mdef.cd2cisen;
            bmdef.sen2cimap = mdef.sen2cimap;
            bmdef.n_ctx = 3;           /* Triphones only. */
            bmdef.sil = mdef.sil;
            mdef.sseq = PointerHelpers.NULL<Pointer<ushort>>();          /* We are taking over this one. */
            mdef.cd2cisen = PointerHelpers.NULL<short>();      /* And this one. */
            mdef.sen2cimap = PointerHelpers.NULL<short>();     /* And this one. */

            /* Get the phone names.  If they are not sorted
             * ASCII-betically then we are in a world of hurt and
             * therefore will simply refuse to continue. */
            bmdef.ciname = CKDAlloc.ckd_calloc<Pointer<byte>>(bmdef.n_ciphone);
            nchars = 0;
            for (i = 0; i < bmdef.n_ciphone; ++i)
                nchars += (int)cstring.strlen(mdef.ciphone[i].name) + 1;
            bmdef.ciname[0] = CKDAlloc.ckd_calloc<byte>(nchars);
            cstring.strcpy(bmdef.ciname[0], mdef.ciphone[0].name);
            for (i = 1; i < bmdef.n_ciphone; ++i)
            {
                bmdef.ciname[i] =
                    bmdef.ciname[i - 1] + cstring.strlen(bmdef.ciname[i - 1]) + 1;
                cstring.strcpy(bmdef.ciname[i], mdef.ciphone[i].name);
                if (cstring.strcmp(bmdef.ciname[i - 1], bmdef.ciname[i]) > 0)
                {
                    /* FIXME: there should be a solution to this, actually. */
                    logger.E_ERROR("Phone names are not in sorted order, sorry.");
                    return null;
                }
            }

            /* Copy over phone information. */
            bmdef.phone = new mdef_entry_t[bmdef.n_phone];
            for (i = 0; i < mdef.n_phone; ++i)
            {
                bmdef.phone[i] = new mdef_entry_t();
                bmdef.phone[i].ssid = mdef.phone[i].ssid;
                bmdef.phone[i].tmat = mdef.phone[i].tmat;
                if (i < bmdef.n_ciphone)
                {
                    bmdef.phone[i].info_ci_filler = checked((byte)mdef.ciphone[i].filler);
                }
                else
                {
                    bmdef.phone[i].info_cd_wpos = checked((byte)mdef.phone[i].wpos);
                    Pointer<byte> tmp = bmdef.phone[i].info_cd_ctx;
                    tmp[0] = checked((byte)mdef.phone[i].ci);
                    tmp[1] = checked((byte)mdef.phone[i].lc);
                    tmp[2] = checked((byte)mdef.phone[i].rc);
                }
            }

            /* Walk the wpos_ci_lclist once to find the total number of
             * nodes and the starting locations for each level. */
            nodes = lc_idx = ci_idx = rc_idx = 0;
            for (i = 0; i < ModelDef.N_WORD_POSN; ++i)
            {
                int j;
                for (j = 0; j < mdef.n_ciphone; ++j)
                {
                    ph_lc_t lc;

                    for (lc = mdef.wpos_ci_lclist[i][j]; lc != null; lc = lc.next)
                    {
                        ph_rc_t rc;
                        for (rc = lc.rclist; rc != null; rc = rc.next)
                        {
                            ++nodes;    /* RC node */
                        }
                        ++nodes;        /* LC node */
                        ++rc_idx;       /* Start of RC nodes (after LC nodes) */
                    }
                    ++nodes;            /* CI node */
                    ++lc_idx;           /* Start of LC nodes (after CI nodes) */
                    ++rc_idx;           /* Start of RC nodes (after CI and LC nodes) */
                }
                ++nodes;                /* wpos node */
                ++ci_idx;               /* Start of CI nodes (after wpos nodes) */
                ++lc_idx;               /* Start of LC nodes (after CI nodes) */
                ++rc_idx;               /* STart of RC nodes (after wpos, CI, and LC nodes) */
            }
            logger.E_INFO(string.Format("Allocating {0} * {1} bytes ({2} KiB) for CD tree\n",
                   nodes, 8,
                   nodes * 8 / 1024));
            bmdef.n_cd_tree = nodes;
            bmdef.cd_tree = new cd_tree_t[nodes];
            for (i = 0; i < nodes; i++)
            {
                bmdef.cd_tree[i] = new cd_tree_t();
            }

            for (i = 0; i < ModelDef.N_WORD_POSN; ++i)
            {
                int j;

                bmdef.cd_tree[i].ctx = (short)i;
                bmdef.cd_tree[i].n_down = checked((short)mdef.n_ciphone);
                bmdef.cd_tree[i].c_down = ci_idx;

                /* Now we can build the rest of the tree. */
                for (j = 0; j < mdef.n_ciphone; ++j)
                {
                    ph_lc_t lc;

                    bmdef.cd_tree[ci_idx].ctx = (short)j;
                    bmdef.cd_tree[ci_idx].c_down = lc_idx;
                    for (lc = mdef.wpos_ci_lclist[i][j]; lc != null; lc = lc.next)
                    {
                        ph_rc_t rc;

                        bmdef.cd_tree[lc_idx].ctx = lc.lc;
                        bmdef.cd_tree[lc_idx].c_down = rc_idx;
                        for (rc = lc.rclist; rc != null; rc = rc.next)
                        {
                            bmdef.cd_tree[rc_idx].ctx = rc.rc;
                            bmdef.cd_tree[rc_idx].n_down = 0;
                            bmdef.cd_tree[rc_idx].c_pid = rc.pid;

                            ++bmdef.cd_tree[lc_idx].n_down;
                            ++rc_idx;
                        }
                        /* If there are no triphones here,
                         * this is considered a leafnode, so
                         * set the pid to -1. */
                        if (bmdef.cd_tree[lc_idx].n_down == 0)
                            bmdef.cd_tree[lc_idx].c_pid = -1;

                        ++bmdef.cd_tree[ci_idx].n_down;
                        ++lc_idx;
                    }

                    /* As above, so below. */
                    if (bmdef.cd_tree[ci_idx].n_down == 0)
                        bmdef.cd_tree[ci_idx].c_pid = -1;

                    ++ci_idx;
                }
            }

            bmdef.alloc_mode = alloc_mode.BIN_MDEF_FROM_TEXT;
            return bmdef;
        }

        internal static int FREAD_SWAP32_CHK(bin_mdef_t m, BinaryReader fh, int swap, out int dest, Pointer<byte> filename)
        {
            dest = fh.ReadInt32();
            if (swap != 0) dest = ByteOrder.SWAP_INT32(dest);
            return 0;
        }

        /// <summary>
        /// Reads a cd_tree_t structure from FILE and stores it into the memory location referred to by dest
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="file"></param>
        /// <param name="swap">If non-zero, swap endianness</param>
        private static void read_cdtree_t(out cd_tree_t dest, BinaryReader file, int swap)
        {
            cd_tree_t val = new cd_tree_t();
            val.ctx = file.ReadInt16();
            val.n_down = file.ReadInt16();
            val.c_pid = file.ReadInt32();
            if (swap != 0)
            {
                val.ctx = ByteOrder.SWAP_INT16(val.ctx);
                val.n_down = ByteOrder.SWAP_INT16(val.n_down);
                val.c_pid = ByteOrder.SWAP_INT32(val.c_pid);
            }
            dest = val;
        }

        /// <summary>
        /// Reads a mdef_entry_t structure from FILE and stores it in the memory location referred to by dest
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="file"></param>
        /// <param name="swap">If non-zero, swap endianness</param>
        private static void read_mdef_entry_t(out mdef_entry_t dest, BinaryReader file, int swap)
        {
            mdef_entry_t val = new mdef_entry_t();
            val.ssid = file.ReadInt32();
            val.tmat = file.ReadInt32();
            val.info_cd_wpos = file.ReadByte();
            byte[] ctx = file.ReadBytes(3);
            val.info_cd_ctx = ctx.GetPointer();
            if (swap != 0)
            {
                val.ssid = ByteOrder.SWAP_INT32(val.ssid);
                val.tmat = ByteOrder.SWAP_INT32(val.tmat);
            }
            dest = val;
        }

        internal static bin_mdef_t bin_mdef_read(cmd_ln_t config, Pointer<byte> filename, FileAdapter fileAdapter, SphinxLogger logger)
        {
            bin_mdef_t m;
            FILE fh;
            int i, swap;
            long pos;
            int sseq_size;

            Pointer<byte> val_bytes = PointerHelpers.Malloc<byte>(4);
            Pointer<int> val = val_bytes.ReinterpretCast<int>();

            /* Try to read it as text first. */
            if (config != null && (m = bin_mdef_read_text(config, filename, fileAdapter, logger)) != null)
                return m;

            logger.E_INFO(string.Format("Reading binary model definition: {0}\n", cstring.FromCString(filename)));
            if ((fh = fileAdapter.fopen(filename, "rb")) == null)
                return null;

            Stream fileStream = fh.FileStream;
            using (BinaryReader reader = new BinaryReader(fileStream))
            {
                int byte_order_mark = reader.ReadInt32();
                swap = 0;
                if (byte_order_mark == BIN_MDEF_OTHER_ENDIAN)
                {
                    swap = 1;
                    logger.E_INFO(string.Format("Must byte-swap {0}\n", filename));
                }

                int version = reader.ReadInt32();
                if (swap != 0)
                    version = ByteOrder.SWAP_INT32(version);
                if (version > BIN_MDEF_FORMAT_VERSION)
                {
                    logger.E_ERROR(string.Format("File format version {0} for {1} is newer than library\n",
                            version, cstring.FromCString(filename)));
                    fh.fclose();
                    return null;
                }

                int headerLength = reader.ReadInt32();
                if (swap != 0)
                    headerLength = ByteOrder.SWAP_INT32(headerLength);
                /* Skip format descriptor. */
                fileStream.Seek(headerLength, SeekOrigin.Current);

                /* Finally allocate it. */
                m = new bin_mdef_t();
                m.logger = logger;

                int tmp;
                if (FREAD_SWAP32_CHK(m, reader, swap, out tmp, filename) != 0) return null;
                m.n_ciphone = tmp;
                if (FREAD_SWAP32_CHK(m, reader, swap, out tmp, filename) != 0) return null;
                m.n_phone = tmp;
                if (FREAD_SWAP32_CHK(m, reader, swap, out tmp, filename) != 0) return null;
                m.n_emit_state = tmp;
                if (FREAD_SWAP32_CHK(m, reader, swap, out tmp, filename) != 0) return null;
                m.n_ci_sen = tmp;
                if (FREAD_SWAP32_CHK(m, reader, swap, out tmp, filename) != 0) return null;
                m.n_sen = tmp;
                if (FREAD_SWAP32_CHK(m, reader, swap, out tmp, filename) != 0) return null;
                m.n_tmat = tmp;
                if (FREAD_SWAP32_CHK(m, reader, swap, out tmp, filename) != 0) return null;
                m.n_sseq = tmp;
                if (FREAD_SWAP32_CHK(m, reader, swap, out tmp, filename) != 0) return null;
                m.n_ctx = tmp;
                if (FREAD_SWAP32_CHK(m, reader, swap, out tmp, filename) != 0) return null;
                m.n_cd_tree = tmp;
                if (FREAD_SWAP32_CHK(m, reader, swap, out tmp, filename) != 0) return null;
                m.sil = tmp;

                /* CI names are first in the file. */
                m.ciname = CKDAlloc.ckd_calloc<Pointer<byte>>(m.n_ciphone);

                // LOGAN modified
                // The original code would read the entire rest of the file as a single giant memory allocation, and then find the right indexes
                // for all the datastructures to point into.
                // Since we can't do that safely in C#, we have to rewrite the code to read each struct one-by-one
                // The file format is:
                //char ciphones[][];            n_ciphone null-terminated strings
                //char padding[];               0-3 padding bytes to align file to a 4 byte boundary
                //cd_tree_t[];                  n_cd_tree tree structs
                //mdef_entry_t[] phones[];      n_phones phone structs
                //int32 sseq_len                The total size of the sseq[] block, in 16-bit elements
                //short sseq[];                 an set of 16-bit values with size equal to sseq_len
                //int8 sseq_len[];              n_sseq integers, only if n_emit_state is zero

                // Read the cinames
                m.ciname = PointerHelpers.Malloc<Pointer<byte>>(m.n_ciphone);
                byte[] cinameBuf = new byte[1000];
                for (int cn = 0; cn < m.n_ciphone; cn++)
                {
                    // Read a single cstring from file
                    Pointer<byte> cinameStr = new Pointer<byte>(cinameBuf);
                    Pointer<byte> bufferStr = new Pointer<byte>(cinameBuf);
                    for (int cinameLen = 0; cinameLen < cinameBuf.Length - 1; cinameLen++)
                    {
                        cinameStr.Deref = reader.ReadByte();
                        if (cinameStr.Deref == 0)
                        {
                            // Append null terminator
                            cinameStr[1] = 0;
                            // Copy string from buffer to ciname struct
                            m.ciname[cn] = new Pointer<byte>(cinameLen + 1);
                            cstring.strcpy(m.ciname[cn], bufferStr);
                            string debugStr = cstring.FromCString(m.ciname[cn]);
                            break;
                        }

                        cinameStr = cinameStr.Point(1);
                    }
                }

                // Seek to the nearest 4-byte alignment, based on our current position in the file
                pos = fileStream.Position;
                if (pos % 4 > 0)
                    fileStream.Seek(pos % 4, SeekOrigin.Current);

                // Read all the cd trees
                m.cd_tree = new cd_tree_t[m.n_cd_tree];
                for (int cn = 0; cn < m.n_cd_tree; cn++)
                {
                    read_cdtree_t(out m.cd_tree[cn], reader, swap);
                }

                // Read all the mdef entries
                m.phone = new mdef_entry_t[m.n_phone];
                for (int cn = 0; cn < m.n_phone; cn++)
                {
                    read_mdef_entry_t(out m.phone[cn], reader, swap);
                }

                // read sseq_size, which is the number of 16-bit values in the entire sseq data structure
                if (FREAD_SWAP32_CHK(m, reader, swap, out sseq_size, filename) != 0) return null;
                if (swap != 0)
                    sseq_size = ByteOrder.SWAP_INT32(sseq_size);

                // Read that many sseqs from the file into a block
                ushort[] sseq_buf = new ushort[sseq_size];
                for (i = 0; i < sseq_size; ++i)
                    sseq_buf[i] = reader.ReadUInt16();

                if (swap != 0)
                {
                    for (i = 0; i < sseq_size; ++i)
                        sseq_buf[i] = ByteOrder.SWAP_INT16(sseq_buf[i]);
                }

                // Now create pointer indexes into that block
                m.sseq = CKDAlloc.ckd_calloc<Pointer<ushort>>(m.n_sseq);
                m.sseq[0] = sseq_buf.GetPointer();

                if (m.n_emit_state != 0)
                {
                    for (i = 1; i < m.n_sseq; ++i)
                        m.sseq[i] = m.sseq[0] + (i * m.n_emit_state);
                }
                else
                {
                    // The rest of the file in this case is a byte array of sseq_lengths. Read the rest of the file
                    int sseqLenBlockSize = (int)(fileStream.Length - fileStream.Position);
                    m.sseq_len = reader.ReadBytes(sseqLenBlockSize).GetPointer();
                    for (i = 1; i < m.n_sseq; ++i)
                        m.sseq[i] = m.sseq[i - 1] + m.sseq_len[i - 1];
                }

                /* Now build the CD-to-CI mappings using the senone sequences.
                 * This is the only really accurate way to do it, though it is
                 * still inaccurate in the case of heterogeneous topologies or
                 * cross-state tying. */
                m.cd2cisen = CKDAlloc.ckd_malloc<short>(m.n_sen);
                m.sen2cimap = CKDAlloc.ckd_malloc<short>(m.n_sen);

                /* Default mappings (identity, none) */
                for (i = 0; i < m.n_ci_sen; ++i)
                    m.cd2cisen[i] = checked((short)i);
                for (; i < m.n_sen; ++i)
                    m.cd2cisen[i] = -1;
                for (i = 0; i < m.n_sen; ++i)
                    m.sen2cimap[i] = -1;
                for (i = 0; i < m.n_phone; ++i)
                {
                    int j, ssid = m.phone[i].ssid;

                    for (j = 0; j < bin_mdef_n_emit_state_phone(m, i); ++j)
                    {
                        int s = bin_mdef_sseq2sen(m, ssid, j);
                        int ci = bin_mdef_pid2ci(m, i);
                        /* Take the first one and warn if we have cross-state tying. */
                        if (m.sen2cimap[s] == -1)
                            m.sen2cimap[s] = checked((short)ci);
                        if (m.sen2cimap[s] != ci)
                            logger.E_WARN(string.Format("Senone {0} is shared between multiple base phones\n",
                                 s));

                        if (j > bin_mdef_n_emit_state_phone(m, ci))
                            logger.E_WARN(string.Format("CD phone {0} has fewer states than CI phone {1}\n",
                                   i, ci));
                        else
                            m.cd2cisen[s] = (short)bin_mdef_sseq2sen(m, m.phone[ci].ssid, j);
                    }
                }

                /* Set the silence phone. */
                m.sil = bin_mdef_ciphone_id(m, ModelDef.S3_SILENCE_CIPHONE);

                logger.E_INFO
                    (string.Format("{0} CI-phone, {1} CD-phone, {2} emitstate/phone, {3} CI-sen, {4} Sen, {5} Sen-Seq\n",
                     m.n_ciphone, m.n_phone - m.n_ciphone, m.n_emit_state,
                     m.n_ci_sen, m.n_sen, m.n_sseq));
                fh.fclose();
                return m;
            }
        }

        internal static int bin_mdef_ciphone_id(bin_mdef_t m, Pointer<byte> ciphone)
        {
            int low, mid, high;

            /* Exact binary search on m.Deref.ciphone */
            low = 0;
            high = m.n_ciphone;
            while (low < high)
            {
                int c;

                mid = (low + high) / 2;
                c = (int)cstring.strcmp(ciphone, m.ciname[mid]);
                if (c == 0)
                    return mid;
                else if (c > 0)
                    low = mid + 1;
                else
                    high = mid;
            }
            return -1;
        }

        internal static int bin_mdef_ciphone_id_nocase(bin_mdef_t m, Pointer<byte> ciphone)
        {
            int low, mid, high;

            /* Exact binary search on m.Deref.ciphone */
            low = 0;
            high = m.n_ciphone;
            while (low < high)
            {
                int c;

                mid = (low + high) / 2;
                c = StringCase.strcmp_nocase(ciphone, m.ciname[mid]);
                if (c == 0)
                    return mid;
                else if (c > 0)
                    low = mid + 1;
                else
                    high = mid;
            }
            return -1;
        }

        internal static int bin_mdef_phone_id(bin_mdef_t m, int ci, int lc, int rc, int wpos)
        {
            int cd_tree_ptr;
            int level, max;
            Pointer<short> ctx = PointerHelpers.Malloc<short>(4);

            SphinxAssert.assert(m != null);

            /* In the future, we might back off when context is not available,
             * but for now we'll just return the CI phone. */
            if (lc < 0 || rc < 0)
                return ci;

            SphinxAssert.assert((ci >= 0) && (ci < m.n_ciphone));
            SphinxAssert.assert((lc >= 0) && (lc < m.n_ciphone));
            SphinxAssert.assert((rc >= 0) && (rc < m.n_ciphone));
            SphinxAssert.assert((wpos >= 0) && (wpos < ModelDef.N_WORD_POSN));

            /* Create a context list, mapping fillers to silence. */
            ctx[0] = (short)wpos;
            ctx[1] = (short)ci;
            ctx[2] = (short)((m.sil >= 0
                      && m.phone[lc].info_ci_filler != 0) ? m.sil : lc);
            ctx[3] = (short)((m.sil >= 0
                      && m.phone[rc].info_ci_filler != 0) ? m.sil : rc);

            /* Walk down the cd_tree. */
            cd_tree_ptr = 0;
            level = 0;                  /* What level we are on. */
            max = ModelDef.N_WORD_POSN;          /* Number of nodes on this level. */
            while (level < 4)
            {
                int i;

                for (i = 0; i < max; ++i)
                {
                    if (m.cd_tree[cd_tree_ptr + i].ctx == ctx[level])
                        break;
                }
                if (i == max)
                    return -1;

                /* Leaf node, stop here. */
                if (m.cd_tree[cd_tree_ptr + i].n_down == 0)
                    return m.cd_tree[cd_tree_ptr + i].c_pid;

                /* Go down one level. */
                max = m.cd_tree[cd_tree_ptr + i].n_down;
                cd_tree_ptr = m.cd_tree[cd_tree_ptr + i].c_down;
                ++level;
            }
            /* We probably shouldn't get here. */
            return -1;
        }

        internal static int bin_mdef_phone_id_nearest(bin_mdef_t m, int b, int l, int r, int pos)
        {
            int p, tmppos;

            /* In the future, we might back off when context is not available,
             * but for now we'll just return the CI phone. */
            if (l < 0 || r < 0)
                return b;

            p = bin_mdef_phone_id(m, b, l, r, pos);
            if (p >= 0)
                return p;

            /* Exact triphone not found; backoff to other word positions */
            for (tmppos = 0; tmppos < ModelDef.N_WORD_POSN; tmppos++)
            {
                if (tmppos != pos)
                {
                    p = bin_mdef_phone_id(m, b, l, r, tmppos);
                    if (p >= 0)
                        return p;
                }
            }

            /* Nothing yet; backoff to silence phone if non-silence filler context */
            /* In addition, backoff to silence phone on left/right if in beginning/end position */
            if (m.sil >= 0)
            {
                int newl = l, newr = r;
                if (m.phone[(int)l].info_ci_filler != 0
                    || pos == word_posn_t.WORD_POSN_BEGIN || pos == word_posn_t.WORD_POSN_SINGLE)
                    newl = m.sil;
                if (m.phone[(int)r].info_ci_filler != 0
                    || pos == word_posn_t.WORD_POSN_END || pos == word_posn_t.WORD_POSN_SINGLE)
                    newr = m.sil;
                if ((newl != l) || (newr != r))
                {
                    p = bin_mdef_phone_id(m, b, newl, newr, pos);
                    if (p >= 0)
                        return p;

                    for (tmppos = 0; tmppos < ModelDef.N_WORD_POSN; tmppos++)
                    {
                        if (tmppos != pos)
                        {
                            p = bin_mdef_phone_id(m, b, newl, newr, tmppos);
                            if (p >= 0)
                                return p;
                        }
                    }
                }
            }

            /* Nothing yet; backoff to base phone */
            return b;
        }
    }
}
