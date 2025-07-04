﻿using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.MathExt.FFT
{
    internal class PlanRealPackedFloat32 : IReal1DFFTPlanFloat32
    {
        internal struct rfftp_fctdata
        {
            public int fct;
            public int tw; // index into plan.mem
            public int tws; // index into plan.mem

            public rfftp_fctdata(int fct, int tw, int tws)
            {
                this.fct = fct;
                this.tw = tw;
                this.tws = tws;
            }
        }

        internal int length;
        internal int nfct;
        internal PooledBuffer<float> mem;
#if NET8_0_OR_GREATER
        [System.Runtime.CompilerServices.InlineArray(FFTConstants.NFCT)]
        internal struct rfftp_fctdata_array
        {
            internal rfftp_fctdata data;
        }

        internal rfftp_fctdata_array fct;
#else
        internal rfftp_fctdata[] fct; // [Constants.NFCT]
#endif
        private int _disposed = 0;

        public int Length => length;

        public PlanRealPackedFloat32(int length)
        {
            if (length == 0)
            {
                throw new ArgumentOutOfRangeException("FFT length must be greater than zero");
            }

            this.length = length;
            this.nfct = 0;
            this.mem = null;
#if !NET8_0_OR_GREATER
            if (this.fct == null) // if it's not an inline struct array...
            {
                this.fct = new rfftp_fctdata[FFTConstants.NFCT];
            }
#endif

            for (int i = 0; i < FFTConstants.NFCT; ++i)
            {
                this.fct[i] = new rfftp_fctdata(0, 0, 0);
            }

            if (length == 1)
            {
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
                return;
            }

            if (!rfftp_factorize())
            {
                throw new ArithmeticException($"Could not factorize FFT of length {length}");
            }

            int tws = rfftp_twsize();
            this.mem = BufferPool<float>.Rent(tws);
            rfftp_comp_twiddle();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PlanRealPackedFloat32()
        {
            Dispose(false);
        }
#endif

        public void Forward(Span<float> c, float fct)
        {
            rfftp_forward(this, c, fct);
        }

        public void Backward(Span<float> c, float fct)
        {
            rfftp_backward(this, c, fct);
        }

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
                this.mem?.Dispose();
            }
        }

        private static void rfftp_forward(PlanRealPackedFloat32 plan, Span<float> c, float fct)
        {
            if (plan.length == 1)
            {
                return;
            }

            int n = plan.length;
            int l1 = n, nf = plan.nfct;
            PooledBuffer<float> scratch = null;
            bool doStackAlloc = n <= FFTConstants.STACKALLOC_DOUBLE_LIMIT;
            if (!doStackAlloc)
            {
                scratch = BufferPool<float>.Rent(n);
            }

            Span<float> ch = doStackAlloc ? stackalloc float[n] : scratch.Buffer;
            Span<float> p1 = c;
            Span<float> p2 = ch;
            bool swapped = false;

            for (int k1 = 0; k1 < nf; ++k1)
            {
                int k = nf - k1 - 1;
                int ip = plan.fct[k].fct;
                int ido = n / l1;
                l1 /= ip;
                if (swapped) // we can't swap a stackalloc span so we have to do this weird thing instead
                {
                    if (ip == 4)
                    {
                        radf4(ido, l1, p2, p1, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else if (ip == 2)
                    {
                        radf2(ido, l1, p2, p1, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else if (ip == 3)
                    {
                        radf3(ido, l1, p2, p1, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else if (ip == 5)
                    {
                        radf5(ido, l1, p2, p1, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else
                    {
                        radfg(ido, ip, l1, p2, p1, plan.mem.Buffer.AsSpan(plan.fct[k].tw), plan.mem.Buffer.AsSpan(plan.fct[k].tws));
                        swapped = !swapped;
                    }
                }
                else
                {
                    if (ip == 4)
                    {
                        radf4(ido, l1, p1, p2, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else if (ip == 2)
                    {
                        radf2(ido, l1, p1, p2, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else if (ip == 3)
                    {
                        radf3(ido, l1, p1, p2, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else if (ip == 5)
                    {
                        radf5(ido, l1, p1, p2, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else
                    {
                        radfg(ido, ip, l1, p1, p2, plan.mem.Buffer.AsSpan(plan.fct[k].tw), plan.mem.Buffer.AsSpan(plan.fct[k].tws));
                        swapped = !swapped;
                    }
                }

                swapped = !swapped;
            }

            copy_and_norm(c, swapped ? p2 : p1, n, fct);
            scratch?.Dispose();
        }

        private static void rfftp_backward(PlanRealPackedFloat32 plan, Span<float> c, float fct)
        {
            if (plan.length == 1)
            {
                return;
            }

            int n = plan.length;
            int l1 = 1, nf = plan.nfct;
            PooledBuffer<float> scratch = null;
            bool doStackAlloc = n <= FFTConstants.STACKALLOC_DOUBLE_LIMIT;
            if (!doStackAlloc)
            {
                scratch = BufferPool<float>.Rent(n);
            }

            Span<float> ch = doStackAlloc ? stackalloc float[n] : scratch.Buffer;
            Span<float> p1 = c;
            Span<float> p2 = ch;
            bool swapped = false;

            for (int k = 0; k < nf; k++)
            {
                int ip = plan.fct[k].fct,
                    ido = n / (ip * l1);

                if (swapped) // we can't swap a stackalloc span so we have to do this weird thing instead
                {
                    if (ip == 4)
                    {
                        radb4(ido, l1, p2, p1, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else if (ip == 2)
                    {
                        radb2(ido, l1, p2, p1, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else if (ip == 3)
                    {
                        radb3(ido, l1, p2, p1, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else if (ip == 5)
                    {
                        radb5(ido, l1, p2, p1, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else
                    {
                        radbg(ido, ip, l1, p2, p1, plan.mem.Buffer.AsSpan(plan.fct[k].tw), plan.mem.Buffer.AsSpan(plan.fct[k].tws));
                    }
                }
                else
                {
                    if (ip == 4)
                    {
                        radb4(ido, l1, p1, p2, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else if (ip == 2)
                    {
                        radb2(ido, l1, p1, p2, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else if (ip == 3)
                    {
                        radb3(ido, l1, p1, p2, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else if (ip == 5)
                    {
                        radb5(ido, l1, p1, p2, plan.mem.Buffer.AsSpan(plan.fct[k].tw));
                    }
                    else
                    {
                        radbg(ido, ip, l1, p1, p2, plan.mem.Buffer.AsSpan(plan.fct[k].tw), plan.mem.Buffer.AsSpan(plan.fct[k].tws));
                    }
                }

                swapped = !swapped;
                l1 *= ip;
            }

            copy_and_norm(c, swapped ? p2 : p1, n, fct);
            scratch?.Dispose();
        }

        private static void copy_and_norm(Span<float> c, Span<float> p1, int n, float fct)
        {
            if (!FFTIntrinsics.SpanRefEquals(p1, c))
            {
                if (fct != 1.0)
                {
                    FFTIntrinsics.ScaleSpan(p1.Slice(0, n), c.Slice(0, n), fct);
                }
                else
                {
                    p1.Slice(0, n).CopyTo(c);
                }
            }
            else
            {
                if (fct != 1.0)
                {
                    FFTIntrinsics.ScaleSpanInPlace(c.Slice(0, n), fct);
                }
            }
        }

        private bool rfftp_factorize()
        {
            int length = this.length;
            int nfct = 0;
            while ((length % 4) == 0)
            {
                if (nfct >= FFTConstants.NFCT)
                {
                    return false;
                }

                this.fct[nfct++].fct = 4; length >>= 2;
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

            int maxl = (int)(Math.Sqrt((float)length)) + 1;
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
                    maxl = (int)(Math.Sqrt((float)length)) + 1;
                }
            }
            if (length > 1)
            {
                this.fct[nfct++].fct = length;
            }

            this.nfct = nfct;
            return true;
        }

        private int rfftp_twsize()
        {
            int twsize = 0, l1 = 1;
            for (int k = 0; k < this.nfct; ++k)
            {
                int ip = this.fct[k].fct, ido = this.length / (l1 * ip);
                twsize += (ip - 1) * (ido - 1);
                if (ip > 5) twsize += 2 * ip;
                l1 *= ip;
            }

            return twsize;
        }

        private void rfftp_comp_twiddle()
        {
            int length = this.length;
            float[] twid = new float[2 * length];
            FFTIntrinsics.sincos_2pibyn_half(length, twid);
            int l1 = 1;
            int ptr = 0;
            for (int k = 0; k < this.nfct; ++k)
            {
                int ip = this.fct[k].fct, ido = length / (l1 * ip);
                if (k < this.nfct - 1) // last factor doesn't need twiddles
                {
                    this.fct[k].tw = ptr;
                    Span<float> tw = this.mem.Buffer.AsSpan(ptr);
                    ptr += (ip - 1) * (ido - 1);
                    for (int j = 1; j < ip; ++j)
                    {
                        for (int i = 1; i <= (ido - 1) / 2; ++i)
                        {
                            tw[(j - 1) * (ido - 1) + 2 * i - 2] = twid[2 * j * l1 * i];
                            tw[(j - 1) * (ido - 1) + 2 * i - 1] = twid[2 * j * l1 * i + 1];
                        }
                    }
                }

                if (ip > 5) // special factors required by *g functions
                {
                    this.fct[k].tws = ptr;
                    Span<float> tws = this.mem.Buffer.AsSpan(ptr);
                    ptr += 2 * ip;
                    tws[0] = 1.0f;
                    tws[1] = 0.0f;
                    for (int i = 1; i <= (ip >> 1); ++i)
                    {
                        tws[2 * i] = twid[2 * i * (length / ip)];
                        tws[2 * i + 1] = twid[2 * i * (length / ip) + 1];
                        tws[2 * (ip - i)] = twid[2 * i * (length / ip)];
                        tws[2 * (ip - i) + 1] = -twid[2 * i * (length / ip) + 1];
                    }
                }

                l1 *= ip;
            }
        }

        private static void radf2(int ido, int l1, Span<float> cc, Span<float> ch, Span<float> wa)
        {
            const int cdim = 2;

            for (int k = 0; k < l1; k++)
            {
                ch[(0) + ido * ((0) + cdim * (k))] = cc[(0) + ido * ((k) + l1 * (0))] + cc[(0) + ido * ((k) + l1 * (1))];
                ch[(ido - 1) + ido * ((1) + cdim * (k))] = cc[(0) + ido * ((k) + l1 * (0))] - cc[(0) + ido * ((k) + l1 * (1))];
            }

            if ((ido & 1) == 0)
            {
                for (int k = 0; k < l1; k++)
                {
                    ch[(0) + ido * ((1) + cdim * (k))] = -cc[(ido - 1) + ido * ((k) + l1 * (1))];
                    ch[(ido - 1) + ido * ((0) + cdim * (k))] = cc[(ido - 1) + ido * ((k) + l1 * (0))];
                }
            }

            if (ido <= 2)
            {
                return;
            }

            for (int k = 0; k < l1; k++)
            {
                for (int i = 2; i < ido; i += 2)
                {
                    int ic = ido - i;
                    float tr2, ti2;
                    tr2 = wa[(i - 2) + (0) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (1))] + wa[(i - 1) + (0) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (1))];
                    ti2 = wa[(i - 2) + (0) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (1))] - wa[(i - 1) + (0) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (1))];
                    ch[(i - 1) + ido * ((0) + cdim * (k))] = cc[(i - 1) + ido * ((k) + l1 * (0))] + tr2;
                    ch[(ic - 1) + ido * ((1) + cdim * (k))] = cc[(i - 1) + ido * ((k) + l1 * (0))] - tr2;
                    ch[(i) + ido * ((0) + cdim * (k))] = ti2 + cc[(i) + ido * ((k) + l1 * (0))];
                    ch[(ic) + ido * ((1) + cdim * (k))] = ti2 - cc[(i) + ido * ((k) + l1 * (0))];
                }
            }
        }

        private static void radb2(int ido, int l1, Span<float> cc, Span<float> ch, Span<float> wa)
        {
            const int cdim = 2;

            for (int k = 0; k < l1; k++)
            {
                ch[(0) + ido * ((k) + l1 * (0))] = cc[(0) + ido * ((0) + cdim * (k))] + cc[(ido - 1) + ido * ((1) + cdim * (k))];
                ch[(0) + ido * ((k) + l1 * (1))] = cc[(0) + ido * ((0) + cdim * (k))] - cc[(ido - 1) + ido * ((1) + cdim * (k))];
            }
            if ((ido & 1) == 0)
            {
                for (int k = 0; k < l1; k++)
                {
                    ch[(ido - 1) + ido * ((k) + l1 * (0))] = 2.0f * cc[(ido - 1) + ido * ((0) + cdim * (k))];
                    ch[(ido - 1) + ido * ((k) + l1 * (1))] = -2.0f * cc[(0) + ido * ((1) + cdim * (k))];
                }
            }

            if (ido <= 2)
            {
                return;
            }

            for (int k = 0; k < l1; ++k)
            {
                for (int i = 2; i < ido; i += 2)
                {
                    int ic = ido - i;
                    float ti2, tr2;
                    ch[(i - 1) + ido * ((k) + l1 * (0))] = cc[(i - 1) + ido * ((0) + cdim * (k))] + cc[(ic - 1) + ido * ((1) + cdim * (k))];
                    tr2 = cc[(i - 1) + ido * ((0) + cdim * (k))] - cc[(ic - 1) + ido * ((1) + cdim * (k))];
                    ti2 = cc[(i) + ido * ((0) + cdim * (k))] + cc[(ic) + ido * ((1) + cdim * (k))];
                    ch[(i) + ido * ((k) + l1 * (0))] = cc[(i) + ido * ((0) + cdim * (k))] - cc[(ic) + ido * ((1) + cdim * (k))];
                    ch[(i) + ido * ((k) + l1 * (1))] = wa[(i - 2) + (0) * (ido - 1)] * ti2 + wa[(i - 1) + (0) * (ido - 1)] * tr2;
                    ch[(i - 1) + ido * ((k) + l1 * (1))] = wa[(i - 2) + (0) * (ido - 1)] * tr2 - wa[(i - 1) + (0) * (ido - 1)] * ti2;
                }
            }
        }

        private static void radb3(int ido, int l1, Span<float> cc, Span<float> ch, Span<float> wa)
        {
            const int cdim = 3;
            const float taur = -0.5f, taui = 0.86602540378443864676f;

            for (int k = 0; k < l1; k++)
            {
                float tr2 = 2.0f * cc[(ido - 1) + ido * ((1) + cdim * (k))];
                float cr2 = cc[(0) + ido * ((0) + cdim * (k))] + taur * tr2;
                ch[(0) + ido * ((k) + l1 * (0))] = cc[(0) + ido * ((0) + cdim * (k))] + tr2;
                float ci3 = 2.0f * taui * cc[(0) + ido * ((2) + cdim * (k))];
                { ch[(0) + ido * ((k) + l1 * (2))] = cr2 + ci3; ch[(0) + ido * ((k) + l1 * (1))] = cr2 - ci3; };
            }

            if (ido == 1)
            {
                return;
            }

            for (int k = 0; k < l1; k++)
            {
                for (int i = 2; i < ido; i += 2)
                {
                    int ic = ido - i;
                    float tr2 = cc[(i - 1) + ido * ((2) + cdim * (k))] + cc[(ic - 1) + ido * ((1) + cdim * (k))]; // t2=CC(I) + conj(CC(ic))
                    float ti2 = cc[(i) + ido * ((2) + cdim * (k))] - cc[(ic) + ido * ((1) + cdim * (k))];
                    float cr2 = cc[(i - 1) + ido * ((0) + cdim * (k))] + taur * tr2;     // c2=CC +taur*t2
                    float ci2 = cc[(i) + ido * ((0) + cdim * (k))] + taur * ti2;
                    ch[(i - 1) + ido * ((k) + l1 * (0))] = cc[(i - 1) + ido * ((0) + cdim * (k))] + tr2;         // CH=CC+t2
                    ch[(i) + ido * ((k) + l1 * (0))] = cc[(i) + ido * ((0) + cdim * (k))] + ti2;
                    float cr3 = taui * (cc[(i - 1) + ido * ((2) + cdim * (k))] - cc[(ic - 1) + ido * ((1) + cdim * (k))]);// c3=taui*(CC(i)-conj(CC(ic)))
                    float ci3 = taui * (cc[(i) + ido * ((2) + cdim * (k))] + cc[(ic) + ido * ((1) + cdim * (k))]);
                    float dr3 = cr2 + ci3;
                    float dr2 = cr2 - ci3; // d2= (cr2-ci3, ci2+cr3) = c2+i*c3
                    float di2 = ci2 + cr3;
                    float di3 = ci2 - cr3; // d3= (cr2+ci3, ci2-cr3) = c2-i*c3
                    ch[(i) + ido * ((k) + l1 * (1))] = wa[(i - 2) + (0) * (ido - 1)] * di2 + wa[(i - 1) + (0) * (ido - 1)] * dr2;
                    ch[(i - 1) + ido * ((k) + l1 * (1))] = wa[(i - 2) + (0) * (ido - 1)] * dr2 - wa[(i - 1) + (0) * (ido - 1)] * di2; // ch = WA*d2
                    ch[(i) + ido * ((k) + l1 * (2))] = wa[(i - 2) + (1) * (ido - 1)] * di3 + wa[(i - 1) + (1) * (ido - 1)] * dr3;
                    ch[(i - 1) + ido * ((k) + l1 * (2))] = wa[(i - 2) + (1) * (ido - 1)] * dr3 - wa[(i - 1) + (1) * (ido - 1)] * di3;
                }
            }
        }

        private static void radf3(int ido, int l1, Span<float> cc, Span<float> ch, Span<float> wa)
        {
            const int cdim = 3;
            const float taur = -0.5f, taui = 0.86602540378443864676f;

            for (int k = 0; k < l1; k++)
            {
                float cr2 = cc[(0) + ido * ((k) + l1 * (1))] + cc[(0) + ido * ((k) + l1 * (2))];
                ch[(0) + ido * ((0) + cdim * (k))] = cc[(0) + ido * ((k) + l1 * (0))] + cr2;
                ch[(0) + ido * ((2) + cdim * (k))] = taui * (cc[(0) + ido * ((k) + l1 * (2))] - cc[(0) + ido * ((k) + l1 * (1))]);
                ch[(ido - 1) + ido * ((1) + cdim * (k))] = cc[(0) + ido * ((k) + l1 * (0))] + taur * cr2;
            }

            if (ido == 1)
            {
                return;
            }

            for (int k = 0; k < l1; k++)
            {
                for (int i = 2; i < ido; i += 2)
                {
                    int ic = ido - i;
                    float di2, di3, dr2, dr3;
                    dr2 = wa[(i - 2) + (0) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (1))] + wa[(i - 1) + (0) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (1))];
                    di2 = wa[(i - 2) + (0) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (1))] - wa[(i - 1) + (0) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (1))]; // d2=conj(WA0)*CC1
                    dr3 = wa[(i - 2) + (1) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (2))] + wa[(i - 1) + (1) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (2))];
                    di3 = wa[(i - 2) + (1) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (2))] - wa[(i - 1) + (1) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (2))]; // d3=conj(WA1)*CC2
                    float cr2 = dr2 + dr3; // c add
                    float ci2 = di2 + di3;
                    ch[(i - 1) + ido * ((0) + cdim * (k))] = cc[(i - 1) + ido * ((k) + l1 * (0))] + cr2; // c add
                    ch[(i) + ido * ((0) + cdim * (k))] = cc[(i) + ido * ((k) + l1 * (0))] + ci2;
                    float tr2 = cc[(i - 1) + ido * ((k) + l1 * (0))] + taur * cr2; // c add
                    float ti2 = cc[(i) + ido * ((k) + l1 * (0))] + taur * ci2;
                    float tr3 = taui * (di2 - di3);  // t3 = taui*i*(d3-d2)?
                    float ti3 = taui * (dr3 - dr2);
                    ch[(i - 1) + ido * ((2) + cdim * (k))] = tr2 + tr3;
                    ch[(ic - 1) + ido * ((1) + cdim * (k))] = tr2 - tr3; // PM(i) = t2+t3
                    ch[(i) + ido * ((2) + cdim * (k))] = ti3 + ti2;
                    ch[(ic) + ido * ((1) + cdim * (k))] = ti3 - ti2; // PM(ic) = conj(t2-t3)
                }
            }
        }

        private static void radb4(int ido, int l1, Span<float> cc, Span<float> ch, Span<float> wa)
        {
            const int cdim = 4;
            const float sqrt2 = 1.41421356237309504880f;

            for (int k = 0; k < l1; k++)
            {
                float tr1, tr2;
                tr2 = cc[(0) + ido * ((0) + cdim * (k))] + cc[(ido - 1) + ido * ((3) + cdim * (k))];
                tr1 = cc[(0) + ido * ((0) + cdim * (k))] - cc[(ido - 1) + ido * ((3) + cdim * (k))];
                float tr3 = 2.0f * cc[(ido - 1) + ido * ((1) + cdim * (k))];
                float tr4 = 2.0f * cc[(0) + ido * ((2) + cdim * (k))];
                ch[(0) + ido * ((k) + l1 * (0))] = tr2 + tr3;
                ch[(0) + ido * ((k) + l1 * (2))] = tr2 - tr3;
                ch[(0) + ido * ((k) + l1 * (3))] = tr1 + tr4;
                ch[(0) + ido * ((k) + l1 * (1))] = tr1 - tr4;
            }
            if ((ido & 1) == 0)
            {
                for (int k = 0; k < l1; k++)
                {
                    float tr1, tr2, ti1, ti2;
                    ti1 = cc[(0) + ido * ((3) + cdim * (k))] + cc[(0) + ido * ((1) + cdim * (k))];
                    ti2 = cc[(0) + ido * ((3) + cdim * (k))] - cc[(0) + ido * ((1) + cdim * (k))];
                    tr2 = cc[(ido - 1) + ido * ((0) + cdim * (k))] + cc[(ido - 1) + ido * ((2) + cdim * (k))];
                    tr1 = cc[(ido - 1) + ido * ((0) + cdim * (k))] - cc[(ido - 1) + ido * ((2) + cdim * (k))];
                    ch[(ido - 1) + ido * ((k) + l1 * (0))] = tr2 + tr2;
                    ch[(ido - 1) + ido * ((k) + l1 * (1))] = sqrt2 * (tr1 - ti1);
                    ch[(ido - 1) + ido * ((k) + l1 * (2))] = ti2 + ti2;
                    ch[(ido - 1) + ido * ((k) + l1 * (3))] = -sqrt2 * (tr1 + ti1);
                }
            }

            if (ido <= 2)
            {
                return;
            }

            for (int k = 0; k < l1; ++k)
            {
                for (int i = 2; i < ido; i += 2)
                {
                    float ci2, ci3, ci4, cr2, cr3, cr4, ti1, ti2, ti3, ti4, tr1, tr2, tr3, tr4;
                    int ic = ido - i;
                    tr2 = cc[(i - 1) + ido * ((0) + cdim * (k))] + cc[(ic - 1) + ido * ((3) + cdim * (k))];
                    tr1 = cc[(i - 1) + ido * ((0) + cdim * (k))] - cc[(ic - 1) + ido * ((3) + cdim * (k))];
                    ti1 = cc[(i) + ido * ((0) + cdim * (k))] + cc[(ic) + ido * ((3) + cdim * (k))];
                    ti2 = cc[(i) + ido * ((0) + cdim * (k))] - cc[(ic) + ido * ((3) + cdim * (k))];
                    tr4 = cc[(i) + ido * ((2) + cdim * (k))] + cc[(ic) + ido * ((1) + cdim * (k))];
                    ti3 = cc[(i) + ido * ((2) + cdim * (k))] - cc[(ic) + ido * ((1) + cdim * (k))];
                    tr3 = cc[(i - 1) + ido * ((2) + cdim * (k))] + cc[(ic - 1) + ido * ((1) + cdim * (k))];
                    ti4 = cc[(i - 1) + ido * ((2) + cdim * (k))] - cc[(ic - 1) + ido * ((1) + cdim * (k))];
                    ch[(i - 1) + ido * ((k) + l1 * (0))] = tr2 + tr3; cr3 = tr2 - tr3;
                    ch[(i) + ido * ((k) + l1 * (0))] = ti2 + ti3; ci3 = ti2 - ti3;
                    cr4 = tr1 + tr4;
                    cr2 = tr1 - tr4;
                    ci2 = ti1 + ti4;
                    ci4 = ti1 - ti4;
                    ch[(i) + ido * ((k) + l1 * (1))] = wa[(i - 2) + (0) * (ido - 1)] * ci2 + wa[(i - 1) + (0) * (ido - 1)] * cr2;
                    ch[(i - 1) + ido * ((k) + l1 * (1))] = wa[(i - 2) + (0) * (ido - 1)] * cr2 - wa[(i - 1) + (0) * (ido - 1)] * ci2;
                    ch[(i) + ido * ((k) + l1 * (2))] = wa[(i - 2) + (1) * (ido - 1)] * ci3 + wa[(i - 1) + (1) * (ido - 1)] * cr3;
                    ch[(i - 1) + ido * ((k) + l1 * (2))] = wa[(i - 2) + (1) * (ido - 1)] * cr3 - wa[(i - 1) + (1) * (ido - 1)] * ci3;
                    ch[(i) + ido * ((k) + l1 * (3))] = wa[(i - 2) + (2) * (ido - 1)] * ci4 + wa[(i - 1) + (2) * (ido - 1)] * cr4;
                    ch[(i - 1) + ido * ((k) + l1 * (3))] = wa[(i - 2) + (2) * (ido - 1)] * cr4 - wa[(i - 1) + (2) * (ido - 1)] * ci4;
                }
            }
        }

        private static void radf4(int ido, int l1, Span<float> cc, Span<float> ch, Span<float> wa)
        {
            const int cdim = 4;
            const float hsqt2 = 0.70710678118654752440f;

            for (int k = 0; k < l1; k++)
            {
                float tr1, tr2;
                tr1 = cc[(0) + ido * ((k) + l1 * (3))] + cc[(0) + ido * ((k) + l1 * (1))];
                ch[(0) + ido * ((2) + cdim * (k))] = cc[(0) + ido * ((k) + l1 * (3))] - cc[(0) + ido * ((k) + l1 * (1))];
                tr2 = cc[(0) + ido * ((k) + l1 * (0))] + cc[(0) + ido * ((k) + l1 * (2))];
                ch[(ido - 1) + ido * ((1) + cdim * (k))] = cc[(0) + ido * ((k) + l1 * (0))] - cc[(0) + ido * ((k) + l1 * (2))];
                ch[(0) + ido * ((0) + cdim * (k))] = tr2 + tr1;
                ch[(ido - 1) + ido * ((3) + cdim * (k))] = tr2 - tr1;
            }

            if ((ido & 1) == 0)
            {
                for (int k = 0; k < l1; k++)
                {
                    float ti1 = -hsqt2 * (cc[(ido - 1) + ido * ((k) + l1 * (1))] + cc[(ido - 1) + ido * ((k) + l1 * (3))]);
                    float tr1 = hsqt2 * (cc[(ido - 1) + ido * ((k) + l1 * (1))] - cc[(ido - 1) + ido * ((k) + l1 * (3))]);
                    ch[(ido - 1) + ido * ((0) + cdim * (k))] = cc[(ido - 1) + ido * ((k) + l1 * (0))] + tr1;
                    ch[(ido - 1) + ido * ((2) + cdim * (k))] = cc[(ido - 1) + ido * ((k) + l1 * (0))] - tr1;
                    ch[(0) + ido * ((3) + cdim * (k))] = ti1 + cc[(ido - 1) + ido * ((k) + l1 * (2))];
                    ch[(0) + ido * ((1) + cdim * (k))] = ti1 - cc[(ido - 1) + ido * ((k) + l1 * (2))];
                }
            }

            if (ido <= 2)
            {
                return;
            }

            for (int k = 0; k < l1; k++)
            {
                for (int i = 2; i < ido; i += 2)
                {
                    int ic = ido - i;
                    float ci2, ci3, ci4, cr2, cr3, cr4, ti1, ti2, ti3, ti4, tr1, tr2, tr3, tr4;
                    cr2 = wa[(i - 2) + (0) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (1))] + wa[(i - 1) + (0) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (1))];
                    ci2 = wa[(i - 2) + (0) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (1))] - wa[(i - 1) + (0) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (1))];
                    cr3 = wa[(i - 2) + (1) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (2))] + wa[(i - 1) + (1) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (2))];
                    ci3 = wa[(i - 2) + (1) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (2))] - wa[(i - 1) + (1) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (2))];
                    cr4 = wa[(i - 2) + (2) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (3))] + wa[(i - 1) + (2) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (3))];
                    ci4 = wa[(i - 2) + (2) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (3))] - wa[(i - 1) + (2) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (3))];
                    tr1 = cr4 + cr2; tr4 = cr4 - cr2;
                    ti1 = ci2 + ci4; ti4 = ci2 - ci4;
                    tr2 = cc[(i - 1) + ido * ((k) + l1 * (0))] + cr3;
                    tr3 = cc[(i - 1) + ido * ((k) + l1 * (0))] - cr3;
                    ti2 = cc[(i) + ido * ((k) + l1 * (0))] + ci3;
                    ti3 = cc[(i) + ido * ((k) + l1 * (0))] - ci3;
                    ch[(i - 1) + ido * ((0) + cdim * (k))] = tr2 + tr1; ch[(ic - 1) + ido * ((3) + cdim * (k))] = tr2 - tr1;
                    ch[(i) + ido * ((0) + cdim * (k))] = ti1 + ti2;
                    ch[(ic) + ido * ((3) + cdim * (k))] = ti1 - ti2;
                    ch[(i - 1) + ido * ((2) + cdim * (k))] = tr3 + ti4; ch[(ic - 1) + ido * ((1) + cdim * (k))] = tr3 - ti4;
                    ch[(i) + ido * ((2) + cdim * (k))] = tr4 + ti3;
                    ch[(ic) + ido * ((1) + cdim * (k))] = tr4 - ti3;
                }
            }
        }

        private static void radb5(int ido, int l1, Span<float> cc, Span<float> ch, Span<float> wa)
        {
            const int cdim = 5;
            const float tr11 = 0.3090169943749474241f, ti11 = 0.95105651629515357212f,
                tr12 = -0.8090169943749474241f, ti12 = 0.58778525229247312917f;

            for (int k = 0; k < l1; k++)
            {
                float ti5 = cc[(0) + ido * ((2) + cdim * (k))] + cc[(0) + ido * ((2) + cdim * (k))];
                float ti4 = cc[(0) + ido * ((4) + cdim * (k))] + cc[(0) + ido * ((4) + cdim * (k))];
                float tr2 = cc[(ido - 1) + ido * ((1) + cdim * (k))] + cc[(ido - 1) + ido * ((1) + cdim * (k))];
                float tr3 = cc[(ido - 1) + ido * ((3) + cdim * (k))] + cc[(ido - 1) + ido * ((3) + cdim * (k))];
                ch[(0) + ido * ((k) + l1 * (0))] = cc[(0) + ido * ((0) + cdim * (k))] + tr2 + tr3;
                float cr2 = cc[(0) + ido * ((0) + cdim * (k))] + tr11 * tr2 + tr12 * tr3;
                float cr3 = cc[(0) + ido * ((0) + cdim * (k))] + tr12 * tr2 + tr11 * tr3;
                float ci4, ci5;
                ci5 = ti5 * ti11 + ti4 * ti12;
                ci4 = ti5 * ti12 - ti4 * ti11;
                ch[(0) + ido * ((k) + l1 * (4))] = cr2 + ci5;
                ch[(0) + ido * ((k) + l1 * (1))] = cr2 - ci5;
                ch[(0) + ido * ((k) + l1 * (3))] = cr3 + ci4;
                ch[(0) + ido * ((k) + l1 * (2))] = cr3 - ci4;
            }

            if (ido == 1)
            {
                return;
            }

            for (int k = 0; k < l1; ++k)
            {
                for (int i = 2; i < ido; i += 2)
                {
                    int ic = ido - i;
                    float tr2, tr3, tr4, tr5, ti2, ti3, ti4, ti5;
                    tr2 = cc[(i - 1) + ido * ((2) + cdim * (k))] + cc[(ic - 1) + ido * ((1) + cdim * (k))]; tr5 = cc[(i - 1) + ido * ((2) + cdim * (k))] - cc[(ic - 1) + ido * ((1) + cdim * (k))];
                    ti5 = cc[(i) + ido * ((2) + cdim * (k))] + cc[(ic) + ido * ((1) + cdim * (k))]; ti2 = cc[(i) + ido * ((2) + cdim * (k))] - cc[(ic) + ido * ((1) + cdim * (k))];
                    tr3 = cc[(i - 1) + ido * ((4) + cdim * (k))] + cc[(ic - 1) + ido * ((3) + cdim * (k))]; tr4 = cc[(i - 1) + ido * ((4) + cdim * (k))] - cc[(ic - 1) + ido * ((3) + cdim * (k))];
                    ti4 = cc[(i) + ido * ((4) + cdim * (k))] + cc[(ic) + ido * ((3) + cdim * (k))]; ti3 = cc[(i) + ido * ((4) + cdim * (k))] - cc[(ic) + ido * ((3) + cdim * (k))];
                    ch[(i - 1) + ido * ((k) + l1 * (0))] = cc[(i - 1) + ido * ((0) + cdim * (k))] + tr2 + tr3;
                    ch[(i) + ido * ((k) + l1 * (0))] = cc[(i) + ido * ((0) + cdim * (k))] + ti2 + ti3;
                    float cr2 = cc[(i - 1) + ido * ((0) + cdim * (k))] + tr11 * tr2 + tr12 * tr3;
                    float ci2 = cc[(i) + ido * ((0) + cdim * (k))] + tr11 * ti2 + tr12 * ti3;
                    float cr3 = cc[(i - 1) + ido * ((0) + cdim * (k))] + tr12 * tr2 + tr11 * tr3;
                    float ci3 = cc[(i) + ido * ((0) + cdim * (k))] + tr12 * ti2 + tr11 * ti3;
                    float ci4, ci5, cr5, cr4;
                    cr5 = tr5 * ti11 + tr4 * ti12;
                    cr4 = tr5 * ti12 - tr4 * ti11;
                    ci5 = ti5 * ti11 + ti4 * ti12;
                    ci4 = ti5 * ti12 - ti4 * ti11;
                    float dr2, dr3, dr4, dr5, di2, di3, di4, di5;
                    dr4 = cr3 + ci4; dr3 = cr3 - ci4;
                    di3 = ci3 + cr4; di4 = ci3 - cr4;
                    dr5 = cr2 + ci5; dr2 = cr2 - ci5;
                    di2 = ci2 + cr5; di5 = ci2 - cr5;
                    ch[(i) + ido * ((k) + l1 * (1))] = wa[(i - 2) + (0) * (ido - 1)] * di2 + wa[(i - 1) + (0) * (ido - 1)] * dr2;
                    ch[(i - 1) + ido * ((k) + l1 * (1))] = wa[(i - 2) + (0) * (ido - 1)] * dr2 - wa[(i - 1) + (0) * (ido - 1)] * di2;
                    ch[(i) + ido * ((k) + l1 * (2))] = wa[(i - 2) + (1) * (ido - 1)] * di3 + wa[(i - 1) + (1) * (ido - 1)] * dr3;
                    ch[(i - 1) + ido * ((k) + l1 * (2))] = wa[(i - 2) + (1) * (ido - 1)] * dr3 - wa[(i - 1) + (1) * (ido - 1)] * di3;
                    ch[(i) + ido * ((k) + l1 * (3))] = wa[(i - 2) + (2) * (ido - 1)] * di4 + wa[(i - 1) + (2) * (ido - 1)] * dr4;
                    ch[(i - 1) + ido * ((k) + l1 * (3))] = wa[(i - 2) + (2) * (ido - 1)] * dr4 - wa[(i - 1) + (2) * (ido - 1)] * di4;
                    ch[(i) + ido * ((k) + l1 * (4))] = wa[(i - 2) + (3) * (ido - 1)] * di5 + wa[(i - 1) + (3) * (ido - 1)] * dr5;
                    ch[(i - 1) + ido * ((k) + l1 * (4))] = wa[(i - 2) + (3) * (ido - 1)] * dr5 - wa[(i - 1) + (3) * (ido - 1)] * di5;
                }
            }
        }

        private static void radf5(int ido, int l1, Span<float> cc, Span<float> ch, Span<float> wa)
        {
            const int cdim = 5;
            const float
                tr11 = 0.3090169943749474241f,
                ti11 = 0.95105651629515357212f,
                tr12 = -0.8090169943749474241f,
                ti12 = 0.58778525229247312917f;

            for (int k = 0; k < l1; k++)
            {
                float cr2, cr3, ci4, ci5;
                cr2 = cc[(0) + ido * ((k) + l1 * (4))] + cc[(0) + ido * ((k) + l1 * (1))];
                ci5 = cc[(0) + ido * ((k) + l1 * (4))] - cc[(0) + ido * ((k) + l1 * (1))];
                cr3 = cc[(0) + ido * ((k) + l1 * (3))] + cc[(0) + ido * ((k) + l1 * (2))];
                ci4 = cc[(0) + ido * ((k) + l1 * (3))] - cc[(0) + ido * ((k) + l1 * (2))];
                ch[(0) + ido * ((0) + cdim * (k))] = cc[(0) + ido * ((k) + l1 * (0))] + cr2 + cr3;
                ch[(ido - 1) + ido * ((1) + cdim * (k))] = cc[(0) + ido * ((k) + l1 * (0))] + tr11 * cr2 + tr12 * cr3;
                ch[(0) + ido * ((2) + cdim * (k))] = ti11 * ci5 + ti12 * ci4;
                ch[(ido - 1) + ido * ((3) + cdim * (k))] = cc[(0) + ido * ((k) + l1 * (0))] + tr12 * cr2 + tr11 * cr3;
                ch[(0) + ido * ((4) + cdim * (k))] = ti12 * ci5 - ti11 * ci4;
            }

            if (ido == 1)
            {
                return;
            }

            for (int k = 0; k < l1; ++k)
            {
                for (int i = 2; i < ido; i += 2)
                {
                    float ci2, di2, ci4, ci5, di3, di4, di5, ci3, cr2, cr3, dr2, dr3,
                        dr4, dr5, cr5, cr4, ti2, ti3, ti5, ti4, tr2, tr3, tr4, tr5;
                    int ic = ido - i;
                    dr2 = wa[(i - 2) + (0) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (1))] + wa[(i - 1) + (0) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (1))];
                    di2 = wa[(i - 2) + (0) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (1))] - wa[(i - 1) + (0) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (1))];
                    dr3 = wa[(i - 2) + (1) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (2))] + wa[(i - 1) + (1) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (2))];
                    di3 = wa[(i - 2) + (1) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (2))] - wa[(i - 1) + (1) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (2))];
                    dr4 = wa[(i - 2) + (2) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (3))] + wa[(i - 1) + (2) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (3))];
                    di4 = wa[(i - 2) + (2) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (3))] - wa[(i - 1) + (2) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (3))];
                    dr5 = wa[(i - 2) + (3) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (4))] + wa[(i - 1) + (3) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (4))];
                    di5 = wa[(i - 2) + (3) * (ido - 1)] * cc[(i) + ido * ((k) + l1 * (4))] - wa[(i - 1) + (3) * (ido - 1)] * cc[(i - 1) + ido * ((k) + l1 * (4))];
                    cr2 = dr5 + dr2; ci5 = dr5 - dr2;
                    ci2 = di2 + di5; cr5 = di2 - di5;
                    cr3 = dr4 + dr3; ci4 = dr4 - dr3;
                    ci3 = di3 + di4; cr4 = di3 - di4;
                    ch[(i - 1) + ido * ((0) + cdim * (k))] = cc[(i - 1) + ido * ((k) + l1 * (0))] + cr2 + cr3;
                    ch[(i) + ido * ((0) + cdim * (k))] = cc[(i) + ido * ((k) + l1 * (0))] + ci2 + ci3;
                    tr2 = cc[(i - 1) + ido * ((k) + l1 * (0))] + tr11 * cr2 + tr12 * cr3;
                    ti2 = cc[(i) + ido * ((k) + l1 * (0))] + tr11 * ci2 + tr12 * ci3;
                    tr3 = cc[(i - 1) + ido * ((k) + l1 * (0))] + tr12 * cr2 + tr11 * cr3;
                    ti3 = cc[(i) + ido * ((k) + l1 * (0))] + tr12 * ci2 + tr11 * ci3;
                    tr5 = cr5 * ti11 + cr4 * ti12;
                    tr4 = cr5 * ti12 - cr4 * ti11;
                    ti5 = ci5 * ti11 + ci4 * ti12;
                    ti4 = ci5 * ti12 - ci4 * ti11;
                    ch[(i - 1) + ido * ((2) + cdim * (k))] = tr2 + tr5;
                    ch[(ic - 1) + ido * ((1) + cdim * (k))] = tr2 - tr5;
                    ch[(i) + ido * ((2) + cdim * (k))] = ti5 + ti2;
                    ch[(ic) + ido * ((1) + cdim * (k))] = ti5 - ti2;
                    ch[(i - 1) + ido * ((4) + cdim * (k))] = tr3 + tr4;
                    ch[(ic - 1) + ido * ((3) + cdim * (k))] = tr3 - tr4;
                    ch[(i) + ido * ((4) + cdim * (k))] = ti4 + ti3;
                    ch[(ic) + ido * ((3) + cdim * (k))] = ti4 - ti3;
                }
            }
        }

        private static void radbg(int ido, int ip, int l1, Span<float> cc, Span<float> ch, Span<float> wa, Span<float> csarr)
        {
            int cdim = ip;
            int ipph = (ip + 1) / 2;
            int idl1 = ido * l1;

            for (int k = 0; k < l1; ++k)        // 102
            {
                for (int i = 0; i < ido; ++i)     // 101
                {
                    ch[(i) + ido * ((k) + l1 * (0))] = cc[(i) + ido * ((0) + cdim * (k))];
                }
            }

            for (int j = 1, jc = ip - 1; j < ipph; ++j, --jc)   // 108
            {
                int j2 = 2 * j - 1;
                for (int k = 0; k < l1; ++k)
                {
                    ch[(0) + ido * ((k) + l1 * (j))] = 2 * cc[(ido - 1) + ido * ((j2) + cdim * (k))];
                    ch[(0) + ido * ((k) + l1 * (jc))] = 2 * cc[(0) + ido * ((j2 + 1) + cdim * (k))];
                }
            }

            if (ido != 1)
            {
                for (int j = 1, jc = ip - 1; j < ipph; ++j, --jc)   // 111
                {
                    int j2 = 2 * j - 1;
                    for (int k = 0; k < l1; ++k)
                    {
                        for (int i = 1, ic = ido - i - 2; i <= ido - 2; i += 2, ic -= 2)      // 109
                        {
                            ch[(i) + ido * ((k) + l1 * (j))] = cc[(i) + ido * ((j2 + 1) + cdim * (k))] + cc[(ic) + ido * ((j2) + cdim * (k))];
                            ch[(i) + ido * ((k) + l1 * (jc))] = cc[(i) + ido * ((j2 + 1) + cdim * (k))] - cc[(ic) + ido * ((j2) + cdim * (k))];
                            ch[(i + 1) + ido * ((k) + l1 * (j))] = cc[(i + 1) + ido * ((j2 + 1) + cdim * (k))] - cc[(ic + 1) + ido * ((j2) + cdim * (k))];
                            ch[(i + 1) + ido * ((k) + l1 * (jc))] = cc[(i + 1) + ido * ((j2 + 1) + cdim * (k))] + cc[(ic + 1) + ido * ((j2) + cdim * (k))];
                        }
                    }
                }
            }

            for (int l = 1, lc = ip - 1; l < ipph; ++l, --lc)
            {
                for (int ik = 0; ik < idl1; ++ik)
                {
                    cc[(ik) + idl1 * (l)] = ch[(ik) + idl1 * (0)] + csarr[2 * l] * ch[(ik) + idl1 * (1)] + csarr[4 * l] * ch[(ik) + idl1 * (2)];
                    cc[(ik) + idl1 * (lc)] = csarr[2 * l + 1] * ch[(ik) + idl1 * (ip - 1)] + csarr[4 * l + 1] * ch[(ik) + idl1 * (ip - 2)];
                }

                int iang = 2 * l;
                int j = 3, jc = ip - 3;
                for (; j < ipph - 3; j += 4, jc -= 4)
                {
                    iang += l; if (iang > ip) iang -= ip;
                    float ar1 = csarr[2 * iang], ai1 = csarr[2 * iang + 1];
                    iang += l; if (iang > ip) iang -= ip;
                    float ar2 = csarr[2 * iang], ai2 = csarr[2 * iang + 1];
                    iang += l; if (iang > ip) iang -= ip;
                    float ar3 = csarr[2 * iang], ai3 = csarr[2 * iang + 1];
                    iang += l; if (iang > ip) iang -= ip;
                    float ar4 = csarr[2 * iang], ai4 = csarr[2 * iang + 1];
                    for (int ik = 0; ik < idl1; ++ik)
                    {
                        cc[(ik) + idl1 * (l)] += ar1 * ch[(ik) + idl1 * (j)] + ar2 * ch[(ik) + idl1 * (j + 1)]
                            + ar3 * ch[(ik) + idl1 * (j + 2)] + ar4 * ch[(ik) + idl1 * (j + 3)];
                        cc[(ik) + idl1 * (lc)] += ai1 * ch[(ik) + idl1 * (jc)] + ai2 * ch[(ik) + idl1 * (jc - 1)]
                            + ai3 * ch[(ik) + idl1 * (jc - 2)] + ai4 * ch[(ik) + idl1 * (jc - 3)];
                    }
                }

                for (; j < ipph - 1; j += 2, jc -= 2)
                {
                    iang += l; if (iang > ip) iang -= ip;
                    float ar1 = csarr[2 * iang], ai1 = csarr[2 * iang + 1];
                    iang += l; if (iang > ip) iang -= ip;
                    float ar2 = csarr[2 * iang], ai2 = csarr[2 * iang + 1];
                    for (int ik = 0; ik < idl1; ++ik)
                    {
                        cc[(ik) + idl1 * (l)] += ar1 * ch[(ik) + idl1 * (j)] + ar2 * ch[(ik) + idl1 * (j + 1)];
                        cc[(ik) + idl1 * (lc)] += ai1 * ch[(ik) + idl1 * (jc)] + ai2 * ch[(ik) + idl1 * (jc - 1)];
                    }
                }

                for (; j < ipph; ++j, --jc)
                {
                    iang += l; if (iang > ip) iang -= ip;
                    float war = csarr[2 * iang], wai = csarr[2 * iang + 1];
                    for (int ik = 0; ik < idl1; ++ik)
                    {
                        cc[(ik) + idl1 * (l)] += war * ch[(ik) + idl1 * (j)];
                        cc[(ik) + idl1 * (lc)] += wai * ch[(ik) + idl1 * (jc)];
                    }
                }
            }

            for (int j = 1; j < ipph; ++j)
            {
                for (int ik = 0; ik < idl1; ++ik)
                {
                    ch[(ik) + idl1 * (0)] += ch[(ik) + idl1 * (j)];
                }
            }

            for (int j = 1, jc = ip - 1; j < ipph; ++j, --jc)   // 124
            {
                for (int k = 0; k < l1; ++k)
                {
                    ch[(0) + ido * ((k) + l1 * (j))] = cc[(0) + ido * ((k) + l1 * (j))] - cc[(0) + ido * ((k) + l1 * (jc))];
                    ch[(0) + ido * ((k) + l1 * (jc))] = cc[(0) + ido * ((k) + l1 * (j))] + cc[(0) + ido * ((k) + l1 * (jc))];
                }
            }

            if (ido == 1)
            {
                return;
            }

            for (int j = 1, jc = ip - 1; j < ipph; ++j, --jc)  // 127
            {
                for (int k = 0; k < l1; ++k)
                {
                    for (int i = 1; i <= ido - 2; i += 2)
                    {
                        ch[(i) + ido * ((k) + l1 * (j))] = cc[(i) + ido * ((k) + l1 * (j))] - cc[(i + 1) + ido * ((k) + l1 * (jc))];
                        ch[(i) + ido * ((k) + l1 * (jc))] = cc[(i) + ido * ((k) + l1 * (j))] + cc[(i + 1) + ido * ((k) + l1 * (jc))];
                        ch[(i + 1) + ido * ((k) + l1 * (j))] = cc[(i + 1) + ido * ((k) + l1 * (j))] + cc[(i) + ido * ((k) + l1 * (jc))];
                        ch[(i + 1) + ido * ((k) + l1 * (jc))] = cc[(i + 1) + ido * ((k) + l1 * (j))] - cc[(i) + ido * ((k) + l1 * (jc))];
                    }
                }
            }

            // All in CH

            for (int j = 1; j < ip; ++j)
            {
                int is1 = (j - 1) * (ido - 1);
                for (int k = 0; k < l1; ++k)
                {
                    int idij = is1;
                    for (int i = 1; i <= ido - 2; i += 2)
                    {
                        float t1 = ch[(i) + ido * ((k) + l1 * (j))], t2 = ch[(i + 1) + ido * ((k) + l1 * (j))];
                        ch[(i) + ido * ((k) + l1 * (j))] = wa[idij] * t1 - wa[idij + 1] * t2;
                        ch[(i + 1) + ido * ((k) + l1 * (j))] = wa[idij] * t2 + wa[idij + 1] * t1;
                        idij += 2;
                    }
                }
            }
        }

        private static void radfg(int ido, int ip, int l1, Span<float> cc, Span<float> ch, Span<float> wa, Span<float> csarr)
        {
            int cdim = ip;
            int ipph = (ip + 1) / 2;
            int idl1 = ido * l1;

            if (ido > 1)
            {
                for (int j = 1, jc = ip - 1; j < ipph; ++j, --jc)              // 114
                {
                    int is1 = (j - 1) * (ido - 1),
                        is2 = (jc - 1) * (ido - 1);

                    for (int k = 0; k < l1; ++k)                            // 113
                    {
                        int idij = is1;
                        int idij2 = is2;

                        for (int i = 1; i <= ido - 2; i += 2)                      // 112
                        {
                            float t1 = cc[(i) + ido * ((k) + l1 * (j))], t2 = cc[(i + 1) + ido * ((k) + l1 * (j))],
                                t3 = cc[(i) + ido * ((k) + l1 * (jc))], t4 = cc[(i + 1) + ido * ((k) + l1 * (jc))];
                            float x1 = wa[idij] * t1 + wa[idij + 1] * t2,
                                x2 = wa[idij] * t2 - wa[idij + 1] * t1,
                                x3 = wa[idij2] * t3 + wa[idij2 + 1] * t4,
                                x4 = wa[idij2] * t4 - wa[idij2 + 1] * t3;
                            cc[(i) + ido * ((k) + l1 * (j))] = x1 + x3;
                            cc[(i) + ido * ((k) + l1 * (jc))] = x2 - x4;
                            cc[(i + 1) + ido * ((k) + l1 * (j))] = x2 + x4;
                            cc[(i + 1) + ido * ((k) + l1 * (jc))] = x3 - x1;
                            idij += 2;
                            idij2 += 2;
                        }
                    }
                }
            }

            for (int j = 1, jc = ip - 1; j < ipph; ++j, --jc)                // 123
            {
                for (int k = 0; k < l1; ++k)                              // 122
                {
                    int ikj = ido * ((k) + l1 * (j));
                    int ikjc = ido * ((k) + l1 * (jc));
                    float t1 = cc[ikj], t2 = cc[ikjc];
                    cc[ikj] = t1 + t2;
                    cc[ikjc] = t2 - t1;
                }
            }

            //everything in C

            for (int l = 1, lc = ip - 1; l < ipph; ++l, --lc)                 // 127
            {
                for (int ik = 0; ik < idl1; ++ik)                         // 124
                {
                    ch[(ik) + idl1 * (l)] = cc[(ik) + idl1 * (0)] + csarr[2 * l] * cc[(ik) + idl1 * (1)] + csarr[4 * l] * cc[(ik) + idl1 * (2)];
                    ch[(ik) + idl1 * (lc)] = csarr[2 * l + 1] * cc[(ik) + idl1 * (ip - 1)] + csarr[4 * l + 1] * cc[(ik) + idl1 * (ip - 2)];
                }

                int iang = 2 * l;
                int j = 3, jc = ip - 3;
                for (; j < ipph - 3; j += 4, jc -= 4)              // 126
                {
                    iang += l; if (iang >= ip) iang -= ip;
                    float ar1 = csarr[2 * iang], ai1 = csarr[2 * iang + 1];
                    iang += l; if (iang >= ip) iang -= ip;
                    float ar2 = csarr[2 * iang], ai2 = csarr[2 * iang + 1];
                    iang += l; if (iang >= ip) iang -= ip;
                    float ar3 = csarr[2 * iang], ai3 = csarr[2 * iang + 1];
                    iang += l; if (iang >= ip) iang -= ip;
                    float ar4 = csarr[2 * iang], ai4 = csarr[2 * iang + 1];
                    for (int ik = 0; ik < idl1; ++ik)                       // 125
                    {
                        ch[(ik) + idl1 * (l)] += ar1 * cc[(ik) + idl1 * (j)] + ar2 * cc[(ik) + idl1 * (j + 1)]
                            + ar3 * cc[(ik) + idl1 * (j + 2)] + ar4 * cc[(ik) + idl1 * (j + 3)];
                        ch[(ik) + idl1 * (lc)] += ai1 * cc[(ik) + idl1 * (jc)] + ai2 * cc[(ik) + idl1 * (jc - 1)]
                            + ai3 * cc[(ik) + idl1 * (jc - 2)] + ai4 * cc[(ik) + idl1 * (jc - 3)];
                    }
                }

                for (; j < ipph - 1; j += 2, jc -= 2)              // 126
                {
                    iang += l; if (iang >= ip) iang -= ip;
                    float ar1 = csarr[2 * iang], ai1 = csarr[2 * iang + 1];
                    iang += l; if (iang >= ip) iang -= ip;
                    float ar2 = csarr[2 * iang], ai2 = csarr[2 * iang + 1];
                    for (int ik = 0; ik < idl1; ++ik)                       // 125
                    {
                        ch[(ik) + idl1 * (l)] += ar1 * cc[(ik) + idl1 * (j)] + ar2 * cc[(ik) + idl1 * (j + 1)];
                        ch[(ik) + idl1 * (lc)] += ai1 * cc[(ik) + idl1 * (jc)] + ai2 * cc[(ik) + idl1 * (jc - 1)];
                    }
                }

                for (; j < ipph; ++j, --jc)              // 126
                {
                    iang += l; if (iang >= ip) iang -= ip;
                    float ar = csarr[2 * iang], ai = csarr[2 * iang + 1];
                    for (int ik = 0; ik < idl1; ++ik)                       // 125
                    {
                        ch[(ik) + idl1 * (l)] += ar * cc[(ik) + idl1 * (j)];
                        ch[(ik) + idl1 * (lc)] += ai * cc[(ik) + idl1 * (jc)];
                    }
                }
            }

            for (int ik = 0; ik < idl1; ++ik)                         // 101
            {
                ch[ik] = cc[ik];
            }

            for (int j = 1; j < ipph; ++j)                              // 129
            {
                for (int ik = 0; ik < idl1; ++ik)                         // 128
                {
                    ch[(ik) + idl1 * (0)] += cc[(ik) + idl1 * (j)];
                }
            }

            // everything in CH at this point!

            for (int k = 0; k < l1; ++k)                                // 131
            {
                for (int i = 0; i < ido; ++i)                             // 130
                {
                    cc[(i) + ido * ((0) + cdim * (k))] = ch[(i) + ido * ((k) + l1 * (0))];
                }
            }

            for (int j = 1, jc = ip - 1; j < ipph; ++j, --jc)                // 137
            {
                int j2 = 2 * j - 1;
                for (int k = 0; k < l1; ++k)                              // 136
                {
                    cc[(ido - 1) + ido * ((j2) + cdim * (k))] = ch[(0) + ido * ((k) + l1 * (j))];
                    cc[(0) + ido * ((j2 + 1) + cdim * (k))] = ch[(0) + ido * ((k) + l1 * (jc))];
                }
            }

            if (ido == 1)
            {
                return;
            }

            for (int j = 1, jc = ip - 1; j < ipph; ++j, --jc)                // 140
            {
                int j2 = 2 * j - 1;
                for (int k = 0; k < l1; ++k)                               // 139
                {
                    for (int i = 1, ic = ido - i - 2; i <= ido - 2; i += 2, ic -= 2)      // 138
                    {
                        cc[(i) + ido * ((j2 + 1) + cdim * (k))] = ch[(i) + ido * ((k) + l1 * (j))] + ch[(i) + ido * ((k) + l1 * (jc))];
                        cc[(ic) + ido * ((j2) + cdim * (k))] = ch[(i) + ido * ((k) + l1 * (j))] - ch[(i) + ido * ((k) + l1 * (jc))];
                        cc[(i + 1) + ido * ((j2 + 1) + cdim * (k))] = ch[(i + 1) + ido * ((k) + l1 * (j))] + ch[(i + 1) + ido * ((k) + l1 * (jc))];
                        cc[(ic + 1) + ido * ((j2) + cdim * (k))] = ch[(i + 1) + ido * ((k) + l1 * (jc))] - ch[(i + 1) + ido * ((k) + l1 * (j))];
                    }
                }
            }
        }
    }
}
