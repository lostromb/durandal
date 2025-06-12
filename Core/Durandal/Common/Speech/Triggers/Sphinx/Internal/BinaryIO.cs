using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class BinaryIO
    {
        public const int BYTE_ORDER_MAGIC = 0x11223344;
        public const int BIO_HDRARG_MAX = 32;
        internal static readonly Pointer<byte> END_COMMENT = cstring.ToCString("*end_comment*\n");

        internal static void bcomment_read(FILE fp, SphinxLogger logger)
        {
            Pointer<byte> iline = PointerHelpers.Malloc<byte>(16384);

            while (fp.fgets(iline, 16384).IsNonNull)
            {
                if (cstring.strcmp(iline, END_COMMENT) == 0)
                    return;
            }

            logger.E_FATAL(string.Format("Missing {0} marker\n", cstring.FromCString(END_COMMENT)));
        }

        internal static void bcomment_read(BinaryReader fp, SphinxLogger logger)
        {
            Pointer<byte> iline = PointerHelpers.Malloc<byte>(16384);

            while (FILE.fgets(fp, iline, 16384).IsNonNull)
            {
                if (cstring.strcmp(iline, END_COMMENT) == 0)
                    return;
            }

            logger.E_FATAL(string.Format("Missing {0} marker\n", cstring.FromCString(END_COMMENT)));
        }

        internal static int swap_check(FILE fp, SphinxLogger logger)
        {
            Pointer<byte> magic_buf = PointerHelpers.Malloc<byte>(4);
            Pointer<uint> magic = magic_buf.ReinterpretCast<uint>();

            if (fp.fread(magic_buf, 4, 1) != 1)
            {
                logger.E_ERROR("Cannot read BYTEORDER MAGIC NO.\n");
                return -1;
            }

            if (+magic != BYTE_ORDER_MAGIC)
            {
                /* either need to swap or got bogus magic number */
                ByteOrder.SWAP_INT32(magic);

                if (+magic == BYTE_ORDER_MAGIC)
                    return 1;

                ByteOrder.SWAP_INT32(magic);
                logger.E_ERROR(string.Format("Bad BYTEORDER MAGIC NO: {0:x8}, expecting {1:x8}\n",
                        magic, BYTE_ORDER_MAGIC));
                return -1;
            }

            return 0;
        }

        internal static int swap_check(BinaryReader fp, SphinxLogger logger)
        {
            uint magic = fp.ReadUInt32();

            if (magic != BYTE_ORDER_MAGIC)
            {
                /* either need to swap or got bogus magic number */
                magic = ByteOrder.SWAP_INT32(magic);

                if (magic == BYTE_ORDER_MAGIC)
                    return 1;

                magic = ByteOrder.SWAP_INT32(magic);
                logger.E_ERROR(string.Format("Bad BYTEORDER MAGIC NO: {0:x8}, expecting {1:x8}\n",
                        magic, BYTE_ORDER_MAGIC));
                return -1;
            }

            return 0;
        }

        internal static int bio_readhdr(FILE fp, BoxedValue<Pointer<Pointer<byte>>> argname, BoxedValue<Pointer<Pointer<byte>>> argval, out int swap, SphinxLogger logger)
        {
            Pointer<byte> line = PointerHelpers.Malloc<byte>(16384);
            Pointer<byte> word = PointerHelpers.Malloc<byte>(4096);
            int i, l;
            int lineno;

            argname.Val = CKDAlloc.ckd_calloc<Pointer<byte>>(BIO_HDRARG_MAX + 1);
            argval.Val = CKDAlloc.ckd_calloc<Pointer<byte>>(BIO_HDRARG_MAX);

            lineno = 0;
            if (fp.fgets(line, 16384).IsNull)
            {
                logger.E_ERROR(string.Format("Premature EOF, line {0}\n", lineno));
                goto error_out;
            }
            lineno++;

            if ((line[0] == 's') && (line[1] == '3') && (line[2] == '\n'))
            {
                /* New format (post Dec-1996, including checksums); read argument-value pairs */
                for (i = 0; ;)
                {
                    if (fp.fgets(line, 16384).IsNull)
                    {
                        logger.E_ERROR(string.Format("Premature EOF, line {0}\n", lineno));
                        goto error_out;
                    }
                    lineno++;

                    if (stdio.sscanf_s_n(line, word, out l) != 1)
                    {
                        logger.E_ERROR(string.Format("Header format error, line {0}\n", lineno));
                        goto error_out;
                    }
                    if (cstring.strcmp(word, cstring.ToCString("endhdr")) == 0)
                        break;
                    if (word[0] == '#') /* Skip comments */
                        continue;

                    if (i >= BIO_HDRARG_MAX)
                    {
                        logger.E_ERROR
                            (string.Format("Max arg-value limit({0}) exceeded; increase BIO_HDRARG_MAX\n",
                             BIO_HDRARG_MAX));
                        goto error_out;
                    }

                    argname.Val[i] = CKDAlloc.ckd_salloc(word);
                    if (stdio.sscanf_s(line + l, word) != 1)
                    {      /* Multi-word values not allowed */
                        logger.E_ERROR(string.Format("Header format error, line {0}\n", lineno));
                        goto error_out;
                    }
                    argval.Val[i] = CKDAlloc.ckd_salloc(word);
                    i++;
                }
            }
            else
            {
                /* Old format (without checksums); the first entry must be the version# */
                if (stdio.sscanf_s(line, word) != 1)
                {
                    logger.E_ERROR(string.Format("Header format error, line {0}\n", lineno));
                    goto error_out;
                }

                argname.Val[0] = CKDAlloc.ckd_salloc(cstring.ToCString("version"));
                argval.Val[0] = CKDAlloc.ckd_salloc(word);
                i = 1;

                bcomment_read(fp, logger);
            }
            argname.Val[i] = PointerHelpers.NULL<byte>();

            if ((swap = swap_check(fp, logger)) < 0)
            {
                logger.E_ERROR("swap_check failed\n");
                goto error_out;
            }

            return 0;
            error_out:
            argname.Val = argval.Val = PointerHelpers.NULL<Pointer<byte>>();
            swap = 0;
            return -1;
        }

        internal static int bio_readhdr(BinaryReader fp, BoxedValue<Pointer<Pointer<byte>>> argname, BoxedValue<Pointer<Pointer<byte>>> argval, out int swap, SphinxLogger logger)
        {
            Pointer<byte> line = PointerHelpers.Malloc<byte>(16384);
            Pointer<byte> word = PointerHelpers.Malloc<byte>(4096);
            int i, l;
            int lineno;

            argname.Val = CKDAlloc.ckd_calloc<Pointer<byte>>(BIO_HDRARG_MAX + 1);
            argval.Val = CKDAlloc.ckd_calloc<Pointer<byte>>(BIO_HDRARG_MAX);

            lineno = 0;
            if (FILE.fgets(fp, line, 16384).IsNull)
            {
                logger.E_ERROR(string.Format("Premature EOF, line {0}\n", lineno));
                goto error_out;
            }
            lineno++;

            if ((line[0] == 's') && (line[1] == '3') && (line[2] == '\n'))
            {
                /* New format (post Dec-1996, including checksums); read argument-value pairs */
                for (i = 0; ;)
                {
                    if (FILE.fgets(fp, line, 16384).IsNull)
                    {
                        logger.E_ERROR(string.Format("Premature EOF, line {0}\n", lineno));
                        goto error_out;
                    }
                    lineno++;

                    if (stdio.sscanf_s_n(line, word, out l) != 1)
                    {
                        logger.E_ERROR(string.Format("Header format error, line {0}\n", lineno));
                        goto error_out;
                    }
                    if (cstring.strcmp(word, cstring.ToCString("endhdr")) == 0)
                        break;
                    if (word[0] == '#') /* Skip comments */
                        continue;

                    if (i >= BIO_HDRARG_MAX)
                    {
                        logger.E_ERROR
                            (string.Format("Max arg-value limit({0}) exceeded; increase BIO_HDRARG_MAX\n",
                             BIO_HDRARG_MAX));
                        goto error_out;
                    }

                    argname.Val[i] = CKDAlloc.ckd_salloc(word);
                    if (stdio.sscanf_s(line + l, word) != 1)
                    {      /* Multi-word values not allowed */
                        logger.E_ERROR(string.Format("Header format error, line {0}\n", lineno));
                        goto error_out;
                    }
                    argval.Val[i] = CKDAlloc.ckd_salloc(word);
                    i++;
                }
            }
            else
            {
                /* Old format (without checksums); the first entry must be the version# */
                if (stdio.sscanf_s(line, word) != 1)
                {
                    logger.E_ERROR(string.Format("Header format error, line {0}\n", lineno));
                    goto error_out;
                }

                argname.Val[0] = CKDAlloc.ckd_salloc(cstring.ToCString("version"));
                argval.Val[0] = CKDAlloc.ckd_salloc(word);
                i = 1;

                bcomment_read(fp, logger);
            }
            argname.Val[i] = PointerHelpers.NULL<byte>();

            if ((swap = swap_check(fp, logger)) < 0)
            {
                logger.E_ERROR("swap_check failed\n");
                goto error_out;
            }

            return 0;
            error_out:
            argname.Val = argval.Val = PointerHelpers.NULL<Pointer<byte>>();
            swap = 0;
            return -1;
        }

        internal static uint chksum_accum(Pointer<byte> buf, int el_sz, int n_el, uint sum, SphinxLogger logger)
        {
            int i;
            Pointer<byte> i8;
            Pointer<ushort> i16;
            Pointer<uint> i32;

            switch (el_sz)
            {
                case 1:
                    i8 = buf.ReinterpretCast<byte>();
                    for (i = 0; i < n_el; i++)
                        sum = (sum << 5 | sum >> 27) + i8[i];
                    break;
                case 2:
                    i16 = buf.ReinterpretCast<ushort>();
                    for (i = 0; i < n_el; i++)
                        sum = (sum << 10 | sum >> 22) + i16[i];
                    break;
                case 4:
                    i32 = buf.ReinterpretCast<uint>();
                    for (i = 0; i < n_el; i++)
                        sum = (sum << 20 | sum >> 12) + i32[i];
                    break;
                default:
                    logger.E_FATAL(string.Format("Unsupported elemsize for checksum: {0}\n", el_sz));
                    break;
            }

            return sum;
        }

        internal static uint chksum_accum(int val, uint sum, SphinxLogger logger)
        {
            return (sum << 20 | sum >> 12) + (uint)val;
        }

        internal static uint chksum_accum(uint val, uint sum, SphinxLogger logger)
        {
            return (sum << 20 | sum >> 12) + val;
        }

        internal static void swap_buf(Pointer<byte> buf, int el_sz, int n_el, SphinxLogger logger)
        {
            int i;
            Pointer<ushort> buf16;
            Pointer<uint> buf32;

            switch (el_sz)
            {
                case 1:
                    break;
                case 2:
                    buf16 = buf.ReinterpretCast<ushort>();
                    for (i = 0; i < n_el; i++)
                        ByteOrder.SWAP_INT16(buf16 + i);
                    break;
                case 4:
                    buf32 = buf.ReinterpretCast<uint>();
                    for (i = 0; i < n_el; i++)
                        ByteOrder.SWAP_INT32(buf32 + i);
                    break;
                default:
                    logger.E_FATAL(string.Format("Unsupported elemsize for byteswapping: {0}\n", el_sz));
                    break;
            }
        }

        internal static int bio_fread(Pointer<byte> buf, int el_sz, int n_el, FILE fp, int swap, BoxedValueUInt chksum, SphinxLogger logger)
        {
            if (fp.fread(buf, (uint)el_sz, (uint)n_el) != (uint)n_el)
                return -1;

            if (swap != 0)
                swap_buf(buf, el_sz, n_el, logger);

            if (chksum != null)
                chksum.Val = chksum_accum(buf, el_sz, n_el, chksum.Val, logger);

            return n_el;
        }

        internal static int bio_fread_float(Pointer<float> buf, int n_el, BinaryReader fp, int swap, BoxedValueUInt chksum, SphinxLogger logger)
        {
            for (int c = 0; c < n_el; c++)
            {
                uint v = fp.ReadUInt32();

                if (swap != 0)
                    v = ByteOrder.SWAP_INT32(v);

                buf[c] = BitConverter.ToSingle(BitConverter.GetBytes(v), 0);

                if (chksum != null)
                    chksum.Val = chksum_accum(v, chksum.Val, logger);
            }

            return n_el;
        }

        internal static bool bio_fread_int32(out int val, BinaryReader fp, int swap, BoxedValueUInt chksum, SphinxLogger logger)
        {
            val = fp.ReadInt32();

            if (swap != 0)
                val = ByteOrder.SWAP_INT32(val);

            if (chksum != null)
                chksum.Val = chksum_accum(val, chksum.Val, logger);

            return true;
        }

        internal static bool bio_fread_uint32(out uint val, BinaryReader fp, int swap, BoxedValueUInt chksum, SphinxLogger logger)
        {
            val = fp.ReadUInt32();

            if (swap != 0)
                val = ByteOrder.SWAP_INT32(val);

            if (chksum != null)
                chksum.Val = chksum_accum((int)val, chksum.Val, logger);

            return true;
        }

        internal static int bio_fread_1d(BoxedValue<Pointer<byte>> buf, uint el_sz, BoxedValueUInt n_el, FILE fp, int sw, BoxedValueUInt ck, SphinxLogger logger)
        {
            /* Read 1-d array size */
            Pointer<byte> array_size = PointerHelpers.Malloc<byte>(4);
            if (bio_fread(array_size, 4, 1, fp, sw, ck, logger) != 1)
                logger.E_FATAL("fread(arraysize) failed\n");

            n_el.Val = array_size.ReinterpretCast<uint>().Deref;
            if (n_el.Val <= 0)
                logger.E_FATAL(string.Format("Bad arraysize: {0}\n", n_el.Val));

            /* Allocate memory for array data */
            buf.Val = CKDAlloc.ckd_calloc<byte>(n_el.Val * el_sz);

            /* Read array data */
            if (bio_fread(buf.Val, (int)el_sz, (int)n_el.Val, fp, sw, ck, logger) != n_el.Val)
                logger.E_FATAL("fread(arraydata) failed\n");

            return (int)(n_el.Val);
        }

        internal static int bio_fread_3d(BoxedValue<Pointer<Pointer<Pointer<float>>>> arr,
                     BoxedValueUInt d1,
                     BoxedValueUInt d2,
                     BoxedValueUInt d3,
                     FILE fp,
                     uint swap,
                     BoxedValueUInt chksum,
                     SphinxLogger logger)
        {
            MemoryBlock<byte> length_buf = new MemoryBlock<byte>(12);
            Pointer<byte> length = new Pointer<byte>(new BasicMemoryBlockAccess<byte>(length_buf), 0);
            Pointer<uint> l_d1 = new Pointer<uint>(new UpcastingMemoryBlockAccess<uint>(length_buf), 0);
            Pointer<uint> l_d2 = new Pointer<uint>(new UpcastingMemoryBlockAccess<uint>(length_buf), 4);
            Pointer<uint> l_d3 = new Pointer<uint>(new UpcastingMemoryBlockAccess<uint>(length_buf), 8);
            uint n = 0;
            Pointer<byte> raw = PointerHelpers.NULL<byte>();
            uint ret;

            ret = (uint)bio_fread(length.Point(0), 4, 1, fp, (int)swap, chksum, logger);
            if (ret != 1)
            {
                if (ret == 0)
                {
                    logger.E_ERROR_SYSTEM("Unable to read complete data");
                }
                else
                {
                    logger.E_ERROR_SYSTEM("OS error in bio_fread_3d");
                }
                return -1;
            }
            ret = (uint)bio_fread(length.Point(4), 4, 1, fp, (int)swap, chksum, logger);
            if (ret != 1)
            {
                if (ret == 0)
                {
                    logger.E_ERROR_SYSTEM("Unable to read complete data");
                }
                else
                {
                    logger.E_ERROR_SYSTEM("OS error in bio_fread_3d");
                }
                return -1;
            }
            ret = (uint)bio_fread(length.Point(8), 4, 1, fp, (int)swap, chksum, logger);
            if (ret != 1)
            {
                if (ret == 0)
                {
                    logger.E_ERROR_SYSTEM("Unable to read complete data");
                }
                else
                {
                    logger.E_ERROR_SYSTEM("OS error in bio_fread_3d");
                }
                return -1;
            }

            BoxedValue<Pointer<byte>> boxed_raw = new BoxedValue<Pointer<byte>>(raw);
            BoxedValueUInt boxed_n = new BoxedValueUInt(n);
            if (bio_fread_1d(boxed_raw, 4, boxed_n, fp, (int)swap, chksum, logger) != n)
            {
                return -1;
            }
            n = boxed_n.Val;
            raw = boxed_raw.Val;

            SphinxAssert.assert(n == +l_d1 * +l_d2 * +l_d3);

            // LOGAN changed
            // Convert byte data to float
            Pointer<float> float_upcast_buf = raw.ReinterpretCast<float>();
            Pointer<float> float_copy_buf = PointerHelpers.Malloc<float>(n);
            float_upcast_buf.MemCopyTo(float_copy_buf, (int)n);

            arr.Val = CKDAlloc.ckd_alloc_3d_ptr<float>(+l_d1, +l_d2, +l_d3, float_copy_buf);
            d1.Val = +l_d1;
            d2.Val = +l_d2;
            d3.Val = +l_d3;

            return (int)n;
        }

        internal static void bio_verify_chksum(FILE fp, int byteswap, uint chksum, SphinxLogger logger)
        {
            Pointer<byte> file_chksum_array = PointerHelpers.Malloc<byte>(4);
            Pointer<uint> file_chksum = file_chksum_array.ReinterpretCast<uint>();

            if (fp.fread(file_chksum_array, 4, 1) != 1)
                logger.E_FATAL("fread(chksum) failed\n");
            
            if (byteswap != 0)
                ByteOrder.SWAP_INT32(file_chksum);
            if (+file_chksum != chksum)
                logger.E_FATAL
                    (string.Format("Checksum error; file-checksum {0:x8}, computed {1:x8}\n", file_chksum, chksum));
        }

        internal static void bio_verify_chksum(BinaryReader fp, int byteswap, uint chksum, SphinxLogger logger)
        {
            uint file_chksum = 0;
            BoxedValueUInt dummy = new BoxedValueUInt();
            if (!bio_fread_uint32(out file_chksum, fp, byteswap, dummy, logger))
                logger.E_FATAL("fread(chksum) failed\n");

            if (byteswap != 0)
                ByteOrder.SWAP_INT32(file_chksum);
            if (+file_chksum != chksum)
                logger.E_FATAL
                    (string.Format("Checksum error; file-checksum {0:x8}, computed {1:x8}\n", file_chksum, chksum));
        }
    }
}