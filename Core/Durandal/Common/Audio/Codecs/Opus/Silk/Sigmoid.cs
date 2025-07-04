﻿/* Copyright (c) 2006-2011 Skype Limited. All Rights Reserved
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

namespace Durandal.Common.Audio.Codecs.Opus.Silk
{
    using Durandal.Common.Audio.Codecs.Opus.Common;
    using Durandal.Common.Audio.Codecs.Opus.Common.CPlusPlus;
    using Durandal.Common.Audio.Codecs.Opus.Silk.Enums;
    using Durandal.Common.Audio.Codecs.Opus.Silk.Structs;
    using System.Diagnostics;

    /// <summary>
    /// Approximate sigmoid function
    /// </summary>
    internal static class Sigmoid
    {
        private static readonly int[] sigm_LUT_slope_Q10 = {
            237, 153, 73, 30, 12, 7
        };

        private static readonly int[] sigm_LUT_pos_Q15 = {
            16384, 23955, 28861, 31213, 32178, 32548
        };

        private static readonly int[] sigm_LUT_neg_Q15 = {
            16384, 8812, 3906, 1554, 589, 219
        };

        internal static int silk_sigm_Q15(int in_Q5)
        {
            int ind;

            if (in_Q5 < 0)
            {
                /* Negative input */
                in_Q5 = -in_Q5;
                if (in_Q5 >= 6 * 32)
                {
                    return 0;        /* Clip */
                }
                else
                {
                    /* Linear interpolation of look up table */
                    ind = Inlines.silk_RSHIFT(in_Q5, 5);
                    return (sigm_LUT_neg_Q15[ind] - Inlines.silk_SMULBB(sigm_LUT_slope_Q10[ind], in_Q5 & 0x1F));
                }
            }
            else
            {
                /* Positive input */
                if (in_Q5 >= 6 * 32)
                {
                    return 32767;        /* clip */
                }
                else
                {
                    /* Linear interpolation of look up table */
                    ind = Inlines.silk_RSHIFT(in_Q5, 5);
                    return (sigm_LUT_pos_Q15[ind] + Inlines.silk_SMULBB(sigm_LUT_slope_Q10[ind], in_Q5 & 0x1F));
                }
            }
        }
    }
}
