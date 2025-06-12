using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.MathExt.FFT
{
    internal class PlanBluesteinFloat64 : IReal1DFFTPlanFloat64, IComplex1DFFTPlanFloat64
    {
        internal int n;
        internal int n2;
        internal PlanComplexPackedFloat64 plan;
        internal PooledBuffer<Complex> mem;
        internal int bk_idx; // indexes into mem
        internal int bkf_idx; // indexes into mem
        private int _disposed = 0;

        public int Length => plan.length;

        public PlanBluesteinFloat64(int length)
        {
            n = length;
            n2 = FFTIntrinsics.good_size(n * 2 - 1);
            mem = BufferPool<Complex>.Rent(n + n2);
            bk_idx = 0;
            bkf_idx = bk_idx + n;

            /* initialize b_k */
            Span<double> tmp = new double[4 * n];
            FFTIntrinsics.sincos_2pibyn(2 * n, tmp);
            Span<Complex> bk = mem.Buffer.AsSpan(bk_idx);
            Span<Complex> bkf = mem.Buffer.AsSpan(bkf_idx);
            bk[0].Re = 1;
            bk[0].Im = 0;

            int coeff = 0;
            for (int m = 1; m < n; ++m)
            {
                coeff += 2 * m - 1;
                if (coeff >= 2 * n)
                {
                    coeff -= 2 * n;
                }

                bk[m].Re = tmp[2 * coeff];
                bk[m].Im = tmp[2 * coeff + 1];
            }

            /* initialize the zero-padded, Fourier transformed b_k. Add normalisation. */
            double xn2 = 1.0 / n2;
            bkf[0].Re = bk[0].Re * xn2;
            bkf[0].Im = bk[0].Im * xn2;
            for (int m = 1; m < n; m++)
            {
                bkf[m].Re = bkf[n2 - m].Re = bk[m].Re * xn2;
                bkf[m].Im = bkf[n2 - m].Im = bk[m].Im * xn2;
            }

            // OPT Span.Clear(....)
            for (int m = n; m <= n2 - n; ++m)
            {
                bkf[m].Re = 0.0;
                bkf[m].Im = 0.0;
            }

            plan = new PlanComplexPackedFloat64(n2);
            plan.Forward(bkf, 1.0);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PlanBluesteinFloat64()
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
                plan?.Dispose();
                mem?.Dispose();
            }
        }

        public void Forward(Span<double> c, double fct)
        {
            using (PooledBuffer<Complex> pooledTmp = BufferPool<Complex>.Rent(n))
            {
                Complex[] tmp = pooledTmp.Buffer;
                for (int m = 0; m < n; ++m)
                {
                    tmp[m].Re = c[m];
                    tmp[m].Im = 0.0;
                }

                fftblue_fft(tmp, -1, fct);

                c[0] = tmp[0].Re;

                // Jank method
                MemoryMarshal.Cast<Complex, double>(tmp.AsSpan(1, n - 1)).CopyTo(c.Slice(1));

                // Safe method
                //for (int i = 1; i < n; i++)
                //{
                //    c[2 * i - 1] = tmp[i].r;
                //    c[2 * i] = tmp[i].i;
                //}
            }
        }

        public void Forward(Span<Complex> c, double fct)
        {
            fftblue_fft(c, -1, fct);
        }

        public void Backward(Span<double> c, double fct)
        {
            using (PooledBuffer<Complex> pooledTmp = BufferPool<Complex>.Rent(n))
            {
                Complex[] tmp = pooledTmp.Buffer;
                tmp[0].Re = c[0];
                tmp[1].Im = 0.0;

                // Jank method
                c.Slice(1, (n - 1)).CopyTo(MemoryMarshal.Cast<Complex, double>(tmp.AsSpan(1)));

                // Safe method
                //for (int i = 1; i < n; i++)
                //{
                //    tmp[i].r = c[2 * i - 1];
                //    tmp[i].i = c[2 * i];
                //}

                if ((n & 1) == 0)
                {
                    tmp[n - 1].Im = 0.0;
                }

                for (int m = 1; m < n; m++)
                {
                    FFTIntrinsics.BLUESTEINSTEP0(ref tmp[n - m], ref tmp[m]);
                }

                fftblue_fft(tmp, 1, fct);
                for (int m = 0; m < n; ++m)
                {
                    c[m] = tmp[m].Re;
                }
            }
        }

        public void Backward(Span<Complex> c, double fct)
        {
            fftblue_fft(c, 1, fct);
        }

        private void fftblue_fft(Span<Complex> c, int isign, double fct)
        {
            Span<Complex> bk = mem.Buffer.AsSpan(bk_idx);
            Span<Complex> bkf = mem.Buffer.AsSpan(bkf_idx);
            using (PooledBuffer<Complex> pooledAkf = BufferPool<Complex>.Rent(n2))
            {
                Complex[] akf = pooledAkf.Buffer;

                /* initialize a_k and FFT it */
                if (isign > 0)
                {
                    for (int m = 0; m < n; m++)
                    {
                        FFTIntrinsics.BLUESTEINSTEP1A(ref akf[m], ref bk[m], ref c[m]);
                    }
                }
                else
                {
                    for (int m = 0; m < n; m++)
                    {
                        FFTIntrinsics.BLUESTEINSTEP1B(ref akf[m], ref c[m], ref bk[m]);
                    }
                }

                MemoryMarshal.Cast<Complex, double>(akf.AsSpan(n, n2 - n)).Clear();
                //for (int m = n; m < n2; ++m)
                //{
                //    akf[m].r = 0;
                //    akf[m].i = 0;
                //}

                plan.Forward(akf, fct);

                /* do the convolution */
                if (isign > 0)
                {
                    for (int m = 0; m < n2; m++)
                    {
                        FFTIntrinsics.BLUESTEINSTEP2A(ref akf[m], ref bkf[m]);
                    }
                }
                else
                {
                    for (int m = 0; m < n2; m++)
                    {
                        FFTIntrinsics.BLUESTEINSTEP2B(ref akf[m], ref bkf[m]);
                    }
                }

                /* inverse FFT */
                plan.Backward(akf, 1.0);

                /* multiply by b_k */
                if (isign > 0)
                {
                    for (int m = 0; m < n; m++)
                    {
                        FFTIntrinsics.BLUESTEINSTEP3A(ref c[m], ref bk[m], ref akf[m]);
                    }
                }
                else
                {
                    for (int m = 0; m < n; m++)
                    {
                        FFTIntrinsics.BLUESTEINSTEP3B(ref c[m], ref bk[m], ref akf[m]);
                    }
                }
            }
        }
    }
}
