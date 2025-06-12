using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class MultiStreamSenone
    {
        internal static readonly Pointer<byte> MIXW_PARAM_VERSION = cstring.ToCString("1.0");
        internal static readonly Pointer<byte> SPDEF_PARAM_VERSION = cstring.ToCString("1.2");

        internal static int senone_mgau_map_read(senone_t s, Pointer<byte> file_name, FileAdapter fileAdapter)
        {
            FILE fp;
            int byteswap, chksum_present, n_gauden_present;
            BoxedValueUInt chksum = new BoxedValueUInt();
            int i;
            Pointer<Pointer<byte>> argname;
            Pointer<Pointer<byte>> argval;
            double v;

            s.logger.E_INFO(string.Format("Reading senone gauden-codebook map file: {0}\n", cstring.FromCString(file_name)));

            if ((fp = fileAdapter.fopen(file_name, "rb")) == null)
                s.logger.E_FATAL_SYSTEM(string.Format("Failed to open map file '{0}' for reading", cstring.FromCString(file_name)));

            /* Read header, including argument-value info and 32-bit byteorder magic */
            BoxedValue<Pointer<Pointer<byte>>> boxed_argname = new BoxedValue<Pointer<Pointer<byte>>>();
            BoxedValue<Pointer<Pointer<byte>>> boxed_argval = new BoxedValue<Pointer<Pointer<byte>>>();
            if (BinaryIO.bio_readhdr(fp, boxed_argname, boxed_argval, out byteswap, s.logger) < 0)
                s.logger.E_FATAL(string.Format("Failed to read header from file '{0}'\n", cstring.FromCString(file_name)));
            argname = boxed_argname.Val;
            argval = boxed_argval.Val;
            
            /* Parse argument-value list */
            chksum_present = 0;
            n_gauden_present = 0;
            for (i = 0; argname[i].IsNonNull; i++)
            {
                if (cstring.strcmp(argname[i], cstring.ToCString("version")) == 0)
                {
                    if (cstring.strcmp(argval[i], SPDEF_PARAM_VERSION) != 0)
                    {
                        s.logger.E_WARN(string.Format("Version mismatch({0}): {1}, expecting {2}\n", file_name, argval[i], SPDEF_PARAM_VERSION));
                    }

                    /* HACK!! Convert version# to float32 and take appropriate action */
                    if (stdio.sscanf_f(argval[i], out v) != 1)
                        s.logger.E_FATAL(string.Format("{0}: Bad version no. string: {1}\n", cstring.FromCString(file_name),
                        cstring.FromCString(argval[i])));

                    n_gauden_present = (v > 1.1) ? 1 : 0;
                }
                else if (cstring.strcmp(argname[i], cstring.ToCString("chksum0")) == 0)
                {
                    chksum_present = 1; /* Ignore the associated value */
                }
            }

            argname = argval = PointerHelpers.NULL<Pointer<byte>>();

            chksum.Val = 0;

            /* Read #gauden (if version matches) */
            byte[] read_single_buf = new byte[4];
            Pointer<byte> read_byte_ptr = new Pointer<byte>(read_single_buf);
            Pointer<uint> read_uint_ptr = read_byte_ptr.ReinterpretCast<uint>();

            if (n_gauden_present != 0)
            {
                s.logger.E_INFO(string.Format("Reading number of codebooks from {0}\n", cstring.FromCString(file_name)));
                if (BinaryIO.bio_fread(read_byte_ptr, 4, 1, fp, byteswap, chksum, s.logger) != 1)
                    s.logger.E_FATAL(string.Format("fread({0}) (#gauden) failed\n", cstring.FromCString(file_name)));
                s.n_gauden = read_uint_ptr[0];
            }

            /* Read 1d array data */
            BoxedValueUInt n_el = new BoxedValueUInt(s.n_sen);
            BoxedValue<Pointer<byte>> boxed_ptr = new BoxedValue<Pointer<byte>>();
            if (BinaryIO.bio_fread_1d(boxed_ptr, 4, n_el, fp, byteswap, chksum, s.logger) < 0)
            {
                s.logger.E_FATAL(string.Format("bio_fread_1d({0}) failed\n", cstring.FromCString(file_name)));
            }
            s.n_sen = n_el.Val;
            Pointer<uint> data_as_uint = boxed_ptr.Val.ReinterpretCast<uint>();
            Pointer<uint> native_uint_data = PointerHelpers.Malloc<uint>(s.n_sen);
            data_as_uint.MemCopyTo(native_uint_data, (int)s.n_sen);
            s.mgau = native_uint_data;

            s.logger.E_INFO(string.Format("Mapping {0} senones to {1} codebooks\n", s.n_sen, s.n_gauden));

            /* Infer n_gauden if not present in this version */
            if (n_gauden_present == 0)
            {
                s.n_gauden = 1;
                for (i = 0; i < s.n_sen; i++)
                    if (s.mgau[i] >= s.n_gauden)
                        s.n_gauden = s.mgau[i] + 1;
            }

            if (chksum_present != 0)
                BinaryIO.bio_verify_chksum(fp, byteswap, chksum.Val, s.logger);

            if (fp.fread(read_byte_ptr, 1, 1) == 1)
                s.logger.E_FATAL(string.Format("More data than expected in {0}: {1}\n", cstring.FromCString(file_name), read_single_buf[0]));

            fp.fclose();

            s.logger.E_INFO(string.Format("Read {0}->{1} senone-codebook mappings\n", s.n_sen,
                   s.n_gauden));

            return 1;
        }


        internal static int senone_mixw_read(senone_t s, Pointer<byte> file_name, FileAdapter fileAdapter, logmath_t lmath)
        {
            FILE fp;
            int byteswap, chksum_present;
            BoxedValueUInt chksum = new BoxedValueUInt();
            Pointer<float> pdf;
            int i, f, c, p, n_err;
            Pointer<Pointer<byte>> argname;
            Pointer<Pointer<byte>> argval;

            s.logger.E_INFO(string.Format("Reading senone mixture weights: {0}\n", cstring.FromCString(file_name)));

            if ((fp = fileAdapter.fopen(file_name, "rb")) == null)
                s.logger.E_FATAL_SYSTEM(string.Format("Failed to open mixture weights file '{0}' for reading", cstring.FromCString(file_name)));

            /* Read header, including argument-value info and 32-bit byteorder magic */
            BoxedValue<Pointer<Pointer<byte>>> boxed_argname = new BoxedValue<Pointer<Pointer<byte>>>();
            BoxedValue<Pointer<Pointer<byte>>> boxed_argval = new BoxedValue<Pointer<Pointer<byte>>>();
            if (BinaryIO.bio_readhdr(fp, boxed_argname, boxed_argval, out byteswap, s.logger) < 0)
            {
                s.logger.E_FATAL(string.Format("Failed to read header from file '{0}'\n", cstring.FromCString(file_name)));
            }
            argname = boxed_argname.Val;
            argval = boxed_argval.Val;

            /* Parse argument-value list */
            chksum_present = 0;
            for (i = 0; argname[i].IsNonNull; i++)
            {
                if (cstring.strcmp(argname[i], cstring.ToCString("version")) == 0)
                {
                    if (cstring.strcmp(argval[i], MIXW_PARAM_VERSION) != 0)
                        s.logger.E_WARN(string.Format("Version mismatch({0}): {1}, expecting {2}\n",
                               cstring.FromCString(file_name), cstring.FromCString(argval[i]), cstring.FromCString(MIXW_PARAM_VERSION)));
                }
                else if (cstring.strcmp(argname[i], cstring.ToCString("chksum0")) == 0)
                {
                    chksum_present = 1; /* Ignore the associated value */
                }
            }
            argname = argval = PointerHelpers.NULL<Pointer<byte>>();

            chksum.Val = 0;

            /* Read #senones, #features, #codewords, arraysize */
            byte[] temp_read_buf = new byte[4];
            Pointer<byte> temp_read_ptr_byte = new Pointer<byte>(temp_read_buf);
            Pointer<uint> temp_read_ptr_uint = temp_read_ptr_byte.ReinterpretCast<uint>();

            if ((BinaryIO.bio_fread(temp_read_ptr_byte, 4, 1, fp, byteswap, chksum, s.logger) != 1))
            {
                s.logger.E_FATAL(string.Format("bio_fread({0}) (arraysize) failed\n", cstring.FromCString(file_name)));
            }
            s.n_sen = temp_read_ptr_uint[0];

            if ((BinaryIO.bio_fread(temp_read_ptr_byte, 4, 1, fp, byteswap, chksum, s.logger) != 1))
            {
                s.logger.E_FATAL(string.Format("bio_fread({0}) (arraysize) failed\n", cstring.FromCString(file_name)));
            }
            s.n_feat = temp_read_ptr_uint[0];

            if ((BinaryIO.bio_fread(temp_read_ptr_byte, 4, 1, fp, byteswap, chksum, s.logger) != 1))
            {
                s.logger.E_FATAL(string.Format("bio_fread({0}) (arraysize) failed\n", cstring.FromCString(file_name)));
            }
            s.n_cw = temp_read_ptr_uint[0];

            if ((BinaryIO.bio_fread(temp_read_ptr_byte, 4, 1, fp, byteswap, chksum, s.logger) != 1))
            {
                s.logger.E_FATAL(string.Format("bio_fread({0}) (arraysize) failed\n", cstring.FromCString(file_name)));
            }
            i = (int)temp_read_ptr_uint[0];
            
            if (i != s.n_sen * s.n_feat * s.n_cw)
            {
                s.logger.E_FATAL
                    (string.Format("{0}: #float32s({1}) doesn't match dimensions: {2} x {3} x {4}\n",
                     cstring.FromCString(file_name), i, s.n_sen, s.n_feat, s.n_cw));
            }

            /*
             * Compute #LSB bits to be dropped to represent mixwfloor with 8 bits.
             * All PDF values will be truncated (in the LSB positions) by these many bits.
             */
            if ((s.mixwfloor <= 0.0) || (s.mixwfloor >= 1.0))
                s.logger.E_FATAL(string.Format("mixwfloor ({0}) not in range (0, 1)\n", s.mixwfloor));

            /* Use a fixed shift for compatibility with everything else. */
            s.logger.E_INFO(string.Format("Truncating senone logs3(pdf) values by {0} bits\n", HiddenMarkovModel.SENSCR_SHIFT));

            /*
             * Allocate memory for senone PDF data.  Organize normally or transposed depending on
             * s.n_gauden.
             */
            if (s.n_gauden > 1)
            {
                s.logger.E_INFO("Not transposing mixture weights in memory\n");
                s.pdf = CKDAlloc.ckd_calloc_3d<byte>(s.n_sen, s.n_feat, s.n_cw);
            }
            else
            {
                s.logger.E_INFO("Transposing mixture weights in memory\n");
                s.pdf = CKDAlloc.ckd_calloc_3d<byte>(s.n_feat, s.n_cw, s.n_sen);
            }

            /* Temporary structure to read in floats */
            pdf = CKDAlloc.ckd_calloc<float>(s.n_cw);
            Pointer<byte> bytebuf = PointerHelpers.Malloc<byte>(4 * s.n_cw);
            Pointer<float> floatbuf = bytebuf.ReinterpretCast<float>();

            /* Read senone probs data, normalize, floor, convert to logs3, truncate to 8 bits */
            n_err = 0;
            for (i = 0; i < s.n_sen; i++)
            {
                for (f = 0; f < s.n_feat; f++)
                {
                    if (BinaryIO.bio_fread(bytebuf, 4, (int)s.n_cw, fp, byteswap, chksum, s.logger) != s.n_cw)
                    {
                        s.logger.E_FATAL(string.Format("bio_fread({0}) (arraydata) failed\n", file_name));
                    }
                    floatbuf.MemCopyTo(pdf, (int)s.n_cw);

                    /* Normalize and floor */
                    if (Vector.vector_sum_norm(pdf, checked((int)s.n_cw)) <= 0.0)
                        n_err++;
                    Vector.vector_floor(pdf, checked((int)s.n_cw), s.mixwfloor);
                    Vector.vector_sum_norm(pdf, checked((int)s.n_cw));

                    /* Convert to logs3, truncate to 8 bits, and store in s.pdf */
                    for (c = 0; c < s.n_cw; c++)
                    {
                        p = -(LogMath.logmath_log(lmath, pdf[c]));
                        p += (1 << (HiddenMarkovModel.SENSCR_SHIFT - 1)) - 1; /* Rounding before truncation */

                        if (s.n_gauden > 1)
                        {
                            s.pdf[i][f].Set(c, checked((byte)((p < (255 << HiddenMarkovModel.SENSCR_SHIFT)) ? (p >> HiddenMarkovModel.SENSCR_SHIFT) : 255)));
                        }
                        else
                        {
                            s.pdf[f][c].Set(i, checked((byte)((p < (255 << HiddenMarkovModel.SENSCR_SHIFT)) ? (p >> HiddenMarkovModel.SENSCR_SHIFT) : 255)));
                        }
                    }
                }
            }
            if (n_err > 0)
                s.logger.E_WARN(string.Format("Weight normalization failed for {0} mixture weights components\n", n_err));

            if (chksum_present != 0)
                BinaryIO.bio_verify_chksum(fp, byteswap, chksum.Val, s.logger);

            if (fp.fread(temp_read_ptr_byte, 1, 1) == 1)
                s.logger.E_FATAL(string.Format("More data than expected in {0}\n", cstring.FromCString(file_name)));

            fp.fclose();

            s.logger.E_INFO
                (string.Format("Read mixture weights for {0} senones: {1} features x {2} codewords\n",
                 s.n_sen, s.n_feat, s.n_cw));

            return 1;
        }


        internal static senone_t senone_init(gauden_t g, Pointer<byte> mixwfile, Pointer<byte> sen2mgau_map_file, FileAdapter fileAdapter,
                float mixwfloor, logmath_t lmath, bin_mdef_t mdef, SphinxLogger logger)
        {
            senone_t s;
            int n = 0, i;

            s = new senone_t();
            s.logger = logger;
            s.lmath = LogMath.logmath_init(LogMath.logmath_get_base(lmath), HiddenMarkovModel.SENSCR_SHIFT, 1, logger);
            s.mixwfloor = mixwfloor;

            s.n_gauden = checked((uint)g.n_mgau);
            if (sen2mgau_map_file.IsNonNull)
            {
                if (!(cstring.strcmp(sen2mgau_map_file, cstring.ToCString(".semi.")) == 0
                      || cstring.strcmp(sen2mgau_map_file, cstring.ToCString(".ptm.")) == 0
                      || cstring.strcmp(sen2mgau_map_file, cstring.ToCString(".cont.")) == 0))
                {
                    senone_mgau_map_read(s, sen2mgau_map_file, fileAdapter);
                    n = checked((int)s.n_sen);
                }
            }
            else
            {
                if (s.n_gauden == 1)
                    sen2mgau_map_file = cstring.ToCString(".semi.");
                else if (s.n_gauden == BinaryModelDef.bin_mdef_n_ciphone(mdef))
                    sen2mgau_map_file = cstring.ToCString(".ptm.");
                else
                    sen2mgau_map_file = cstring.ToCString(".cont.");
            }

            senone_mixw_read(s, mixwfile, fileAdapter, lmath);

            if (cstring.strcmp(sen2mgau_map_file, cstring.ToCString(".semi.")) == 0)
            {
                /* All-to-1 senones-codebook mapping */
                logger.E_INFO(string.Format("Mapping all senones to one codebook\n"));
                s.mgau = CKDAlloc.ckd_calloc<uint>(s.n_sen);
            }
            else if (cstring.strcmp(sen2mgau_map_file, cstring.ToCString(".ptm.")) == 0)
            {
                /* All-to-ciphone-id senones-codebook mapping */
                logger.E_INFO(string.Format("Mapping senones to context-independent phone codebooks\n"));
                s.mgau = CKDAlloc.ckd_calloc<uint>(s.n_sen);
                for (i = 0; i < s.n_sen; i++)
                    s.mgau[i] = checked((uint)BinaryModelDef.bin_mdef_sen2cimap(mdef, i));
            }
            else if (cstring.strcmp(sen2mgau_map_file, cstring.ToCString(".cont.")) == 0
                     || cstring.strcmp(sen2mgau_map_file, cstring.ToCString(".s3cont.")) == 0)
            {
                /* 1-to-1 senone-codebook mapping */
                logger.E_INFO("Mapping senones to individual codebooks\n");
                if (s.n_sen <= 1)
                    logger.E_FATAL(string.Format("#senone={0}; must be >1\n", s.n_sen));

                s.mgau = CKDAlloc.ckd_calloc<uint>(s.n_sen);
                for (i = 0; i < s.n_sen; i++)
                    s.mgau[i] = (uint)i;
                /* Not sure why this is here, it probably does nothing. */
                s.n_gauden = s.n_sen;
            }
            else
            {
                if (s.n_sen != n)
                    logger.E_FATAL(string.Format("#senones inconsistent: {0} in {1}; {2} in {3}\n",
                            n, cstring.FromCString(sen2mgau_map_file), s.n_sen, cstring.FromCString(mixwfile)));
            }

            s.featscr = PointerHelpers.NULL<int>();
            return s;
        }

        /*
         * Compute senone score for one senone.
         * NOTE:  Remember that senone PDF tables contain SCALED, NEGATED logs3 values.
         * NOTE:  Remember also that PDF data may be transposed or not depending on s.n_gauden.
         */
        internal static int senone_eval(senone_t s, int id, Pointer<Pointer<gauden_dist_t>> dist, int n_top)
        {
            int scr;                  /* total senone score */
            int fden;                 /* Gaussian density */
            int fscr;                 /* senone score for one feature */
            int fwscr;                /* senone score for one feature, one codeword */
            int f, t;
            int top;
            Pointer<gauden_dist_t> fdist;

            SphinxAssert.assert((id >= 0) && (id < s.n_sen));
            SphinxAssert.assert((n_top > 0) && (n_top <= s.n_cw));

            scr = 0;

            for (f = 0; f < s.n_feat; f++)
            {
                fdist = dist[f];

                /* Top codeword for feature f */
                top = fden = ((int)fdist[0].dist + ((1 << HiddenMarkovModel.SENSCR_SHIFT) - 1)) >> HiddenMarkovModel.SENSCR_SHIFT;
                fscr = (s.n_gauden > 1)
                ? (fden + -s.pdf[id][f][fdist[0].id])  /* untransposed */
                : (fden + -s.pdf[f][fdist[0].id][id]); /* transposed */
                s.logger.E_DEBUG(string.Format("fden[{0}][{1}] l+= {2} + {3} = {4}\n",
                            id, f, -(fscr - fden), -(fden - top), -(fscr - top)));
                /* Remaining of n_top codewords for feature f */
                for (t = 1; t < n_top; t++)
                {
                    fden = ((int)fdist[t].dist + ((1 << HiddenMarkovModel.SENSCR_SHIFT) - 1)) >> HiddenMarkovModel.SENSCR_SHIFT;
                    fwscr = (s.n_gauden > 1) ?
                        (fden + -s.pdf[id][f][fdist[t].id]) :
                        (fden + -s.pdf[f][fdist[t].id][id]);
                    fscr = LogMath.logmath_add(s.lmath, fscr, fwscr);
                    s.logger.E_DEBUG(string.Format("fden[{0}][{1}] l+= {2} + {3} = {4}\n",
                                id, f, -(fwscr - fden), -(fden - top), -(fscr - top)));
                }
                /* Senone scores are also scaled, negated logs3 values.  Hence
                 * we have to negate the stuff we calculated above. */
                scr -= fscr;
            }
            /* Downscale scores. */
            scr /= s.aw;

            /* Avoid overflowing int16 */
            if (scr > 32767)
                scr = 32767;
            if (scr < -32768)
                scr = -32768;
            return scr;
        }
    }
}
