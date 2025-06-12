/*
 * Adpcm-xq encoder / decoder from the Adpcm-xq-XQ project: https://github.com/dbry/adpcm-xq
 *
 *                        Copyright (c) 2022 David Bryant
 *                           All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 *     * Redistributions of source code must retain the above copyright notice,
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright notice,
 *       this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of Conifer Software nor the names of its contributors
 *       may be used to endorse or promote products derived from this software
 *       without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE REGENTS OR CONTRIBUTORS BE LIABLE FOR
 * ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 * OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using Durandal.Common.Utils;
using System;

namespace Durandal.Common.Audio.Codecs.ADPCM
{
    internal class Adpcm_xq
    {
        internal const int ADPCM_MAX_CHANNELS = 8;

        private static ushort[] step_table/*[89]*/ = {
            7, 8, 9, 10, 11, 12, 13, 14,
            16, 17, 19, 21, 23, 25, 28, 31,
            34, 37, 41, 45, 50, 55, 60, 66,
            73, 80, 88, 97, 107, 118, 130, 143,
            157, 173, 190, 209, 230, 253, 279, 307,
            337, 371, 408, 449, 494, 544, 598, 658,
            724, 796, 876, 963, 1060, 1166, 1282, 1411,
            1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024,
            3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484,
            7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
            15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794,
            32767
        };

        private static sbyte[] index_table = {
            /* adpcm data size is 4 */
            -1, -1, -1, -1, 2, 4, 6, 8
        };

        private static void CLIP(ref int data, int min, int max)
        {
            if (data > max)
                data = max;
            else if (data < min)
                data = min;
        }

        private static void CLIP(ref short data, short min, short max)
        {
            if (data > max)
                data = max;
            else if (data < min)
                data = min;
        }

        private static void CLIP(ref sbyte data, sbyte min, sbyte max)
        {
            if (data > max)
                data = max;
            else if (data < min)
                data = min;
        }

        internal struct adpcm_channel
        {
            public int pcmdata;       // current PCM value
            public int error;
            public int weight;
            public int history_0;  // for noise shaping
            public int history_1;
            public sbyte index;        // current index into step size table
        }

        internal class adpcm_context
        {
            public adpcm_channel[] channels;
            public int num_channels;
            public int lookahead;
            public NoiseShaping noise_shaping;

            /* Create Adpcm-xq encoder context with given number of channels.
             * The returned pointer is used for subsequent calls. Note that
             * even though an Adpcm-xq encoder could be set up to encode frames
             * independently, we use a context so that we can use previous
             * data to improve quality (this encoder might not be optimal
             * for encoding independent frames).
             */
            public adpcm_context(int num_channels, int lookahead, NoiseShaping noise_shaping, ReadOnlySpan<int> initial_deltas)
            {
                int ch;
                sbyte i;
                channels = new adpcm_channel[num_channels];
                this.noise_shaping = noise_shaping;
                this.num_channels = num_channels;
                this.lookahead = lookahead;

                // given the supplied initial deltas, search for and store the closest index

                for (ch = 0; ch < num_channels; ++ch)
                {
                    channels[ch] = new adpcm_channel();
                    for (i = 0; i <= 88; i++)
                    {
                        if (i == 88 || initial_deltas[ch] < ((int)step_table[i] + step_table[i + 1]) / 2)
                        {
                            channels[ch].index = i;
                            break;
                        }
                    }
                }
            }

            private void set_decode_parameters(ReadOnlySpan<int> init_pcmdata, ReadOnlySpan<sbyte> init_index)
            {
                int ch;

                for (ch = 0; ch < num_channels; ch++)
                {
                    channels[ch].pcmdata = init_pcmdata[ch];
                    channels[ch].index = init_index[ch];
                }
            }

            private void get_decode_parameters(Span<int> init_pcmdata, Span<sbyte> init_index)
            {
                int ch;

                for (ch = 0; ch < num_channels; ch++)
                {
                    init_pcmdata[ch] = channels[ch].pcmdata;
                    init_index[ch] = channels[ch].index;
                }
            }

            private byte encode_sample(int ch, ReadOnlySpan<float> sample, int num_samples)
            {
                // we need this method to write its changes to channels[ch]
                // inside this context object, but copy-by-value interferes with that.
                // so we make a local copy of channels[ch] and then write it back
                // to context at the end of this function
                adpcm_channel pchan_val = channels[ch];
                int csample = (int)(sample[0] * 32767.0f);
                CLIP(ref csample, -32767, 32767);
                int depth = num_samples - 1;
                byte nibble;
                ushort step = step_table[pchan_val.index];
                ushort trial_delta = (ushort)(step >> 3);

                if (noise_shaping == NoiseShaping.NOISE_SHAPING_DYNAMIC)
                {
                    int sam = (3 * pchan_val.history_0 - pchan_val.history_1) >> 1;
                    int temp = csample - (((pchan_val.weight * sam) + 512) >> 10);
                    int shaping_weight;

                    if (sam != 0 && temp != 0) pchan_val.weight -= (((sam ^ temp) >> 29) & 4) - 2;
                    pchan_val.history_1 = pchan_val.history_0;
                    pchan_val.history_0 = csample;

                    shaping_weight = (pchan_val.weight < 256) ? 1024 : 1536 - (pchan_val.weight * 2);
                    temp = -((shaping_weight * pchan_val.error + 512) >> 10);

                    if (shaping_weight < 0 && temp != 0)
                    {
                        if (temp == pchan_val.error)
                        {
                            temp = (temp < 0) ? temp + 1 : temp - 1;
                        }

                        pchan_val.error = -csample;
                        csample += temp;
                    }
                    else
                    {
                        pchan_val.error = -(csample += temp);
                    }
                }
                else if (noise_shaping == NoiseShaping.NOISE_SHAPING_STATIC)
                {
                    pchan_val.error = -(csample -= pchan_val.error);
                }

                if (depth > lookahead)
                    depth = lookahead;

                minimum_error(ref pchan_val, num_channels, csample, sample, depth, out nibble);

                if ((nibble & 0x1) != 0) trial_delta += (ushort)(step >> 2);
                if ((nibble & 0x2) != 0) trial_delta += (ushort)(step >> 1);
                if ((nibble & 0x4) != 0) trial_delta += (ushort)step;

                if ((nibble & 0x8) != 0)
                    pchan_val.pcmdata -= trial_delta;
                else
                    pchan_val.pcmdata += trial_delta;

                pchan_val.index += index_table[nibble & 0x07];
                CLIP(ref pchan_val.index, 0, 88);
                CLIP(ref pchan_val.pcmdata, -32767, 32767);

                if (noise_shaping != NoiseShaping.NOISE_SHAPING_OFF)
                {
                    pchan_val.error += pchan_val.pcmdata;
                }

                channels[ch] = pchan_val; // apply changes to local copy of channel struct back to context
                return nibble;
            }

            public static double minimum_error(ref adpcm_channel pchan, int nch, int csample, ReadOnlySpan<float> sample, int depth, out byte best_nibble)
            {
                int delta = csample - pchan.pcmdata;
                adpcm_channel chan = pchan; // copy-by-value is intended here, it's tricky
                ushort step = step_table[chan.index];
                ushort trial_delta = (ushort)(step >> 3);
                byte nibble, nibble2;
                double min_error;
                int clamped_sample;

                if (delta < 0)
                {
                    int mag = (-delta << 2) / step;
                    nibble = (byte)(0x8 | (mag > 7 ? 7 : mag));
                }
                else
                {
                    int mag = (delta << 2) / step;
                    nibble = (byte)(mag > 7 ? 7 : mag);
                }

                if ((nibble & 0x1) != 0) trial_delta += (ushort)(step >> 2);
                if ((nibble & 0x2) != 0) trial_delta += (ushort)(step >> 1);
                if ((nibble & 0x4) != 0) trial_delta += (ushort)step;

                if ((nibble & 0x8) != 0)
                {
                    chan.pcmdata -= trial_delta;
                }
                else
                {
                    chan.pcmdata += trial_delta;
                }

                CLIP(ref chan.pcmdata, -32767, 32767);
                best_nibble = nibble;
                min_error = (double)(chan.pcmdata - csample) * (chan.pcmdata - csample);

                if (depth != 0)
                {
                    chan.index += index_table[nibble & 0x07];
                    clamped_sample = (int)(sample[nch] * 32767.0f);
                    CLIP(ref clamped_sample, -32767, 32767);
                    CLIP(ref chan.index, 0, 88);
                    byte best_nibble_discard;
                    min_error += minimum_error(ref chan, nch, clamped_sample, sample.Slice(nch), depth - 1, out best_nibble_discard);
                }
                else
                {
                    return min_error;
                }

                for (nibble2 = 0; nibble2 <= 0xF; ++nibble2)
                {
                    double error;

                    if (nibble2 == nibble)
                    {
                        continue;
                    }

                    chan = pchan; // restore original channel state
                    trial_delta = (ushort)(step >> 3);

                    if ((nibble2 & 0x1) != 0) trial_delta += (ushort)(step >> 2);
                    if ((nibble2 & 0x2) != 0) trial_delta += (ushort)(step >> 1);
                    if ((nibble2 & 0x4) != 0) trial_delta += step;

                    if ((nibble2 & 0x8) != 0)
                    {
                        chan.pcmdata -= trial_delta;
                    }
                    else
                    {
                        chan.pcmdata += trial_delta;
                    }

                    CLIP(ref chan.pcmdata, -32767, 32767);

                    error = (double)(chan.pcmdata - csample) * (chan.pcmdata - csample);

                    if (error < min_error)
                    {
                        chan.index += index_table[nibble2 & 0x07];
                        clamped_sample = (int)(sample[nch] * 32767.0f);
                        CLIP(ref clamped_sample, -32767, 32767);
                        CLIP(ref chan.index, 0, 88);
                        byte best_nibble_discard;
                        error += minimum_error(ref chan, nch, clamped_sample, sample.Slice(nch), depth - 1, out best_nibble_discard);

                        if (error < min_error)
                        {
                            best_nibble = nibble2;
                            min_error = error;
                        }
                    }
                }

                return min_error;
            }

            private void encode_chunks(Span<byte> outbuf, ref int outbufsize, ReadOnlySpan<float> inbuf, int inbufcount)
            {
                int pcmbuf_idx;
                int inbuf_idx = 0;
                int chunks, ch, i;

                chunks = (inbufcount - 1) / 8;
                outbufsize += (chunks* 4) * num_channels;
                int outbufidx = 0;

                while (chunks-- != 0)
                {
                    for (ch = 0; ch < num_channels; ch++)
                    {
                        pcmbuf_idx = inbuf_idx + ch;

                        for (i = 0; i< 4; i++)
                        {
                            outbuf[outbufidx] = encode_sample(ch, inbuf.Slice(pcmbuf_idx), chunks * 8 + (3 - i) * 2 + 2);
                            pcmbuf_idx += num_channels;
                            outbuf[outbufidx] |= (byte)(encode_sample(ch, inbuf.Slice(pcmbuf_idx), chunks * 8 + (3 - i) * 2 + 1) << 4);
                            pcmbuf_idx += num_channels;
                            outbufidx++;
                        }
                    }

                    inbuf_idx += 8 * num_channels;
                }
            }

            /// <summary>
            /// Encode a block of 16-bit PCM data into 4-bit Adpcm-xq.
            /// </summary>
            /// <param name="outbuf">Destination buffer. Should be at least ((inbufCount + 8) * num_channels) / 2</param>
            /// <param name="outbufsize">After completion, will contain the number of bytes written.</param>
            /// <param name="inbuf">The input PCM sample data</param>
            /// <param name="inbufcount">The number of composite PCM frames provided (that is, total sample count divided by number of channels)</param>
            public void adpcm_encode_block(Span<byte> outbuf, out int outbufsize, ReadOnlySpan<float> inbuf, int inbufcount)
            {
                Span<int> init_pcmdata = stackalloc int[ADPCM_MAX_CHANNELS];
                Span<sbyte> init_index = stackalloc sbyte[ADPCM_MAX_CHANNELS];
                int clamped_sample;
                int ch;

                int inbuf_idx = 0;
                int outbuf_idx = 0;
                outbufsize = 0;

                if (inbufcount == 0)
                {
                    return;
                }

                get_decode_parameters(init_pcmdata, init_index);

                for (ch = 0; ch < num_channels; ch++)
                {
                    clamped_sample = (int)(inbuf[inbuf_idx] * 32767.0f);
                    CLIP(ref clamped_sample, -32767, 32767);
                    init_pcmdata[ch] = clamped_sample;
                    inbuf_idx++;

                    // my modification here
                    Span<byte> outbuf_2 = outbuf.Slice(outbuf_idx);
                    BinaryHelpers.Int16ToByteSpanLittleEndian((short)clamped_sample, ref outbuf_2);

                    // original code (relying on system endianness)
                    //outbuf[outbuf_idx + 0] = (byte)(init_pcmdata[ch] & 0xFF);
                    //outbuf[outbuf_idx + 1] = (byte)((init_pcmdata[ch] >> 8) & 0xFF);

                    outbuf[outbuf_idx + 2] = (byte)init_index[ch];
                    outbuf[outbuf_idx + 3] = 0;

                    outbuf_idx += 4;
                    outbufsize += 4;
                }

                set_decode_parameters(init_pcmdata, init_index);
                encode_chunks(outbuf.Slice(outbuf_idx), ref outbufsize, inbuf.Slice(inbuf_idx), inbufcount);
            }
        }

        /// <summary>
        /// Decode the block of Adpcm-xq data into PCM. This requires no context because Adpcm-xq blocks
        /// are independently decodable. This assumes that a single entire block is always decoded;
        /// it must be called multiple times for multiple blocks and cannot resume in the middle of a
        /// block.
        /// </summary>
        /// <param name="outbuf">destination for interleaved PCM samples</param>
        /// <param name="inbuf">source Adpcm-xq block</param>
        /// <param name="inbufsize">size of source Adpcm-xq block</param>
        /// <param name="channels">number of channels in block (must be determined from other context)</param>
        /// <returns>Returns number of converted composite samples (total samples divided by number of channels)</returns>
        public static int adpcm_decode_block(Span<float> outbuf, ReadOnlySpan<byte> inbuf, int inbufsize, int channels)
        {
            int ch;
            int samples = 1;
            int chunks;
            int outbuf_idx = 0;
            int inbuf_idx = 0;
            Span<int> pcmdata = stackalloc int[ADPCM_MAX_CHANNELS];
            Span<sbyte> index = stackalloc sbyte[ADPCM_MAX_CHANNELS];

            if (inbufsize < (uint)channels * 4)
            {
                return 0;
            }

            for (ch = 0; ch<channels; ch++)
            {
                ReadOnlySpan<byte> inbuf_2 = inbuf.Slice(inbuf_idx);
                pcmdata[ch] = BinaryHelpers.ByteSpanToInt16LittleEndian(ref inbuf_2);
                outbuf[outbuf_idx] = ((float)pcmdata[ch]) / 32767.0f;
                outbuf_idx++;
                index[ch] = (sbyte)inbuf[inbuf_idx + 2];

                // sanitize the input a little...
                if (index[ch] < 0 || index[ch] > 88 || inbuf[inbuf_idx + 3] != 0)
                {
                    return 0;
                }

                inbufsize -= 4;
                inbuf_idx += 4;
            }

            chunks = inbufsize / (channels* 4);
            samples += chunks* 8;

            while (chunks-- != 0)
            {
                int i;
                for (ch = 0; ch < channels; ++ch) {

                    for (i = 0; i< 4; ++i)
                    {
                        ushort step = step_table[index[ch]];
                        ushort delta = (ushort)(step >> 3);

                        byte thisInputByte = inbuf[inbuf_idx];
                        if ((thisInputByte & 0x1) != 0) delta += (ushort)(step >> 2);
                        if ((thisInputByte & 0x2) != 0) delta += (ushort)(step >> 1);
                        if ((thisInputByte & 0x4) != 0) delta += step;

                        if ((thisInputByte & 0x8) != 0)
                        {
                            pcmdata[ch] -= delta;
                        }
                        else
                        {
                            pcmdata[ch] += delta;
                        }

                        index[ch] += index_table[thisInputByte & 0x7];
                        CLIP(ref index[ch], 0, 88);
                        //CLIP(ref pcmdata[ch], -32768, 32767); // not strictly necessary when decoding to float
                        outbuf[outbuf_idx + (i * 2 * channels)] = ((float)pcmdata[ch]) / 32767.0f;

                        step = step_table[index[ch]];
                        delta = (ushort)(step >> 3);

                        if ((thisInputByte & 0x10) != 0) delta += (ushort)(step >> 2);
                        if ((thisInputByte & 0x20) != 0) delta += (ushort)(step >> 1);
                        if ((thisInputByte & 0x40) != 0) delta += step;

                        if ((thisInputByte & 0x80) != 0)
                            pcmdata[ch] -= delta;
                        else
                            pcmdata[ch] += delta;
                
                        index[ch] += index_table[(thisInputByte >> 4) & 0x7];
                        CLIP(ref index[ch], 0, 88);
                        //CLIP(ref pcmdata[ch], -32768, 32767);
                        outbuf[outbuf_idx + ((i * 2 + 1) * channels)] = ((float)pcmdata[ch]) / 32767.0f;
                        inbuf_idx++;
                    }

                    outbuf_idx++;
                }

                outbuf_idx += channels * 7;
            }

            return samples;
        }
    }
}