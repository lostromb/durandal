﻿using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Durandal.Common.MathExt.FFT
{
    internal struct cfftp_fctdata_double
    {
        internal int fct;
        internal int tw; // originally these were Complex* pointers, but they just indexed into plan.mem
        internal int tws; // so they have been replaced by basic integer indexes

        internal cfftp_fctdata_double(int fct, int tw, int tws)
        {
            this.fct = fct;
            this.tw = tw;
            this.tws = tws;
        }
    }

#if NET8_0_OR_GREATER
    [System.Runtime.CompilerServices.InlineArray(FFTConstants.NFCT)]
    internal struct cfftp_fctdata_array_double
    {
        internal cfftp_fctdata_double data;
    }
#endif

    internal class PlanComplexPackedFloat64 : IComplex1DFFTPlanFloat64
    {
        internal int length;
        internal int nfct;
        internal PooledBuffer<Complex> mem;
#if NET8_0_OR_GREATER
        internal cfftp_fctdata_array_double fct;
#else
        internal cfftp_fctdata_double[] fct; // [FFTConstants.NFCT]
#endif
        private int _disposed = 0;

        public int Length => length;

        public PlanComplexPackedFloat64(int length)
        {
            if (length == 0)
            {
                throw new ArgumentOutOfRangeException("FFT length must be greater than zero");
            }

            this.length = length;
            this.nfct = 0;
#if !NET8_0_OR_GREATER
            if (this.fct == null) // if it's not an inline struct array...
            {
                this.fct = new cfftp_fctdata_double[FFTConstants.NFCT];
            }
#endif

            for (int i = 0; i < FFTConstants.NFCT; ++i)
            {
                this.fct[i] = new cfftp_fctdata_double(0, 0, 0);
            }

            this.mem = null;
            if (length == 1)
            {
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
                return;
            }

            if (!cfftp_factorize())
            {
                throw new ArithmeticException($"Could not factorize FFT of length {length}");
            }

            int tws = cfftp_twsize();
            this.mem = BufferPool<Complex>.Rent(tws);

            cfftp_comp_twiddle();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PlanComplexPackedFloat64()
        {
            Dispose(false);
        }
#endif
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                mem?.Dispose();
            }
        }

        public void Forward(Span<Complex> c, double fct)
        {
            pass_all(c, fct, -1);
        }

        public void Backward(Span<Complex> c, double fct)
        {
            pass_all(c, fct, 1);
        }

        private bool cfftp_factorize()
        {
            int length = this.length;
            int nfct = 0;

            while ((length % 4) == 0)
            {
                if (nfct >= FFTConstants.NFCT)
                {
                    return false;
                }

                this.fct[nfct++].fct = 4;
                length >>= 2;
            }
            if ((length % 2) == 0)
            {
                length >>= 1;
                // factor 2 should be at the front of the factor list
                if (nfct >= FFTConstants.NFCT)
                {
                    return false;
                }

                this.fct[nfct++].fct = 2;
                FFTIntrinsics.Swap(ref this.fct[0].fct, ref this.fct[nfct - 1].fct);
            }

            int maxl = (int)(Math.Sqrt((double)length)) + 1;
            for (int divisor = 3; (length > 1) && (divisor < maxl); divisor += 2)
            {
                if ((length % divisor) == 0)
                {
                    while ((length % divisor) == 0)
                    {
                        if (nfct >= FFTConstants.NFCT)
                        {
                            return false;
                        }

                        this.fct[nfct++].fct = divisor;
                        length /= divisor;
                    }

                    maxl = (int)(Math.Sqrt((double)length)) + 1;
                }
            }

            if (length > 1)
            {
                this.fct[nfct++].fct = length;
            }

            this.nfct = nfct;
            return true;
        }

        private int cfftp_twsize()
        {
            int twsize = 0, l1 = 1;
            for (int k = 0; k < this.nfct; ++k)
            {
                int ip = this.fct[k].fct, ido = this.length / (l1 * ip);
                twsize += (ip - 1) * (ido - 1);
                if (ip > 11)
                {
                    twsize += ip;
                }

                l1 *= ip;
            }

            return twsize;
        }

        private void cfftp_comp_twiddle()
        {
            int length = this.length;
            Span<double> twid = new double[2 * length];
            FFTIntrinsics.sincos_2pibyn(length, twid);
            int l1 = 1;
            int memofs = 0;
            for (int k = 0; k < this.nfct; ++k)
            {
                int ip = this.fct[k].fct, ido = length / (l1 * ip);
                this.fct[k].tw = memofs;
                Span<Complex> tw = this.mem.Buffer.AsSpan(memofs);
                memofs += (ip - 1) * (ido - 1);
                for (int j = 1; j < ip; ++j)
                {
                    for (int i = 1; i < ido; ++i)
                    {
                        tw[(j - 1) * (ido - 1) + i - 1].Re = twid[2 * j * l1 * i];
                        tw[(j - 1) * (ido - 1) + i - 1].Im = twid[2 * j * l1 * i + 1];
                    }
                }
                if (ip > 11)
                {
                    this.fct[k].tws = memofs;
                    Span<Complex> tws = this.mem.Buffer.AsSpan(memofs);
                    memofs += ip;
                    for (int j = 0; j < ip; ++j)
                    {
                        tws[j].Re = twid[2 * j * l1 * ido];
                        tws[j].Im = twid[2 * j * l1 * ido + 1];
                    }
                }
                l1 *= ip;
            }
        }

        private void pass_all(Span<Complex> c, double fct, int sign)
        {
            if (this.length == 1)
            {
                return;
            }

            int len = this.length;
            int l1 = 1, nf = nfct;
            using (PooledBuffer<Complex> pooledScratch = BufferPool<Complex>.Rent(len))
            {
                Complex[] scratchArray = pooledScratch.Buffer;
                Span<Complex> ch = scratchArray;
                Span<Complex> p1 = c;
                Span<Complex> p2 = ch;

                for (int k1 = 0; k1 < nf; k1++)
                {
                    int ip = this.fct[k1].fct;
                    int l2 = ip * l1;
                    int ido = len / l2;
                    if (ip == 4)
                    {
                        if (sign > 0)
                        {
                            pass4b(ido, l1, p1, p2, this.mem.Buffer.AsSpan(this.fct[k1].tw));
                        }
                        else
                        {
                            pass4f(ido, l1, p1, p2, this.mem.Buffer.AsSpan(this.fct[k1].tw));
                        }
                    }
                    else if (ip == 2)
                    {
                        if (sign > 0)
                        {
                            pass2b(ido, l1, p1, p2, this.mem.Buffer.AsSpan(this.fct[k1].tw));
                        }
                        else
                        {
                            pass2f(ido, l1, p1, p2, this.mem.Buffer.AsSpan(this.fct[k1].tw));
                        }
                    }
                    else if (ip == 3)
                    {
                        if (sign > 0)
                        {
                            pass3b(ido, l1, p1, p2, this.mem.Buffer.AsSpan(this.fct[k1].tw));
                        }
                        else
                        {
                            pass3f(ido, l1, p1, p2, this.mem.Buffer.AsSpan(this.fct[k1].tw));
                        }
                    }
                    else if (ip == 5)
                    {
                        if (sign > 0)
                        {
                            pass5b(ido, l1, p1, p2, this.mem.Buffer.AsSpan(this.fct[k1].tw));
                        }
                        else
                        {
                            pass5f(ido, l1, p1, p2, this.mem.Buffer.AsSpan(this.fct[k1].tw));
                        }
                    }
                    else if (ip == 7)
                    {
                        pass7(ido, l1, p1, p2, this.mem.Buffer.AsSpan(this.fct[k1].tw), sign);
                    }
                    else if (ip == 11)
                    {
                        pass11(ido, l1, p1, p2, this.mem.Buffer.AsSpan(this.fct[k1].tw), sign);
                    }
                    else
                    {
                        passg(ido, ip, l1, p1, p2, this.mem.Buffer.AsSpan(this.fct[k1].tw), this.mem.Buffer.AsSpan(this.fct[k1].tws), sign);
                        {
                            Span<Complex> tmp = p1;
                            p1 = p2;
                            p2 = tmp;
                        }
                    }

                    {
                        Span<Complex> tmp = p1;
                        p1 = p2;
                        p2 = tmp;
                    }
                    l1 = l2;
                }

                if (!FFTIntrinsics.SpanRefEquals(p1, c))
                {
                    if (fct != 1.0)
                    {
                        FFTIntrinsics.ScaleSpan(ch.Slice(0, len), c.Slice(0, len), fct);
                    }
                    else
                    {
                        p1.Slice(0, len).CopyTo(c);
                    }
                }
                else
                {
                    if (fct != 1.0)
                    {
                        FFTIntrinsics.ScaleSpanInPlace(c.Slice(0, len), fct);
                    }
                }
            }
        }

        private static void pass2b(int ido, int l1, Span<Complex> cc, Span<Complex> ch, Span<Complex> wa)
        {
            const int cdim = 2;

            if (ido == 1)
            {
                for (int k = 0; k < l1; ++k)
                {
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (0))],
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref cc[(0) + ido * ((0) + cdim * (k))],
                        ref cc[(0) + ido * ((1) + cdim * (k))]);
                }
            }
            else
            {
                for (int k = 0; k < l1; ++k)
                {
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (0))],
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref cc[(0) + ido * ((0) + cdim * (k))],
                        ref cc[(0) + ido * ((1) + cdim * (k))]);

                    for (int i = 1; i < ido; ++i)
                    {
                        Complex t = default;
                        FFTIntrinsics.PMC(
                            ref ch[(i) + ido * ((k) + l1 * (0))],
                            ref t,
                            ref cc[(i) + ido * ((0) + cdim * (k))],
                            ref cc[(i) + ido * ((1) + cdim * (k))]);
                        FFTIntrinsics.A_EQ_B_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (1))],
                            ref wa[(i) - 1 + (0) * (ido - 1)],
                            ref t);
                    }
                }
            }
        }

        private static void pass2f(int ido, int l1, Span<Complex> cc, Span<Complex> ch, Span<Complex> wa)
        {
            const int cdim = 2;

            if (ido == 1)
            {
                for (int k = 0; k < l1; ++k)
                {
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (0))],
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref cc[(0) + ido * ((0) + cdim * (k))],
                        ref cc[(0) + ido * ((1) + cdim * (k))]);
                }
            }
            else
            {
                for (int k = 0; k < l1; ++k)
                {
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (0))],
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref cc[(0) + ido * ((0) + cdim * (k))],
                        ref cc[(0) + ido * ((1) + cdim * (k))]);

                    for (int i = 1; i < ido; ++i)
                    {
                        Complex t = default;
                        FFTIntrinsics.PMC(
                            ref ch[(i) + ido * ((k) + l1 * (0))],
                            ref t,
                            ref cc[(i) + ido * ((0) + cdim * (k))],
                            ref cc[(i) + ido * ((1) + cdim * (k))]);
                        FFTIntrinsics.A_EQ_CB_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (1))],
                            ref wa[(i) - 1 + (0) * (ido - 1)],
                            ref t);
                    }
                }
            }
        }

        private static void pass3b(int ido, int l1, Span<Complex> cc, Span<Complex> ch, Span<Complex> wa)
        {
            const int cdim = 3;
            const double tw1r = -0.5, tw1i = 0.86602540378443864676;
            if (ido == 1)
            {
                for (int k = 0; k < l1; ++k)
                {
                    Complex t0 = cc[(0) + ido * ((0) + cdim * (k))],
                        t1 = default, t2 = default,
                        ca = default, cb = default;
                    FFTIntrinsics.PMC(
                        ref t1,
                        ref t2,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((2) + cdim * (k))]);
                    FFTIntrinsics.ADDC(ref ch[(0) + ido * ((k) + l1 * (0))], ref t0, ref t1);
                    FFTIntrinsics.ADDCSCALED(ref ca, ref t0, tw1r, ref t1);
                    FFTIntrinsics.CPROJECT(ref cb, tw1i, ref t2);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref ca,
                        ref cb);
                }
            }
            else
            {
                for (int k = 0; k < l1; ++k)
                {
                    Complex t0 = cc[(0) + ido * ((0) + cdim * (k))],
                        t1 = default, t2 = default,
                        ca = default, cb = default,
                        da = default, db = default;
                    FFTIntrinsics.PMC(
                        ref t1,
                        ref t2,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((2) + cdim * (k))]);
                    FFTIntrinsics.ADDC(ref ch[(0) + ido * ((k) + l1 * (0))], ref t0, ref t1);
                    FFTIntrinsics.ADDCSCALED(ref ca, ref t0, tw1r, ref t1);
                    FFTIntrinsics.CPROJECT(ref cb, tw1i, ref t2);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref ca,
                        ref cb);

                    for (int i = 1; i < ido; ++i)
                    {
                        t0 = cc[(i) + ido * ((0) + cdim * (k))];
                        FFTIntrinsics.PMC(
                            ref t1,
                            ref t2,
                            ref cc[(i) + ido * ((1) + cdim * (k))],
                            ref cc[(i) + ido * ((2) + cdim * (k))]);
                        FFTIntrinsics.ADDC(ref ch[(i) + ido * ((k) + l1 * (0))], ref t0, ref t1);
                        FFTIntrinsics.ADDCSCALED(ref ca, ref t0, tw1r, ref t1);
                        FFTIntrinsics.CPROJECT(ref cb, tw1i, ref t2);
                        FFTIntrinsics.PMC(ref da, ref db, ref ca, ref cb);
                        FFTIntrinsics.A_EQ_B_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (1))],
                            ref wa[(i) - 1 + (1 - 1) * (ido - 1)],
                            ref da);
                        FFTIntrinsics.A_EQ_B_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (2))],
                            ref wa[(i) - 1 + (2 - 1) * (ido - 1)],
                            ref db);
                    }
                }
            }
        }

        private static void pass3f(int ido, int l1, Span<Complex> cc, Span<Complex> ch, Span<Complex> wa)
        {
            const int cdim = 3;
            const double tw1r = -0.5, tw1i = -0.86602540378443864676;

            if (ido == 1)
            {
                for (int k = 0; k < l1; ++k)
                {
                    Complex t0 = cc[(0) + ido * ((0) + cdim * (k))],
                        t1 = default, t2 = default,
                        ca = default, cb = default;
                    FFTIntrinsics.PMC(
                        ref t1,
                        ref t2,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((2) + cdim * (k))]);
                    FFTIntrinsics.ADDC(ref ch[(0) + ido * ((k) + l1 * (0))], ref t0, ref t1);
                    FFTIntrinsics.ADDCSCALED(ref ca, ref t0, tw1r, ref t1);
                    FFTIntrinsics.CPROJECT(ref cb, tw1i, ref t2);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref ca,
                        ref cb);
                }
            }
            else
            {
                for (int k = 0; k < l1; ++k)
                {
                    Complex t0 = cc[(0) + ido * ((0) + cdim * (k))],
                        t1 = default, t2 = default,
                        ca = default, cb = default,
                        da = default, db = default;

                    FFTIntrinsics.PMC(
                        ref t1,
                        ref t2,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((2) + cdim * (k))]);
                    FFTIntrinsics.ADDC(ref ch[(0) + ido * ((k) + l1 * (0))], ref t0, ref t1);
                    FFTIntrinsics.ADDCSCALED(ref ca, ref t0, tw1r, ref t1);
                    FFTIntrinsics.CPROJECT(ref cb, tw1i, ref t2);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref ca,
                        ref cb);

                    for (int i = 1; i < ido; ++i)
                    {
                        t0 = cc[(i) + ido * ((0) + cdim * (k))];
                        FFTIntrinsics.PMC(
                            ref t1,
                            ref t2,
                            ref cc[(i) + ido * ((1) + cdim * (k))],
                            ref cc[(i) + ido * ((2) + cdim * (k))]);
                        FFTIntrinsics.ADDC(ref ch[(i) + ido * ((k) + l1 * (0))], ref t0, ref t1);
                        FFTIntrinsics.ADDCSCALED(ref ca, ref t0, tw1r, ref t1);
                        FFTIntrinsics.CPROJECT(ref cb, tw1i, ref t2);
                        FFTIntrinsics.PMC(ref da, ref db, ref ca, ref cb);
                        FFTIntrinsics.A_EQ_CB_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (1))],
                            ref wa[(i) - 1 + (1 - 1) * (ido - 1)],
                            ref da);
                        FFTIntrinsics.A_EQ_CB_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (2))],
                            ref wa[(i) - 1 + (2 - 1) * (ido - 1)],
                            ref db);
                    }
                }
            }
        }

        private static void pass4b(int ido, int l1, Span<Complex> cc, Span<Complex> ch, Span<Complex> wa)
        {
            const int cdim = 4;

            if (ido == 1)
            {
                for (int k = 0; k < l1; ++k)
                {
                    Complex t1 = default, t2 = default, t3 = default, t4 = default;
                    FFTIntrinsics.PMC(
                        ref t2,
                        ref t1,
                        ref cc[(0) + ido * ((0) + cdim * (k))],
                        ref cc[(0) + ido * ((2) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t3,
                        ref t4,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((3) + cdim * (k))]);
                    FFTIntrinsics.ROT90(ref t4);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (0))],
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref t2,
                        ref t3);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (3))],
                        ref t1,
                        ref t4);
                }
            }
            else
            {
                for (int k = 0; k < l1; ++k)
                {
                    Complex t1 = default, t2 = default, t3 = default, t4 = default;
                    FFTIntrinsics.PMC(
                        ref t2,
                        ref t1,
                        ref cc[(0) + ido * ((0) + cdim * (k))],
                        ref cc[(0) + ido * ((2) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t3,
                        ref t4,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((3) + cdim * (k))]);
                    FFTIntrinsics.ROT90(ref t4);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (0))],
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref t2,
                        ref t3);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (3))],
                        ref t1,
                        ref t4);

                    for (int i = 1; i < ido; ++i)
                    {
                        Complex c2 = default, c3 = default, c4 = default;
                        Complex cc0 = cc[(i) + ido * ((0) + cdim * (k))],
                            cc1 = cc[(i) + ido * ((1) + cdim * (k))],
                            cc2 = cc[(i) + ido * ((2) + cdim * (k))],
                            cc3 = cc[(i) + ido * ((3) + cdim * (k))];
                        FFTIntrinsics.PMC(ref t2, ref t1, ref cc0, ref cc2);
                        FFTIntrinsics.PMC(ref t3, ref t4, ref cc1, ref cc3);
                        FFTIntrinsics.ROT90(ref t4);
                        Complex wa0 = wa[(i) - 1 + (0) * (ido - 1)],
                            wa1 = wa[(i) - 1 + (1) * (ido - 1)],
                            wa2 = wa[(i) - 1 + (2) * (ido - 1)];
                        FFTIntrinsics.PMC(ref ch[(i) + ido * ((k) + l1 * (0))], ref c3, ref t2, ref t3);
                        FFTIntrinsics.PMC(ref c2, ref c4, ref t1, ref t4);
                        FFTIntrinsics.A_EQ_B_MUL_C(ref ch[(i) + ido * ((k) + l1 * (1))], ref wa0, ref c2);
                        FFTIntrinsics.A_EQ_B_MUL_C(ref ch[(i) + ido * ((k) + l1 * (2))], ref wa1, ref c3);
                        FFTIntrinsics.A_EQ_B_MUL_C(ref ch[(i) + ido * ((k) + l1 * (3))], ref wa2, ref c4);
                    }
                }
            }
        }

        private static void pass4f(int ido, int l1, Span<Complex> cc, Span<Complex> ch, Span<Complex> wa)
        {
            const int cdim = 4;

            if (ido == 1)
            {
                for (int k = 0; k < l1; ++k)
                {
                    Complex t1 = default, t2 = default, t3 = default, t4 = default;
                    FFTIntrinsics.PMC(
                        ref t2,
                        ref t1,
                        ref cc[(0) + ido * ((0) + cdim * (k))],
                        ref cc[(0) + ido * ((2) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t3,
                        ref t4,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((3) + cdim * (k))]);
                    FFTIntrinsics.ROTM90(ref t4);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (0))],
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref t2,
                        ref t3);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (3))],
                        ref t1,
                        ref t4);
                }
            }
            else
            {
                for (int k = 0; k < l1; ++k)
                {
                    Complex t1 = default, t2 = default, t3 = default, t4 = default;
                    FFTIntrinsics.PMC(
                        ref t2,
                        ref t1,
                        ref cc[(0) + ido * ((0) + cdim * (k))],
                        ref cc[(0) + ido * ((2) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t3,
                        ref t4,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((3) + cdim * (k))]);
                    FFTIntrinsics.ROTM90(ref t4);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (0))],
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref t2,
                        ref t3);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (3))],
                        ref t1,
                        ref t4);

                    for (int i = 1; i < ido; ++i)
                    {
                        Complex c2 = default, c3 = default, c4 = default;
                        Complex cc0 = cc[(i) + ido * ((0) + cdim * (k))],
                            cc1 = cc[(i) + ido * ((1) + cdim * (k))],
                            cc2 = cc[(i) + ido * ((2) + cdim * (k))],
                            cc3 = cc[(i) + ido * ((3) + cdim * (k))];
                        FFTIntrinsics.PMC(ref t2, ref t1, ref cc0, ref cc2);
                        FFTIntrinsics.PMC(ref t3, ref t4, ref cc1, ref cc3);
                        FFTIntrinsics.ROTM90(ref t4);
                        Complex wa0 = wa[(i) - 1 + (0) * (ido - 1)],
                            wa1 = wa[(i) - 1 + (1) * (ido - 1)],
                            wa2 = wa[(i) - 1 + (2) * (ido - 1)];
                        FFTIntrinsics.PMC(ref ch[(i) + ido * ((k) + l1 * (0))], ref c3, ref t2, ref t3);
                        FFTIntrinsics.PMC(ref c2, ref c4, ref t1, ref t4);
                        FFTIntrinsics.A_EQ_CB_MUL_C(ref ch[(i) + ido * ((k) + l1 * (1))], ref wa0, ref c2);
                        FFTIntrinsics.A_EQ_CB_MUL_C(ref ch[(i) + ido * ((k) + l1 * (2))], ref wa1, ref c3);
                        FFTIntrinsics.A_EQ_CB_MUL_C(ref ch[(i) + ido * ((k) + l1 * (3))], ref wa2, ref c4);
                    }
                }
            }
        }

        private static void pass5b(int ido, int l1, Span<Complex> cc, Span<Complex> ch, Span<Complex> wa)
        {
            const int cdim = 5;
            const double tw1r = 0.3090169943749474241,
                tw1i = 0.95105651629515357212,
                tw2r = -0.8090169943749474241,
                tw2i = 0.58778525229247312917;

            if (ido == 1)
            {
                for (int k = 0; k < l1; ++k)
                {
                    Complex t0 = cc[(0) + ido * ((0) + cdim * (k))], t1 = default, t2 = default, t3 = default, t4 = default;
                    Complex ca = default, cb = default;
                    FFTIntrinsics.PMC(
                        ref t1,
                        ref t4,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((4) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t2,
                        ref t3,
                        ref cc[(0) + ido * ((2) + cdim * (k))],
                        ref cc[(0) + ido * ((3) + cdim * (k))]);
                    ref Complex z = ref ch[(0) + ido * ((k) + l1 * (0))];
                    z.Re = t0.Re + t1.Re + t2.Re;
                    z.Im = t0.Im + t1.Im + t2.Im;

                    ca.Re = t0.Re + tw1r * t1.Re + tw2r * t2.Re;
                    ca.Im = t0.Im + tw1r * t1.Im + tw2r * t2.Im;
                    cb.Im = +tw1i * t4.Re + tw2i * t3.Re;
                    cb.Re = -(+tw1i * t4.Im + tw2i * t3.Im);

                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (4))],
                        ref ca,
                        ref cb);

                    ca.Re = t0.Re + tw2r * t1.Re + tw1r * t2.Re;
                    ca.Im = t0.Im + tw2r * t1.Im + tw1r * t2.Im;
                    cb.Im = +tw2i * t4.Re - tw1i * t3.Re;
                    cb.Re = -(+tw2i * t4.Im - tw1i * t3.Im);

                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref ch[(0) + ido * ((k) + l1 * (3))],
                        ref ca,
                        ref cb);
                }
            }
            else
            {
                for (int k = 0; k < l1; ++k)
                {
                    Complex t0 = cc[(0) + ido * ((0) + cdim * (k))], t1 = default, t2 = default, t3 = default, t4 = default;
                    Complex ca = default, cb = default;
                    FFTIntrinsics.PMC(
                        ref t1,
                        ref t4,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((4) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t2,
                        ref t3,
                        ref cc[(0) + ido * ((2) + cdim * (k))],
                        ref cc[(0) + ido * ((3) + cdim * (k))]);
                    ref Complex z = ref ch[(0) + ido * ((k) + l1 * (0))];
                    z.Re = t0.Re + t1.Re + t2.Re;
                    z.Im = t0.Im + t1.Im + t2.Im;

                    ca.Re = t0.Re + tw1r * t1.Re + tw2r * t2.Re;
                    ca.Im = t0.Im + tw1r * t1.Im + tw2r * t2.Im;
                    cb.Im = tw1i * t4.Re + tw2i * t3.Re;
                    cb.Re = -(tw1i * t4.Im + tw2i * t3.Im);

                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (4))],
                        ref ca,
                        ref cb);

                    ca.Re = t0.Re + tw2r * t1.Re + tw1r * t2.Re;
                    ca.Im = t0.Im + tw2r * t1.Im + tw1r * t2.Im;
                    cb.Im = tw2i * t4.Re - tw1i * t3.Re;
                    cb.Re = -(tw2i * t4.Im - tw1i * t3.Im);

                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref ch[(0) + ido * ((k) + l1 * (3))],
                        ref ca,
                        ref cb);

                    for (int i = 1; i < ido; ++i)
                    {
                        Complex da = default, db = default;
                        t0 = cc[(i) + ido * ((0) + cdim * (k))];
                        FFTIntrinsics.PMC(
                            ref t1,
                            ref t4,
                            ref cc[(i) + ido * ((1) + cdim * (k))],
                            ref cc[(i) + ido * ((4) + cdim * (k))]);
                        FFTIntrinsics.PMC(
                            ref t2,
                            ref t3,
                            ref cc[(i) + ido * ((2) + cdim * (k))],
                            ref cc[(i) + ido * ((3) + cdim * (k))]);
                        z = ref ch[(i) + ido * ((k) + l1 * (0))];
                        z.Re = t0.Re + t1.Re + t2.Re;
                        z.Im = t0.Im + t1.Im + t2.Im;
                        ca.Re = t0.Re + tw1r * t1.Re + tw2r * t2.Re;
                        ca.Im = t0.Im + tw1r * t1.Im + tw2r * t2.Im;
                        cb.Im = tw1i * t4.Re + tw2i * t3.Re;
                        cb.Re = -(tw1i * t4.Im + tw2i * t3.Im);
                        FFTIntrinsics.PMC(ref da, ref db, ref ca, ref cb);
                        FFTIntrinsics.A_EQ_B_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (1))],
                            ref wa[(i) - 1 + (1 - 1) * (ido - 1)],
                            ref da);
                        FFTIntrinsics.A_EQ_B_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (4))],
                            ref wa[(i) - 1 + (4 - 1) * (ido - 1)],
                            ref db);
                        ca.Re = t0.Re + tw2r * t1.Re + tw1r * t2.Re;
                        ca.Im = t0.Im + tw2r * t1.Im + tw1r * t2.Im;
                        cb.Im = tw2i * t4.Re - tw1i * t3.Re;
                        cb.Re = -(tw2i * t4.Im - tw1i * t3.Im);
                        FFTIntrinsics.PMC(ref da, ref db, ref ca, ref cb);
                        FFTIntrinsics.A_EQ_B_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (2))],
                            ref wa[(i) - 1 + (2 - 1) * (ido - 1)],
                            ref da);
                        FFTIntrinsics.A_EQ_B_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (3))],
                            ref wa[(i) - 1 + (3 - 1) * (ido - 1)],
                            ref db);
                    }
                }
            }
        }

        static void pass5f(int ido, int l1, Span<Complex> cc, Span<Complex> ch, Span<Complex> wa)
        {
            const int cdim = 5;
            const double tw1r = 0.3090169943749474241,
                tw1i = -0.95105651629515357212,
                tw2r = -0.8090169943749474241,
                tw2i = -0.58778525229247312917;

            if (ido == 1)
            {
                for (int k = 0; k < l1; ++k)
                {
                    Complex t0 = cc[(0) + ido * ((0) + cdim * (k))], t1 = default, t2 = default, t3 = default, t4 = default;
                    Complex ca = default, cb = default;
                    FFTIntrinsics.PMC(
                        ref t1,
                        ref t4,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((4) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t2,
                        ref t3,
                        ref cc[(0) + ido * ((2) + cdim * (k))],
                        ref cc[(0) + ido * ((3) + cdim * (k))]);
                    ref Complex z = ref ch[(0) + ido * ((k) + l1 * (0))];
                    z.Re = t0.Re + t1.Re + t2.Re;
                    z.Im = t0.Im + t1.Im + t2.Im;

                    ca.Re = t0.Re + tw1r * t1.Re + tw2r * t2.Re;
                    ca.Im = t0.Im + tw1r * t1.Im + tw2r * t2.Im;
                    cb.Im = +tw1i * t4.Re + tw2i * t3.Re;
                    cb.Re = -(+tw1i * t4.Im + tw2i * t3.Im);

                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (4))],
                        ref ca,
                        ref cb);

                    ca.Re = t0.Re + tw2r * t1.Re + tw1r * t2.Re;
                    ca.Im = t0.Im + tw2r * t1.Im + tw1r * t2.Im;
                    cb.Im = +tw2i * t4.Re - tw1i * t3.Re;
                    cb.Re = -(+tw2i * t4.Im - tw1i * t3.Im);

                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref ch[(0) + ido * ((k) + l1 * (3))],
                        ref ca,
                        ref cb);
                }
            }
            else
            {
                for (int k = 0; k < l1; ++k)
                {
                    Complex t0 = cc[(0) + ido * ((0) + cdim * (k))], t1 = default, t2 = default, t3 = default, t4 = default;
                    Complex ca = default, cb = default;
                    FFTIntrinsics.PMC(
                        ref t1,
                        ref t4,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((4) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t2,
                        ref t3,
                        ref cc[(0) + ido * ((2) + cdim * (k))],
                        ref cc[(0) + ido * ((3) + cdim * (k))]);
                    ref Complex z = ref ch[(0) + ido * ((k) + l1 * (0))];
                    z.Re = t0.Re + t1.Re + t2.Re;
                    z.Im = t0.Im + t1.Im + t2.Im;

                    ca.Re = t0.Re + tw1r * t1.Re + tw2r * t2.Re;
                    ca.Im = t0.Im + tw1r * t1.Im + tw2r * t2.Im;
                    cb.Im = tw1i * t4.Re + tw2i * t3.Re;
                    cb.Re = -(tw1i * t4.Im + tw2i * t3.Im);

                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (4))],
                        ref ca,
                        ref cb);

                    ca.Re = t0.Re + tw2r * t1.Re + tw1r * t2.Re;
                    ca.Im = t0.Im + tw2r * t1.Im + tw1r * t2.Im;
                    cb.Im = tw2i * t4.Re - tw1i * t3.Re;
                    cb.Re = -(tw2i * t4.Im - tw1i * t3.Im);

                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref ch[(0) + ido * ((k) + l1 * (3))],
                        ref ca,
                        ref cb);

                    for (int i = 1; i < ido; ++i)
                    {
                        Complex da = default, db = default;
                        t0 = cc[(i) + ido * ((0) + cdim * (k))];
                        FFTIntrinsics.PMC(
                            ref t1,
                            ref t4,
                            ref cc[(i) + ido * ((1) + cdim * (k))],
                            ref cc[(i) + ido * ((4) + cdim * (k))]);
                        FFTIntrinsics.PMC(
                            ref t2,
                            ref t3,
                            ref cc[(i) + ido * ((2) + cdim * (k))],
                            ref cc[(i) + ido * ((3) + cdim * (k))]);
                        z = ref ch[(i) + ido * ((k) + l1 * (0))];
                        z.Re = t0.Re + t1.Re + t2.Re;
                        z.Im = t0.Im + t1.Im + t2.Im;
                        ca.Re = t0.Re + tw1r * t1.Re + tw2r * t2.Re;
                        ca.Im = t0.Im + tw1r * t1.Im + tw2r * t2.Im;

                        cb.Im = tw1i * t4.Re + tw2i * t3.Re;
                        cb.Re = -(tw1i * t4.Im + tw2i * t3.Im);
                        FFTIntrinsics.PMC(ref da, ref db, ref ca, ref cb);
                        FFTIntrinsics.A_EQ_CB_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (1))],
                            ref wa[(i) - 1 + (1 - 1) * (ido - 1)],
                            ref da);
                        FFTIntrinsics.A_EQ_CB_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (4))],
                            ref wa[(i) - 1 + (4 - 1) * (ido - 1)],
                            ref db);
                        ca.Re = t0.Re + tw2r * t1.Re + tw1r * t2.Re;
                        ca.Im = t0.Im + tw2r * t1.Im + tw1r * t2.Im;

                        cb.Im = tw2i * t4.Re - tw1i * t3.Re;
                        cb.Re = -(tw2i * t4.Im - tw1i * t3.Im);
                        FFTIntrinsics.PMC(ref da, ref db, ref ca, ref cb);
                        FFTIntrinsics.A_EQ_CB_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (2))],
                            ref wa[(i) - 1 + (2 - 1) * (ido - 1)],
                            ref da);
                        FFTIntrinsics.A_EQ_CB_MUL_C(
                            ref ch[(i) + ido * ((k) + l1 * (3))],
                            ref wa[(i) - 1 + (3 - 1) * (ido - 1)],
                            ref db);
                    }
                }
            }
        }

        private static void pass7(int ido, int l1, Span<Complex> cc, Span<Complex> ch, Span<Complex> wa, int sign)
        {
            const int cdim = 7;
            double tw1r = 0.623489801858733530525,
                tw1i = sign * 0.7818314824680298087084,
                tw2r = -0.222520933956314404289,
                tw2i = sign * 0.9749279121818236070181,
                tw3r = -0.9009688679024191262361,
                tw3i = sign * 0.4338837391175581204758;

            Complex t1 = default, t2 = default, t3 = default, t4 = default, t5 = default, t6 = default, t7 = default;
            Complex ca = default, cb = default, da = default, db = default;

            if (ido == 1)
            {
                for (int k = 0; k < l1; ++k)
                {
                    t1 = cc[(0) + ido * ((0) + cdim * (k))];
                    FFTIntrinsics.PMC(
                        ref t2,
                        ref t7,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((6) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t3,
                        ref t6,
                        ref cc[(0) + ido * ((2) + cdim * (k))],
                        ref cc[(0) + ido * ((5) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t4,
                        ref t5,
                        ref cc[(0) + ido * ((3) + cdim * (k))],
                        ref cc[(0) + ido * ((4) + cdim * (k))]);
                    ref Complex z = ref ch[(0) + ido * ((k) + l1 * (0))];
                    z.Re = t1.Re + t2.Re + t3.Re + t4.Re;
                    z.Im = t1.Im + t2.Im + t3.Im + t4.Im;
                    ca.Re = t1.Re + tw1r * t2.Re + tw2r * t3.Re + tw3r * t4.Re;
                    ca.Im = t1.Im + tw1r * t2.Im + tw2r * t3.Im + tw3r * t4.Im;
                    cb.Im = +tw1i * t7.Re + tw2i * t6.Re + tw3i * t5.Re;
                    cb.Re = -(+tw1i * t7.Im + tw2i * t6.Im + tw3i * t5.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (6))],
                        ref ca,
                        ref cb);
                    ca.Re = t1.Re + tw2r * t2.Re + tw3r * t3.Re + tw1r * t4.Re;
                    ca.Im = t1.Im + tw2r * t2.Im + tw3r * t3.Im + tw1r * t4.Im;
                    cb.Im = +tw2i * t7.Re - tw3i * t6.Re - tw1i * t5.Re;
                    cb.Re = -(+tw2i * t7.Im - tw3i * t6.Im - tw1i * t5.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref ch[(0) + ido * ((k) + l1 * (5))],
                        ref ca,
                        ref cb);
                    ca.Re = t1.Re + tw3r * t2.Re + tw1r * t3.Re + tw2r * t4.Re;
                    ca.Im = t1.Im + tw3r * t2.Im + tw1r * t3.Im + tw2r * t4.Im;
                    cb.Im = +tw3i * t7.Re - tw1i * t6.Re + tw2i * t5.Re;
                    cb.Re = -(+tw3i * t7.Im - tw1i * t6.Im + tw2i * t5.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (3))],
                        ref ch[(0) + ido * ((k) + l1 * (4))],
                        ref ca,
                        ref cb);
                }
            }
            else
            {
                for (int k = 0; k < l1; ++k)
                {
                    t1 = cc[(0) + ido * ((0) + cdim * (k))];
                    FFTIntrinsics.PMC(
                        ref t2,
                        ref t7,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((6) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t3,
                        ref t6,
                        ref cc[(0) + ido * ((2) + cdim * (k))],
                        ref cc[(0) + ido * ((5) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t4,
                        ref t5,
                        ref cc[(0) + ido * ((3) + cdim * (k))],
                        ref cc[(0) + ido * ((4) + cdim * (k))]);
                    ref Complex z = ref ch[(0) + ido * ((k) + l1 * (0))];
                    z.Re = t1.Re + t2.Re + t3.Re + t4.Re;
                    z.Im = t1.Im + t2.Im + t3.Im + t4.Im;
                    ca.Re = t1.Re + tw1r * t2.Re + tw2r * t3.Re + tw3r * t4.Re;
                    ca.Im = t1.Im + tw1r * t2.Im + tw2r * t3.Im + tw3r * t4.Im;
                    cb.Im = +tw1i * t7.Re + tw2i * t6.Re + tw3i * t5.Re;
                    cb.Re = -(+tw1i * t7.Im + tw2i * t6.Im + tw3i * t5.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (6))],
                        ref ca,
                        ref cb);
                    ca.Re = t1.Re + tw2r * t2.Re + tw3r * t3.Re + tw1r * t4.Re;
                    ca.Im = t1.Im + tw2r * t2.Im + tw3r * t3.Im + tw1r * t4.Im;
                    cb.Im = +tw2i * t7.Re - tw3i * t6.Re - tw1i * t5.Re;
                    cb.Re = -(+tw2i * t7.Im - tw3i * t6.Im - tw1i * t5.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref ch[(0) + ido * ((k) + l1 * (5))],
                        ref ca,
                        ref cb);
                    ca.Re = t1.Re + tw3r * t2.Re + tw1r * t3.Re + tw2r * t4.Re;
                    ca.Im = t1.Im + tw3r * t2.Im + tw1r * t3.Im + tw2r * t4.Im;
                    cb.Im = +tw3i * t7.Re - tw1i * t6.Re + tw2i * t5.Re;
                    cb.Re = -(+tw3i * t7.Im - tw1i * t6.Im + tw2i * t5.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (3))],
                        ref ch[(0) + ido * ((k) + l1 * (4))],
                        ref ca,
                        ref cb);

                    for (int i = 1; i < ido; ++i)
                    {
                        t1 = cc[(i) + ido * ((0) + cdim * (k))];
                        FFTIntrinsics.PMC(
                            ref t2,
                            ref t7,
                            ref cc[(i) + ido * ((1) + cdim * (k))],
                            ref cc[(i) + ido * ((6) + cdim * (k))]);
                        FFTIntrinsics.PMC(
                            ref t3,
                            ref t6,
                            ref cc[(i) + ido * ((2) + cdim * (k))],
                            ref cc[(i) + ido * ((5) + cdim * (k))]);
                        FFTIntrinsics.PMC(
                            ref t4,
                            ref t5,
                            ref cc[(i) + ido * ((3) + cdim * (k))],
                            ref cc[(i) + ido * ((4) + cdim * (k))]);
                        z = ref ch[(i) + ido * ((k) + l1 * (0))];
                        z.Re = t1.Re + t2.Re + t3.Re + t4.Re;
                        z.Im = t1.Im + t2.Im + t3.Im + t4.Im;
                        ca.Re = t1.Re + tw1r * t2.Re + tw2r * t3.Re + tw3r * t4.Re;
                        ca.Im = t1.Im + tw1r * t2.Im + tw2r * t3.Im + tw3r * t4.Im;
                        cb.Im = +tw1i * t7.Re + tw2i * t6.Re + tw3i * t5.Re;
                        cb.Re = -(+tw1i * t7.Im + tw2i * t6.Im + tw3i * t5.Im);
                        da.Re = ca.Re + cb.Re;
                        da.Im = ca.Im + cb.Im;
                        db.Re = ca.Re - cb.Re;
                        db.Im = ca.Im - cb.Im;
                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (1))],
                            ref wa[(i) - 1 + (1 - 1) * (ido - 1)],
                            ref da,
                            sign);
                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (6))],
                            ref wa[(i) - 1 + (6 - 1) * (ido - 1)],
                            ref db,
                            sign);
                        ca.Re = t1.Re + tw2r * t2.Re + tw3r * t3.Re + tw1r * t4.Re;
                        ca.Im = t1.Im + tw2r * t2.Im + tw3r * t3.Im + tw1r * t4.Im;
                        cb.Im = +tw2i * t7.Re - tw3i * t6.Re - tw1i * t5.Re;
                        cb.Re = -(+tw2i * t7.Im - tw3i * t6.Im - tw1i * t5.Im);
                        da.Re = ca.Re + cb.Re;
                        da.Im = ca.Im + cb.Im;
                        db.Re = ca.Re - cb.Re;
                        db.Im = ca.Im - cb.Im;
                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (2))],
                            ref wa[(i) - 1 + (2 - 1) * (ido - 1)],
                            ref da,
                            sign);
                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (5))],
                            ref wa[(i) - 1 + (5 - 1) * (ido - 1)],
                            ref db,
                            sign);
                        ca.Re = t1.Re + tw3r * t2.Re + tw1r * t3.Re + tw2r * t4.Re;
                        ca.Im = t1.Im + tw3r * t2.Im + tw1r * t3.Im + tw2r * t4.Im;
                        cb.Im = +tw3i * t7.Re - tw1i * t6.Re + tw2i * t5.Re;
                        cb.Re = -(+tw3i * t7.Im - tw1i * t6.Im + tw2i * t5.Im);
                        da.Re = ca.Re + cb.Re;
                        da.Im = ca.Im + cb.Im;
                        db.Re = ca.Re - cb.Re;
                        db.Im = ca.Im - cb.Im;
                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (3))],
                            ref wa[(i) - 1 + (3 - 1) * (ido - 1)],
                            ref da,
                            sign);
                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (4))],
                            ref wa[(i) - 1 + (4 - 1) * (ido - 1)],
                            ref db,
                            sign);
                    }
                }
            }
        }

        private static void pass11(int ido, int l1, Span<Complex> cc, Span<Complex> ch, Span<Complex> wa, int sign)
        {
            const int cdim = 11;
            double tw1r = 0.8412535328311811688618,
                tw1i = sign * 0.5406408174555975821076,
                tw2r = 0.4154150130018864255293,
                tw2i = sign * 0.9096319953545183714117,
                tw3r = -0.1423148382732851404438,
                tw3i = sign * 0.9898214418809327323761,
                tw4r = -0.6548607339452850640569,
                tw4i = sign * 0.755749574354258283774,
                tw5r = -0.9594929736144973898904,
                tw5i = sign * 0.2817325568414296977114;

            Complex t1 = default, t2 = default, t3 = default, t4 = default, t5 = default, t6 = default, t7 = default, t8 = default, t9 = default, t10 = default, t11 = default;
            Complex ca = default, cb = default, da = default, db = default;
            if (ido == 1)
            {
                for (int k = 0;k < l1; ++k)
                {
                    t1 = cc[(0) + ido * ((0) + cdim * (k))];

                    FFTIntrinsics.PMC(
                        ref t2,
                        ref t11,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((10) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t3,
                        ref t10,
                        ref cc[(0) + ido * ((2) + cdim * (k))],
                        ref cc[(0) + ido * ((9) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t4,
                        ref t9,
                        ref cc[(0) + ido * ((3) + cdim * (k))],
                        ref cc[(0) + ido * ((8) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t5,
                        ref t8,
                        ref cc[(0) + ido * ((4) + cdim * (k))],
                        ref cc[(0) + ido * ((7) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t6,
                        ref t7,
                        ref cc[(0) + ido * ((5) + cdim * (k))],
                        ref cc[(0) + ido * ((6) + cdim * (k))]);

                    ref Complex z = ref ch[(0) + ido * ((k) + l1 * (0))];
                    z.Re = t1.Re + t2.Re + t3.Re + t4.Re + t5.Re + t6.Re;
                    z.Im = t1.Im + t2.Im + t3.Im + t4.Im + t5.Im + t6.Im;

                    ca.Re = t1.Re + tw1r * t2.Re + tw2r * t3.Re + tw3r * t4.Re + tw4r * t5.Re + tw5r * t6.Re;
                    ca.Im = t1.Im + tw1r * t2.Im + tw2r * t3.Im + tw3r * t4.Im + tw4r * t5.Im + tw5r * t6.Im;
                    cb.Im = +tw1i * t11.Re + tw2i * t10.Re + tw3i * t9.Re + tw4i * t8.Re + tw5i * t7.Re;
                    cb.Re = -(+tw1i * t11.Im + tw2i * t10.Im + tw3i * t9.Im + tw4i * t8.Im + tw5i * t7.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (10))],
                        ref ca,
                        ref cb);

                    ca.Re = t1.Re + tw2r * t2.Re + tw4r * t3.Re + tw5r * t4.Re + tw3r * t5.Re + tw1r * t6.Re;
                    ca.Im = t1.Im + tw2r * t2.Im + tw4r * t3.Im + tw5r * t4.Im + tw3r * t5.Im + tw1r * t6.Im;
                    cb.Im = +tw2i * t11.Re + tw4i * t10.Re - tw5i * t9.Re - tw3i * t8.Re - tw1i * t7.Re;
                    cb.Re = -(+tw2i * t11.Im + tw4i * t10.Im - tw5i * t9.Im - tw3i * t8.Im - tw1i * t7.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref ch[(0) + ido * ((k) + l1 * (9))],
                        ref ca,
                        ref cb);

                    ca.Re = t1.Re + tw3r * t2.Re + tw5r * t3.Re + tw2r * t4.Re + tw1r * t5.Re + tw4r * t6.Re;
                    ca.Im = t1.Im + tw3r * t2.Im + tw5r * t3.Im + tw2r * t4.Im + tw1r * t5.Im + tw4r * t6.Im;
                    cb.Im = +tw3i * t11.Re - tw5i * t10.Re - tw2i * t9.Re + tw1i * t8.Re + tw4i * t7.Re;
                    cb.Re = -(+tw3i * t11.Im - tw5i * t10.Im - tw2i * t9.Im + tw1i * t8.Im + tw4i * t7.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (3))],
                        ref ch[(0) + ido * ((k) + l1 * (8))],
                        ref ca,
                        ref cb);

                    ca.Re = t1.Re + tw4r * t2.Re + tw3r * t3.Re + tw1r * t4.Re + tw5r * t5.Re + tw2r * t6.Re;
                    ca.Im = t1.Im + tw4r * t2.Im + tw3r * t3.Im + tw1r * t4.Im + tw5r * t5.Im + tw2r * t6.Im;
                    cb.Im = +tw4i * t11.Re - tw3i * t10.Re + tw1i * t9.Re + tw5i * t8.Re - tw2i * t7.Re;
                    cb.Re = -(+tw4i * t11.Im - tw3i * t10.Im + tw1i * t9.Im + tw5i * t8.Im - tw2i * t7.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (4))],
                        ref ch[(0) + ido * ((k) + l1 * (7))],
                        ref ca,
                        ref cb);

                    ca.Re = t1.Re + tw5r * t2.Re + tw1r * t3.Re + tw4r * t4.Re + tw2r * t5.Re + tw3r * t6.Re;
                    ca.Im = t1.Im + tw5r * t2.Im + tw1r * t3.Im + tw4r * t4.Im + tw2r * t5.Im + tw3r * t6.Im;
                    cb.Im = +tw5i * t11.Re - tw1i * t10.Re + tw4i * t9.Re - tw2i * t8.Re + tw3i * t7.Re;
                    cb.Re = -(+tw5i * t11.Im - tw1i * t10.Im + tw4i * t9.Im - tw2i * t8.Im + tw3i * t7.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (5))],
                        ref ch[(0) + ido * ((k) + l1 * (6))],
                        ref ca,
                        ref cb);
                }
            }
            else
            {
                for (int k = 0; k < l1; ++k)
                {
                    t1 = cc[(0) + ido * ((0) + cdim * (k))];

                    FFTIntrinsics.PMC(
                        ref t2,
                        ref t11,
                        ref cc[(0) + ido * ((1) + cdim * (k))],
                        ref cc[(0) + ido * ((10) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t3,
                        ref t10,
                        ref cc[(0) + ido * ((2) + cdim * (k))],
                        ref cc[(0) + ido * ((9) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t4,
                        ref t9,
                        ref cc[(0) + ido * ((3) + cdim * (k))],
                        ref cc[(0) + ido * ((8) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t5,
                        ref t8,
                        ref cc[(0) + ido * ((4) + cdim * (k))],
                        ref cc[(0) + ido * ((7) + cdim * (k))]);
                    FFTIntrinsics.PMC(
                        ref t6,
                        ref t7,
                        ref cc[(0) + ido * ((5) + cdim * (k))],
                        ref cc[(0) + ido * ((6) + cdim * (k))]);

                    ref Complex z = ref ch[(0) + ido * ((k) + l1 * (0))];
                    z.Re = t1.Re + t2.Re + t3.Re + t4.Re + t5.Re + t6.Re;
                    z.Im = t1.Im + t2.Im + t3.Im + t4.Im + t5.Im + t6.Im;

                    ca.Re = t1.Re + tw1r * t2.Re + tw2r * t3.Re + tw3r * t4.Re + tw4r * t5.Re + tw5r * t6.Re;
                    ca.Im = t1.Im + tw1r * t2.Im + tw2r * t3.Im + tw3r * t4.Im + tw4r * t5.Im + tw5r * t6.Im;
                    cb.Im = +tw1i * t11.Re + tw2i * t10.Re + tw3i * t9.Re + tw4i * t8.Re + tw5i * t7.Re;
                    cb.Re = -(+tw1i * t11.Im + tw2i * t10.Im + tw3i * t9.Im + tw4i * t8.Im + tw5i * t7.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (1))],
                        ref ch[(0) + ido * ((k) + l1 * (10))],
                        ref ca,
                        ref cb);

                    ca.Re = t1.Re + tw2r * t2.Re + tw4r * t3.Re + tw5r * t4.Re + tw3r * t5.Re + tw1r * t6.Re;
                    ca.Im = t1.Im + tw2r * t2.Im + tw4r * t3.Im + tw5r * t4.Im + tw3r * t5.Im + tw1r * t6.Im;
                    cb.Im = +tw2i * t11.Re + tw4i * t10.Re - tw5i * t9.Re - tw3i * t8.Re - tw1i * t7.Re;
                    cb.Re = -(+tw2i * t11.Im + tw4i * t10.Im - tw5i * t9.Im - tw3i * t8.Im - tw1i * t7.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (2))],
                        ref ch[(0) + ido * ((k) + l1 * (9))],
                        ref ca,
                        ref cb);

                    ca.Re = t1.Re + tw3r * t2.Re + tw5r * t3.Re + tw2r * t4.Re + tw1r * t5.Re + tw4r * t6.Re;
                    ca.Im = t1.Im + tw3r * t2.Im + tw5r * t3.Im + tw2r * t4.Im + tw1r * t5.Im + tw4r * t6.Im;
                    cb.Im = +tw3i * t11.Re - tw5i * t10.Re - tw2i * t9.Re + tw1i * t8.Re + tw4i * t7.Re;
                    cb.Re = -(+tw3i * t11.Im - tw5i * t10.Im - tw2i * t9.Im + tw1i * t8.Im + tw4i * t7.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (3))],
                        ref ch[(0) + ido * ((k) + l1 * (8))],
                        ref ca,
                        ref cb);

                    ca.Re = t1.Re + tw4r * t2.Re + tw3r * t3.Re + tw1r * t4.Re + tw5r * t5.Re + tw2r * t6.Re;
                    ca.Im = t1.Im + tw4r * t2.Im + tw3r * t3.Im + tw1r * t4.Im + tw5r * t5.Im + tw2r * t6.Im;
                    cb.Im = +tw4i * t11.Re - tw3i * t10.Re + tw1i * t9.Re + tw5i * t8.Re - tw2i * t7.Re;
                    cb.Re = -(+tw4i * t11.Im - tw3i * t10.Im + tw1i * t9.Im + tw5i * t8.Im - tw2i * t7.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (4))],
                        ref ch[(0) + ido * ((k) + l1 * (7))],
                        ref ca,
                        ref cb);

                    ca.Re = t1.Re + tw5r * t2.Re + tw1r * t3.Re + tw4r * t4.Re + tw2r * t5.Re + tw3r * t6.Re;
                    ca.Im = t1.Im + tw5r * t2.Im + tw1r * t3.Im + tw4r * t4.Im + tw2r * t5.Im + tw3r * t6.Im;
                    cb.Im = +tw5i * t11.Re - tw1i * t10.Re + tw4i * t9.Re - tw2i * t8.Re + tw3i * t7.Re;
                    cb.Re = -(+tw5i * t11.Im - tw1i * t10.Im + tw4i * t9.Im - tw2i * t8.Im + tw3i * t7.Im);
                    FFTIntrinsics.PMC(
                        ref ch[(0) + ido * ((k) + l1 * (5))],
                        ref ch[(0) + ido * ((k) + l1 * (6))],
                        ref ca,
                        ref cb);

                    for (int i = 1; i < ido; ++i)
                    {
                        t1 = cc[(i) + ido * ((0) + cdim * (k))];

                        FFTIntrinsics.PMC(
                            ref t2,
                            ref t11,
                            ref cc[(i) + ido * ((1) + cdim * (k))],
                            ref cc[(i) + ido * ((10) + cdim * (k))]);
                        FFTIntrinsics.PMC(
                            ref t3,
                            ref t10,
                            ref cc[(i) + ido * ((2) + cdim * (k))],
                            ref cc[(i) + ido * ((9) + cdim * (k))]);
                        FFTIntrinsics.PMC(
                            ref t4,
                            ref t9,
                            ref cc[(i) + ido * ((3) + cdim * (k))],
                            ref cc[(i) + ido * ((8) + cdim * (k))]);
                        FFTIntrinsics.PMC(
                            ref t5,
                            ref t8,
                            ref cc[(i) + ido * ((4) + cdim * (k))],
                            ref cc[(i) + ido * ((7) + cdim * (k))]);
                        FFTIntrinsics.PMC(
                            ref t6,
                            ref t7,
                            ref cc[(i) + ido * ((5) + cdim * (k))],
                            ref cc[(i) + ido * ((6) + cdim * (k))]);

                        z = ref ch[(i) + ido * ((k) + l1 * (0))];
                        z.Re = t1.Re + t2.Re + t3.Re + t4.Re + t5.Re + t6.Re;
                        z.Im = t1.Im + t2.Im + t3.Im + t4.Im + t5.Im + t6.Im;

                        ca.Re = t1.Re + tw1r * t2.Re + tw2r * t3.Re + tw3r * t4.Re + tw4r * t5.Re + tw5r * t6.Re;
                        ca.Im = t1.Im + tw1r * t2.Im + tw2r * t3.Im + tw3r * t4.Im + tw4r * t5.Im + tw5r * t6.Im;
                        cb.Im = +tw1i * t11.Re + tw2i * t10.Re + tw3i * t9.Re + tw4i * t8.Re + tw5i * t7.Re;
                        cb.Re = -(+tw1i * t11.Im + tw2i * t10.Im + tw3i * t9.Im + tw4i * t8.Im + tw5i * t7.Im);

                        da.Re = ca.Re + cb.Re;
                        da.Im = ca.Im + cb.Im;
                        db.Re = ca.Re - cb.Re;
                        db.Im = ca.Im - cb.Im;

                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (1))],
                            ref wa[(i) - 1 + (1 - 1) * (ido - 1)],
                            ref da,
                            sign);
                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (10))],
                            ref wa[(i) - 1 + (10 - 1) * (ido - 1)],
                            ref db,
                            sign);

                        ca.Re = t1.Re + tw2r * t2.Re + tw4r * t3.Re + tw5r * t4.Re + tw3r * t5.Re + tw1r * t6.Re;
                        ca.Im = t1.Im + tw2r * t2.Im + tw4r * t3.Im + tw5r * t4.Im + tw3r * t5.Im + tw1r * t6.Im;
                        cb.Im = +tw2i * t11.Re + tw4i * t10.Re - tw5i * t9.Re - tw3i * t8.Re - tw1i * t7.Re;
                        cb.Re = -(+tw2i * t11.Im + tw4i * t10.Im - tw5i * t9.Im - tw3i * t8.Im - tw1i * t7.Im);

                        da.Re = ca.Re + cb.Re;
                        da.Im = ca.Im + cb.Im;
                        db.Re = ca.Re - cb.Re;
                        db.Im = ca.Im - cb.Im;

                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (2))],
                            ref wa[(i) - 1 + (2 - 1) * (ido - 1)],
                            ref da,
                            sign);
                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (9))],
                            ref wa[(i) - 1 + (9 - 1) * (ido - 1)],
                            ref db,
                            sign);

                        ca.Re = t1.Re + tw3r * t2.Re + tw5r * t3.Re + tw2r * t4.Re + tw1r * t5.Re + tw4r * t6.Re;
                        ca.Im = t1.Im + tw3r * t2.Im + tw5r * t3.Im + tw2r * t4.Im + tw1r * t5.Im + tw4r * t6.Im;
                        cb.Im = +tw3i * t11.Re - tw5i * t10.Re - tw2i * t9.Re + tw1i * t8.Re + tw4i * t7.Re;
                        cb.Re = -(+tw3i * t11.Im - tw5i * t10.Im - tw2i * t9.Im + tw1i * t8.Im + tw4i * t7.Im);

                        da.Re = ca.Re + cb.Re;
                        da.Im = ca.Im + cb.Im;
                        db.Re = ca.Re - cb.Re;
                        db.Im = ca.Im - cb.Im;

                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (3))],
                            ref wa[(i) - 1 + (3 - 1) * (ido - 1)],
                            ref da,
                            sign);
                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (8))],
                            ref wa[(i) - 1 + (8 - 1) * (ido - 1)],
                            ref db,
                            sign);

                        ca.Re = t1.Re + tw4r * t2.Re + tw3r * t3.Re + tw1r * t4.Re + tw5r * t5.Re + tw2r * t6.Re;
                        ca.Im = t1.Im + tw4r * t2.Im + tw3r * t3.Im + tw1r * t4.Im + tw5r * t5.Im + tw2r * t6.Im;
                        cb.Im = +tw4i * t11.Re - tw3i * t10.Re + tw1i * t9.Re + tw5i * t8.Re - tw2i * t7.Re;
                        cb.Re = -(+tw4i * t11.Im - tw3i * t10.Im + tw1i * t9.Im + tw5i * t8.Im - tw2i * t7.Im);

                        da.Re = ca.Re + cb.Re;
                        da.Im = ca.Im + cb.Im;
                        db.Re = ca.Re - cb.Re;
                        db.Im = ca.Im - cb.Im;

                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (4))],
                            ref wa[(i) - 1 + (4 - 1) * (ido - 1)],
                            ref da,
                            sign);
                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (7))],
                            ref wa[(i) - 1 + (7 - 1) * (ido - 1)],
                            ref db,
                            sign);

                        ca.Re = t1.Re + tw5r * t2.Re + tw1r * t3.Re + tw4r * t4.Re + tw2r * t5.Re + tw3r * t6.Re;
                        ca.Im = t1.Im + tw5r * t2.Im + tw1r * t3.Im + tw4r * t4.Im + tw2r * t5.Im + tw3r * t6.Im;
                        cb.Im = +tw5i * t11.Re - tw1i * t10.Re + tw4i * t9.Re - tw2i * t8.Re + tw3i * t7.Re;
                        cb.Re = -(+tw5i * t11.Im - tw1i * t10.Im + tw4i * t9.Im - tw2i * t8.Im + tw3i * t7.Im);

                        da.Re = ca.Re + cb.Re;
                        da.Im = ca.Im + cb.Im;
                        db.Re = ca.Re - cb.Re;
                        db.Im = ca.Im - cb.Im;

                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (5))],
                            ref wa[(i) - 1 + (5 - 1) * (ido - 1)],
                            ref da,
                            sign);
                        FFTIntrinsics.MULPMSIGNC(
                            ref ch[(i) + ido * ((k) + l1 * (6))],
                            ref wa[(i) - 1 + (6 - 1) * (ido - 1)],
                            ref db,
                            sign);
                    }
                }
            }
        }

        private static void passg(int ido, int ip, int l1, Span<Complex> cc, Span<Complex> ch, Span<Complex> wa, Span<Complex> csarr, int sign)
        {
            int cdim = ip;
            int ipph = (ip + 1) / 2;
            int idl1 = ido * l1;

            using (PooledBuffer<Complex> pooledWal = BufferPool<Complex>.Rent(ip))
            {
                Complex[] wal = pooledWal.Buffer;
                wal[0] = new Complex(1.0, 0.0);

                for (int i = 1; i < ip; ++i)
                {
                    wal[i] = new Complex(csarr[i].Re, sign * csarr[i].Im);
                }

                for (int k = 0; k < l1; ++k)
                {
                    for (int i = 0; i < ido; ++i)
                    {
                        ch[(i) + ido * ((k) + l1 * (0))] = cc[(i) + ido * ((0) + cdim * (k))];
                    }
                }

                for (int j = 1, jc = ip - 1; j < ipph; ++j, --jc)
                {
                    for (int k = 0; k < l1; ++k)
                    {
                        for (int i = 0; i < ido; ++i)
                        {
                            FFTIntrinsics.PMC(
                                ref ch[(i) + ido * ((k) + l1 * (j))],
                                ref ch[(i) + ido * ((k) + l1 * (jc))],
                                ref cc[(i) + ido * ((j) + cdim * (k))],
                                ref cc[(i) + ido * ((jc) + cdim * (k))]);
                        }
                    }
                }

                for (int k = 0; k < l1; ++k)
                {
                    for (int i = 0; i < ido; ++i)
                    {
                        Complex tmp = ch[(i) + ido * ((k) + l1 * (0))];
                        for (int j = 1; j < ipph; ++j)
                        {
                            ref Complex z = ref ch[(i) + ido * ((k) + l1 * (j))];
                            tmp.Re += z.Re;
                            tmp.Im += z.Im;
                        }

                        cc[(i) + ido * ((k) + l1 * (0))] = tmp;
                    }
                }

                for (int l = 1, lc = ip - 1; l < ipph; ++l, --lc)
                {
                    // j=0
                    for (int ik = 0; ik < idl1; ++ik)
                    {
                        FFTIntrinsics.PASSG1(
                            ref cc[(ik) + idl1 * (l)],
                            ref ch[(ik) + idl1 * (0)],
                            ref wal[l],
                            ref ch[(ik) + idl1 * (1)],
                            ref wal[2 * l],
                            ref ch[(ik) + idl1 * (2)]);
                        FFTIntrinsics.PASSG2(
                            ref cc[(ik) + idl1 * (lc)],
                            ref wal[l],
                            ref ch[(ik) + idl1 * (ip - 1)],
                            ref wal[2 * l],
                            ref ch[(ik) + idl1 * (ip - 2)]);
                    }

                    int iwal = 2 * l;
                    int j = 3, jc = ip - 3;
                    for (; j < ipph - 1; j += 2, jc -= 2)
                    {
                        iwal += l;
                        if (iwal > ip)
                        {
                            iwal -= ip;
                        }

                        Complex xwal = wal[iwal];
                        iwal += l;
                        if (iwal > ip)
                        {
                            iwal -= ip;
                        }

                        Complex xwal2 = wal[iwal];
                        for (int ik = 0; ik < idl1; ++ik)
                        {
                            FFTIntrinsics.PASSG3(
                                ref cc[(ik) + idl1 * (l)],
                                ref ch[(ik) + idl1 * (j)],
                                ref xwal,
                                ref ch[(ik) + idl1 * (j + 1)],
                                ref xwal2);
                            FFTIntrinsics.PASSG4(
                                ref cc[(ik) + idl1 * (lc)],
                                ref ch[(ik) + idl1 * (jc)],
                                ref xwal,
                                ref ch[(ik) + idl1 * (jc - 1)],
                                ref xwal2);
                        }
                    }

                    for (; j < ipph; ++j, --jc)
                    {
                        iwal += l;
                        if (iwal > ip)
                        {
                            iwal -= ip;
                        }

                        Complex xwal = wal[iwal];
                        for (int ik = 0; ik < idl1; ++ik)
                        {
                            FFTIntrinsics.PASSG5(ref cc[(ik) + idl1 * (l)], ref ch[(ik) + idl1 * (j)], ref xwal);
                            FFTIntrinsics.PASSG6(ref cc[(ik) + idl1 * (lc)], ref ch[(ik) + idl1 * (jc)], ref xwal);
                        }
                    }
                }
            }

            Complex t1, t2;

            // shuffling and twiddling
            if (ido == 1)
            {
                for (int j = 1, jc = ip - 1; j < ipph; ++j, --jc)
                {
                    for (int ik = 0; ik < idl1; ++ik)
                    {
                        t1 = cc[(ik) + idl1 * (j)];
                        t2 = cc[(ik) + idl1 * (jc)];
                        FFTIntrinsics.PMC(
                            ref cc[(ik) + idl1 * (j)],
                            ref cc[(ik) + idl1 * (jc)],
                            ref t1,
                            ref t2);
                    }
                }
            }
            else
            {
                for (int j = 1, jc = ip - 1; j < ipph; ++j, --jc)
                {
                    for (int k = 0; k < l1; ++k)
                    {
                        t1 = cc[(0) + ido * ((k) + l1 * (j))];
                        t2 = cc[(0) + ido * ((k) + l1 * (jc))];
                        FFTIntrinsics.PMC(
                            ref cc[(0) + ido * ((k) + l1 * (j))],
                            ref cc[(0) + ido * ((k) + l1 * (jc))],
                            ref t1,
                            ref t2);

                        for (int i = 1; i < ido; ++i)
                        {
                            FFTIntrinsics.PMC(
                                ref t1,
                                ref t2,
                                ref cc[(i) + ido * ((k) + l1 * (j))],
                                ref cc[(i) + ido * ((k) + l1 * (jc))]);
                            int idij = (j - 1) * (ido - 1) + i - 1;
                            FFTIntrinsics.MULPMSIGNC(
                                ref cc[(i) + ido * ((k) + l1 * (j))],
                                ref wa[idij],
                                ref t1,
                                sign);
                            idij = (jc - 1) * (ido - 1) + i - 1;
                            FFTIntrinsics.MULPMSIGNC(
                                ref cc[(i) + ido * ((k) + l1 * (jc))],
                                ref wa[idij],
                                ref t2,
                                sign);
                        }
                    }
                }
            }
        }
    }
}
