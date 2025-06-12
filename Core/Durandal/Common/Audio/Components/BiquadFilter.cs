using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Abstract biquad filter component. Subclasses of this should implement lowpass, highpass, bandpass, etc. filters
    /// Adapted from https://github.com/filoe/cscore/tree/master/CSCore/DSP (MS-PL license).
    /// </summary>
    public abstract class BiquadFilter : AbstractAudioSampleFilter
    {
        // only vectorize if CPU supported and channels > 1
        private readonly bool _isVectorized;

        // z1 and z1 values per-channel - padded to vector width if necessary
        private readonly float[] _z1;
        private readonly float[] _z2;

        // used in rare cases where we need to do partial vector copies
        private readonly float[] _maskedVectorBuf;

        /// <summary>
        /// Gets or sets the biquad coeffecients currently in use. Subclasses should update
        /// this field whenever their parameters change.
        /// </summary>
        protected BiquadCoefficients Coefficients { get; set; }

        public BiquadFilter(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string implementingTypeName, string nodeCustomName)
            : base(graph, implementingTypeName, nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            _isVectorized = Vector.IsHardwareAccelerated && format.NumChannels > 1;
            if (_isVectorized)
            {
                int paddedVecWidth = Vector<float>.Count * (format.NumChannels / Vector<float>.Count);
                if ((format.NumChannels % Vector<float>.Count) != 0)
                {
                    // pad out to at least a multiple of the vector width - this will almost
                    // always be the case unless we have, say, 8-channel input
                    paddedVecWidth += Vector<float>.Count;
                }

                _z1 = new float[paddedVecWidth];
                _z2 = new float[paddedVecWidth];

                if (format.NumChannels > Vector<float>.Count)
                {
                    _maskedVectorBuf = new float[Vector<float>.Count];
                }
            }
            else
            {
                _z1 = new float[format.NumChannels];
                _z2 = new float[format.NumChannels];
            }
        }

        protected abstract BiquadCoefficients CalculateBiQuadCoefficients();

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            float a0 = Coefficients.A0;
            float a1 = Coefficients.A1;
            float a2 = Coefficients.A2;
            float b1 = Coefficients.B1;
            float b2 = Coefficients.B2;

            using (PooledBuffer<float> pooledBuffer = BufferPool<float>.Rent())
            {
                float[] scratch = pooledBuffer.Buffer;
                int numChannels = InputFormat.NumChannels;
                int scratchSizeSamplesPerChannel = scratch.Length / numChannels;
                int samplesPerChannelRead = 0;
                while (samplesPerChannelRead < count)
                {
                    int samplesPerChannelCanRead = FastMath.Min(scratchSizeSamplesPerChannel, count - samplesPerChannelRead);
                    int samplesPerChannelActuallyRead = await Input.ReadAsync(scratch, 0, samplesPerChannelCanRead, cancelToken, realTime).ConfigureAwait(false);
                    if (samplesPerChannelActuallyRead <= 0)
                    {
                        // could be 0 samples available or end of stream, just pass it up the call chain
                        return samplesPerChannelRead;
                    }
                    else
                    {
                        int totalSamplesRead = samplesPerChannelActuallyRead * numChannels;
                        int currentBaseIndex = 0;

                        // Run a vectorized loop across all channels in parallel if possible
#if DEBUG
                        if (_isVectorized && FastRandom.Shared.NextBool())
#else
                        if (_isVectorized)
#endif
                        {
                            if (Vector<float>.Count >= numChannels)
                            {
                                // Typical case: just process all channels at once, one sample per channel at a time
                                // This writes a little bit of junk output ahead of where
                                // we process, but it's OK because we overwrite it later
                                // with valid data in the non-vectorized loop
                                int vectorEndBaseIndex = (numChannels * samplesPerChannelActuallyRead) - Vector<float>.Count;
                                Vector<float> vec_z1 = new Vector<float>(_z1);
                                Vector<float> vec_z2 = new Vector<float>(_z2);

                                while (currentBaseIndex <= vectorEndBaseIndex)
                                {
                                    Vector<float> vec_input = new Vector<float>(scratch, currentBaseIndex);
                                    Vector<float> vec_o = Vector.Add(
                                        Vector.Multiply(vec_input, a0), vec_z1);
                                    vec_z1 = Vector.Subtract(
                                        Vector.Add(
                                            Vector.Multiply(vec_input, a1), vec_z2),
                                        Vector.Multiply(vec_o, b1));
                                    vec_z2 = Vector.Subtract(
                                        Vector.Multiply(vec_input, a2),
                                        Vector.Multiply(vec_o, b2));

                                    vec_o.CopyTo(buffer, offset + currentBaseIndex);
                                    currentBaseIndex += numChannels;
                                }

                                vec_z1.CopyTo(_z1);
                                vec_z2.CopyTo(_z2);
                            }
                            else
                            {
                                // if # of channels is larger than vector width (rare), we have to do multiple passes of the input
                                // with some extra care around vector boundaries
                                int vectorEndBaseIndex = numChannels * samplesPerChannelActuallyRead - Vector<float>.Count + 1;
                                for (int zIdx = 0; zIdx < _z1.Length; zIdx += Vector<float>.Count)
                                {
                                    int outputWidth = FastMath.Min(Vector<float>.Count, numChannels - zIdx);
                                    currentBaseIndex = zIdx;
                                    Vector<float> vec_z1 = new Vector<float>(_z1, zIdx);
                                    Vector<float> vec_z2 = new Vector<float>(_z2, zIdx);

                                    while (currentBaseIndex < vectorEndBaseIndex)
                                    {
                                        Vector<float> vec_input = new Vector<float>(scratch, currentBaseIndex);
                                        Vector<float> vec_o = Vector.Add(
                                            Vector.Multiply(vec_input, a0), vec_z1);
                                        vec_z1 = Vector.Subtract(
                                            Vector.Add(
                                                Vector.Multiply(vec_input, a1), vec_z2),
                                            Vector.Multiply(vec_o, b1));
                                        vec_z2 = Vector.Subtract(
                                            Vector.Multiply(vec_input, a2),
                                            Vector.Multiply(vec_o, b2));

                                        // We have to do a masked copy here because likely only the first few values
                                        // of the vector are valid, and if we copied the entire vector it would
                                        // carry over junk data from the extra registers
                                        vec_o.CopyTo(_maskedVectorBuf);
                                        _maskedVectorBuf.AsSpan(0, outputWidth).CopyTo(buffer.AsSpan(offset + currentBaseIndex));
                                        currentBaseIndex += numChannels;
                                    }

                                    vec_z1.CopyTo(_z1, zIdx);
                                    vec_z2.CopyTo(_z2, zIdx);

                                    // have to adjust our final index so the non-vectorized code lines up properly
                                    currentBaseIndex -= zIdx;
                                }
                            }
                        }

                        // Process each sample across each interleaved channel
                        for (int channel = 0; channel < numChannels; channel++)
                        {
                            float z1 = _z1[channel];
                            float z2 = _z2[channel];
                            for (int sample = currentBaseIndex + channel; sample < totalSamplesRead; sample += numChannels)
                            {
                                float input = scratch[sample];
                                float o = input * a0 + z1;
                                z1 = input * a1 + z2 - b1 * o;
                                z2 = input * a2 - b2 * o;
                                buffer[offset + sample] = o;
                            }

                            _z1[channel] = z1;
                            _z2[channel] = z2;
                        }

                        offset += totalSamplesRead;
                        samplesPerChannelRead += samplesPerChannelActuallyRead;
                    }
                }

                return samplesPerChannelRead;
            }
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            float a0 = Coefficients.A0;
            float a1 = Coefficients.A1;
            float a2 = Coefficients.A2;
            float b1 = Coefficients.B1;
            float b2 = Coefficients.B2;

            using (PooledBuffer<float> pooledBuffer = BufferPool<float>.Rent())
            {
                float[] scratch = pooledBuffer.Buffer;
                int numChannels = InputFormat.NumChannels;
                int scratchSizeSamplesPerChannel = scratch.Length / numChannels;
                int samplesPerChannelWritten = 0;
                while (samplesPerChannelWritten < count)
                {
                    int samplesPerChannelCanWrite = FastMath.Min(scratchSizeSamplesPerChannel, count - samplesPerChannelWritten);
                    int currentBaseIndex = 0;

                    // Run a vectorized loop across all channels in parallel if possible
#if DEBUG
                    if (_isVectorized && FastRandom.Shared.NextBool())
#else
                    if (_isVectorized)
#endif
                    {
                        if (Vector<float>.Count >= numChannels)
                        {
                            // Typical case: just process all channels at once, one sample at a time
                            // This writes a little bit of junk output ahead of where
                            // we process, but it's OK because we overwrite it later
                            // with valid data in the non-vectorized loop
                            int vectorEndBaseIndex = (numChannels * samplesPerChannelCanWrite) - Vector<float>.Count;
                            Vector<float> vec_z1 = new Vector<float>(_z1);
                            Vector<float> vec_z2 = new Vector<float>(_z2);

                            while (currentBaseIndex <= vectorEndBaseIndex)
                            {
                                Vector<float> vec_input = new Vector<float>(buffer, offset + currentBaseIndex);
                                Vector<float> vec_o = Vector.Add(
                                    Vector.Multiply(vec_input, a0), vec_z1);
                                vec_z1 = Vector.Subtract(
                                    Vector.Add(
                                        Vector.Multiply(vec_input, a1), vec_z2),
                                    Vector.Multiply(vec_o, b1));
                                vec_z2 = Vector.Subtract(
                                    Vector.Multiply(vec_input, a2),
                                    Vector.Multiply(vec_o, b2));

                                vec_o.CopyTo(scratch, currentBaseIndex);
                                currentBaseIndex += numChannels;
                            }

                            vec_z1.CopyTo(_z1);
                            vec_z2.CopyTo(_z2);
                        }
                        else
                        {
                            // if # of channels is larger than vector width (rare), we have to do multiple passes of the input
                            // with some extra care around vector boundaries
                            int vectorEndBaseIndex = numChannels * (samplesPerChannelCanWrite - Vector<float>.Count + 1);
                            for (int zIdx = 0; zIdx < _z1.Length; zIdx += Vector<float>.Count)
                            {
                                int outputWidth = FastMath.Min(Vector<float>.Count, numChannels - zIdx);
                                currentBaseIndex = zIdx;
                                Vector<float> vec_z1 = new Vector<float>(_z1, zIdx);
                                Vector<float> vec_z2 = new Vector<float>(_z2, zIdx);

                                while (currentBaseIndex < vectorEndBaseIndex)
                                {
                                    Vector<float> vec_input = new Vector<float>(buffer, offset + currentBaseIndex);
                                    Vector<float> vec_o = Vector.Add(
                                        Vector.Multiply(vec_input, a0), vec_z1);
                                    vec_z1 = Vector.Subtract(
                                        Vector.Add(
                                            Vector.Multiply(vec_input, a1), vec_z2),
                                        Vector.Multiply(vec_o, b1));
                                    vec_z2 = Vector.Subtract(
                                        Vector.Multiply(vec_input, a2),
                                        Vector.Multiply(vec_o, b2));

                                    // We have to do a masked copy here because likely only the first few values
                                    // of the vector are valid, and if we copied the entire vector it would
                                    // carry over junk data from the extra registers
                                    vec_o.CopyTo(_maskedVectorBuf);
                                    _maskedVectorBuf.AsSpan(0, outputWidth).CopyTo(scratch.AsSpan(currentBaseIndex));
                                    currentBaseIndex += numChannels;
                                }

                                vec_z1.CopyTo(_z1, zIdx);
                                vec_z2.CopyTo(_z2, zIdx);

                                // have to adjust our final index so the non-vectorized code lines up properly
                                currentBaseIndex -= zIdx;
                            }
                        }
                    }

                    // Process each sample across each interleaved channel
                    int totalSamplesToProcess = samplesPerChannelCanWrite * numChannels;
                    for (int channel = 0; channel < numChannels; channel++)
                    {
                        float z1 = _z1[channel];
                        float z2 = _z2[channel];
                        for (int sample = currentBaseIndex + channel; sample < totalSamplesToProcess; sample += numChannels)
                        {
                            float input = buffer[offset + sample];
                            float o = input * a0 + z1;
                            z1 = input * a1 + z2 - b1 * o;
                            z2 = input * a2 - b2 * o;
                            scratch[sample] = o;
                        }

                        _z1[channel] = z1;
                        _z2[channel] = z2;
                    }

                    await Output.WriteAsync(scratch, 0, samplesPerChannelCanWrite, cancelToken, realTime).ConfigureAwait(false);

                    offset += totalSamplesToProcess;
                    samplesPerChannelWritten += samplesPerChannelCanWrite;
                }
            }
        }
    }
}
