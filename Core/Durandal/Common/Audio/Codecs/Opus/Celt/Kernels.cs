﻿/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2011 Xiph.Org Foundation
   Originally written by Jean-Marc Valin, Gregory Maxwell, Koen Vos,
   Timothy B. Terriberry, and the Opus open-source contributors
   Ported to C# by Logan Stromberg

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

#if !UNSAFE

namespace Durandal.Common.Audio.Codecs.Opus.Celt
{
    using Durandal.Common.Audio.Codecs.Opus.Celt.Enums;
    using Durandal.Common.Audio.Codecs.Opus.Celt.Structs;
    using Durandal.Common.Audio.Codecs.Opus.Common;
    using Durandal.Common.Audio.Codecs.Opus.Common.CPlusPlus;
    using System.Diagnostics;

    internal static class Kernels
    {
        internal static void celt_fir(
             short[] x,
             int x_ptr,
             short[] num,
             short[] y,
             int y_ptr,
             int N,
             int ord,
             short[] mem
             )
        {
            int i, j;
            short[] rnum = new short[ord];
            short[] local_x = new short[N + ord];

            for (i = 0; i < ord; i++)
            {
                rnum[i] = num[ord - i - 1];
            }

            for (i = 0; i < ord; i++)
            {
                local_x[i] = mem[ord - i - 1];
            }

            for (i = 0; i < N; i++)
            {
                local_x[i + ord] = x[x_ptr + i];
            }

            for (i = 0; i < ord; i++)
            {
                mem[i] = x[x_ptr + N - i - 1];
            }
            
            for (i = 0; i < N - 3; i += 4)
            {
                int sum0 = 0, sum1 = 0, sum2 = 0, sum3 = 0;
                xcorr_kernel(rnum, 0, local_x, i, ref sum0, ref sum1, ref sum2, ref sum3, ord);
                y[y_ptr + i] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i]), Inlines.PSHR32(sum0, CeltConstants.SIG_SHIFT))));
                y[y_ptr + i + 1] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i + 1]), Inlines.PSHR32(sum1, CeltConstants.SIG_SHIFT))));
                y[y_ptr + i + 2] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i + 2]), Inlines.PSHR32(sum2, CeltConstants.SIG_SHIFT))));
                y[y_ptr + i + 3] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i + 3]), Inlines.PSHR32(sum3, CeltConstants.SIG_SHIFT))));
            }

            for (; i < N; i++)
            {
                int sum = 0;

                for (j = 0; j < ord; j++)
                {
                    sum = Inlines.MAC16_16(sum, rnum[j], local_x[i + j]);
                }

                y[y_ptr + i] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i]), Inlines.PSHR32(sum, CeltConstants.SIG_SHIFT))));
            }
        }

        internal static void celt_fir(
             int[] x,
             int x_ptr,
             int[] num,
             int num_ptr,
             int[] y,
             int y_ptr,
             int N,
             int ord,
             int[] mem
             )
        {
            int i, j;
            int[] rnum = new int[ord];
            int[] local_x = new int[N + ord];

            for (i = 0; i < ord; i++)
            {
                rnum[i] = num[num_ptr + ord - i - 1];
            }

            for (i = 0; i < ord; i++)
            {
                local_x[i] = mem[ord - i - 1];
            }

            for (i = 0; i < N; i++)
            {
                local_x[i + ord] = x[x_ptr + i];
            }

            for (i = 0; i < ord; i++)
            {
                mem[i] = x[x_ptr + N - i - 1];
            }
            
            for (i = 0; i < N - 3; i += 4)
            {
                int sum0 = 0, sum1 = 0, sum2 = 0, sum3 = 0;
                xcorr_kernel(rnum, local_x, i, ref sum0, ref sum1, ref sum2, ref sum3, ord);
                y[y_ptr + i] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i]), Inlines.PSHR32(sum0, CeltConstants.SIG_SHIFT))));
                y[y_ptr + i + 1] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i + 1]), Inlines.PSHR32(sum1, CeltConstants.SIG_SHIFT))));
                y[y_ptr + i + 2] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i + 2]), Inlines.PSHR32(sum2, CeltConstants.SIG_SHIFT))));
                y[y_ptr + i + 3] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i + 3]), Inlines.PSHR32(sum3, CeltConstants.SIG_SHIFT))));
            }

            for (; i < N; i++)
            {
                int sum = 0;

                for (j = 0; j < ord; j++)
                {
                    sum = Inlines.MAC16_16(sum, rnum[j], local_x[i + j]);
                }

                y[y_ptr + i] = Inlines.SATURATE16((Inlines.ADD32(Inlines.EXTEND32(x[x_ptr + i]), Inlines.PSHR32(sum, CeltConstants.SIG_SHIFT))));
            }
        }

        /// <summary>
        /// OPT: This is the kernel you really want to optimize. It gets used a lot by the prefilter and by the PLC.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="x_ptr"></param>
        /// <param name="y"></param>
        /// <param name="y_ptr"></param>
        /// <param name="sum0"></param>
        /// <param name="sum1"></param>
        /// <param name="sum2"></param>
        /// <param name="sum3"></param>
        /// <param name="len"></param>
        internal static void xcorr_kernel(short[] x, int x_ptr, short[] y, int y_ptr, ref int sum0, ref int sum1, ref int sum2, ref int sum3, int len)
        {
            int j;
            short y_0, y_1, y_2, y_3;
            Inlines.OpusAssert(len >= 3);
            y_3 = 0; /* gcc doesn't realize that y_3 can't be used uninitialized */
            y_0 = y[y_ptr++];
            y_1 = y[y_ptr++];
            y_2 = y[y_ptr++];
            for (j = 0; j < len - 3; j += 4)
            {
                short tmp;
                tmp = x[x_ptr++];
                y_3 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_0);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_1);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_2);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_3);
                tmp = x[x_ptr++];
                y_0 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_1);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_2);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_3);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_0);
                tmp = x[x_ptr++];
                y_1 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_2);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_3);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_0);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_1);
                tmp = x[x_ptr++];
                y_2 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_3);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_0);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_1);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_2);
            }
            if (j++ < len)
            {
                short tmp;
                tmp = x[x_ptr++];
                y_3 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_0);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_1);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_2);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_3);
            }
            if (j++ < len)
            {
                short tmp;
                tmp = x[x_ptr++];
                y_0 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_1);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_2);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_3);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_0);
            }
            if (j < len)
            {
                short tmp;
                tmp = x[x_ptr++];
                y_1 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_2);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_3);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_0);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_1);
            }
        }

        internal static void xcorr_kernel(int[] x, int[] y, int y_ptr, ref int sum0, ref int sum1, ref int sum2, ref int sum3, int len)
        {
            int j;
            int y_0, y_1, y_2, y_3;
            int x_ptr = 0;
            Inlines.OpusAssert(len >= 3);
            y_3 = 0; /* gcc doesn't realize that y_3 can't be used uninitialized */
            y_0 = y[y_ptr++];
            y_1 = y[y_ptr++];
            y_2 = y[y_ptr++];
            for (j = 0; j < len - 3; j += 4)
            {
                int tmp;
                tmp = x[x_ptr++];
                y_3 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_0);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_1);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_2);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_3);
                tmp = x[x_ptr++];
                y_0 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_1);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_2);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_3);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_0);
                tmp = x[x_ptr++];
                y_1 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_2);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_3);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_0);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_1);
                tmp = x[x_ptr++];
                y_2 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_3);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_0);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_1);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_2);
            }
            if (j++ < len)
            {
                int tmp;
                tmp = x[x_ptr++];
                y_3 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_0);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_1);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_2);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_3);
            }
            if (j++ < len)
            {
                int tmp;
                tmp = x[x_ptr++];
                y_0 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_1);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_2);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_3);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_0);
            }
            if (j < len)
            {
                int tmp;
                tmp = x[x_ptr++];
                y_1 = y[y_ptr++];
                sum0 = Inlines.MAC16_16(sum0, tmp, y_2);
                sum1 = Inlines.MAC16_16(sum1, tmp, y_3);
                sum2 = Inlines.MAC16_16(sum2, tmp, y_0);
                sum3 = Inlines.MAC16_16(sum3, tmp, y_1);
            }
        }

        internal static int celt_inner_prod(short[] x, int x_ptr, short[] y, int y_ptr, int N)
        {
            int i;
            int xy = 0;
            for (i = 0; i < N; i++)
                xy = Inlines.MAC16_16(xy, x[x_ptr + i], y[y_ptr + i]);
            return xy;
        }

        internal static int celt_inner_prod(short[] x, short[] y, int y_ptr, int N)
        {
            int i;
            int xy = 0;
            for (i = 0; i < N; i++)
                xy = Inlines.MAC16_16(xy, x[i], y[y_ptr + i]);
            return xy;
        }

        internal static int celt_inner_prod(int[] x, int x_ptr, int[] y, int y_ptr, int N)
        {
            int i;
            int xy = 0;
            for (i = 0; i < N; i++)
                xy = Inlines.MAC16_16(xy, x[x_ptr + i], y[y_ptr + i]);
            return xy;
        }
        
        internal static void dual_inner_prod(int[] x, int x_ptr, int[] y01,int y01_ptr, int[] y02, int y02_ptr, int N, out int xy1, out int xy2)
        {
            int i;
            int xy01 = 0;
            int xy02 = 0;
            for (i = 0; i < N; i++)
            {
                xy01 = Inlines.MAC16_16(xy01, x[x_ptr + i], y01[y01_ptr + i]);
                xy02 = Inlines.MAC16_16(xy02, x[x_ptr + i], y02[y02_ptr + i]);
            }
            xy1 = xy01;
            xy2 = xy02;
        }
    }
}

#endif