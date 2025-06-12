using System;
using System.IO;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    using Durandal.Common.Audio.Interfaces;

    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using System.Collections.Generic;
    using Durandal.Common.Tasks;
    using Speech.Triggers.Sphinx;
    using Logger;
    using Durandal.Common.Speech;
    using Durandal.Common.Time;
    using Durandal.Common.IO;

    public static class AudioUtils
    {
        public const int DURANDAL_INTERNAL_SAMPLE_RATE = 16000;

        /// <summary>
        /// Opens an audio stream that will actively read from the specified microphone, capture a single utterance,
        /// and stream the recording as it is in progress. The actual recording is done on a thread. This uses the 
        /// "dynamic" recorder, meaning that it will attempt to automatically detect when the user starts and stops
        /// speaking. The audio stream will be closed after the utterance is finished.
        /// </summary>
        /// <param name="audioSource">A microphone to record from. This is expected to be ON and RECORDING already. The microphone's state will not be changed by this method</param>
        /// <param name="internalSampleRate">The internal sample rate to use. If the microphone's hardware sample rate is different, the recorded audio will be resampled to this value. This also defines the sample rate of the output stream.</param>
        /// <returns>An audio stream that will carry the spoken audio as it is being recorded</returns>
        public static ChunkedAudioStream RecordUtteranceDynamic(IAudioInputDevice audioSource, IRealTimeProvider realTime, int internalSampleRate = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE)
        {
            ChunkedAudioStream returnVal = new ChunkedAudioStream();
            // Spin off a thread that will start the processing
            IRealTimeProvider threadLocalTime = realTime.Fork("RecordUtteranceDynamic");
            DurandalTaskExtensions.LongRunningTaskFactory.StartNew(
                async () =>
                {
                    try
                    {
                        await StreamUtterance(new DynamicUtteranceRecorder(), audioSource, returnVal, internalSampleRate, realTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });

            return returnVal;
        }

        /// <summary>
        /// Opens an audio stream that will actively read from the specified microphone, capture a single utterance,
        /// and stream the recording as it is in progress. The actual recording is done on a thread. This uses the 
        /// "static" recorder, which records for a fixed length only. The audio stream will be closed after the utterance is finished.
        /// </summary>
        /// <param name="audioSource">A microphone to record from. This is expected to be ON and RECORDING already. The microphone's state will not be changed by this method</param>
        /// <param name="internalSampleRate">The internal sample rate to use. If the microphone's hardware sample rate is different, the recorded audio will be resampled to this value. This also defines the sample rate of the output stream.</param>
        /// <returns>An audio stream that will carry the spoken audio as it is being recorded</returns>
        public static ChunkedAudioStream RecordUtteranceOfFixedLength(IAudioInputDevice audioSource, TimeSpan length, IRealTimeProvider realTime, int internalSampleRate = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE)
        {
            ChunkedAudioStream returnVal = new ChunkedAudioStream();
            // Spin off a thread that will start the processing
            IRealTimeProvider threadLocalTime = realTime.Fork("RecordUtteranceOfFixedLength");
            DurandalTaskExtensions.LongRunningTaskFactory.StartNew(
                async () =>
                {
                    try
                    {
                        await StreamUtterance(new StaticUtteranceRecorder((int)length.TotalMilliseconds), audioSource, returnVal, internalSampleRate, realTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            
            return returnVal;
        }

        public static ChunkedAudioStream RecordUtteranceUsingVad(IAudioInputDevice audioSource, IPocketSphinx sphinx, IRealTimeProvider realTime, int internalSampleRate = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE)
        {
            ChunkedAudioStream returnVal = new ChunkedAudioStream();
            // Spin off a thread that will start the processing
            IRealTimeProvider threadLocalTime = realTime.Fork("RecordUtteranceUsingVad");
            DurandalTaskExtensions.LongRunningTaskFactory.StartNew(
                async () =>
                {
                    try
                    {
                        await StreamUtterance(new VadUtteranceRecorder(sphinx), audioSource, returnVal, internalSampleRate, realTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });

            return returnVal;
        }

        private static async Task StreamUtterance(IUtteranceRecorder recorder, IAudioInputDevice audioSource, ChunkedAudioStream outputStream, int internalSampleRate, IRealTimeProvider realTime)
        {
            audioSource.ClearBuffers();
            RecorderState lastRecorderState = RecorderState.NotStarted;
            while (lastRecorderState == RecorderState.NotStarted ||
                lastRecorderState == RecorderState.Speaking)
            {
                AudioChunk chunk = await audioSource.ReadMicrophone(TimeSpan.FromMilliseconds(10), realTime).ConfigureAwait(false);
                lastRecorderState = recorder.ProcessInput(chunk);
                switch (lastRecorderState)
                {
                    case RecorderState.Error:
                        break;
                    default:
                        outputStream.Write(chunk);
                        break;
                }
            }
            outputStream.Write(null, true);
        }

        public static byte[] CompressAudioUsingStream(AudioChunk audio, IAudioCodec codec, out string encodeParams, Guid? traceId = null)
        {
            using (IAudioCompressionStream compressor = codec.CreateCompressionStream(audio.SampleRate, traceId))
            {
                return CompressAudioUsingStream(audio, compressor, out encodeParams);
            }
        }

        /// <summary>
        /// Sends an entire audio chunk through a compressor and returns the byte array output and encode params
        /// </summary>
        /// <param name="audio"></param>
        /// <param name="compressor"></param>
        /// <param name="encodeParams"></param>
        /// <returns></returns>
        public static byte[] CompressAudioUsingStream(AudioChunk audio, IAudioCompressionStream compressor, out string encodeParams)
        {
            if (compressor == null)
            {
                throw new NullReferenceException("IAudioCompressionStream");
            }

            encodeParams = compressor.GetEncodeParams();

            // Chunk the input and pass it to the stream
            const int CHUNK_SIZE = 320;
            short[] samples = new short[CHUNK_SIZE];
            IList<byte[]> outputChunks = new List<byte[]>();
            int totalOutputSize = 0;
            int input_ptr;
            for (input_ptr = 0; input_ptr < audio.DataLength - CHUNK_SIZE; input_ptr += CHUNK_SIZE)
            {
                Array.Copy(audio.Data, input_ptr, samples, 0, CHUNK_SIZE);
                AudioChunk sample = new AudioChunk(samples, audio.SampleRate);
                byte[] thisPacket = compressor.Compress(sample);
                if (thisPacket != null && thisPacket.Length > 0)
                {
                    outputChunks.Add(thisPacket);
                    totalOutputSize += thisPacket.Length;
                }
            }
            if (input_ptr < audio.DataLength)
            {
                short[] tail = new short[audio.DataLength - input_ptr];
                Array.Copy(audio.Data, input_ptr, tail, 0, tail.Length);
                AudioChunk sample = new AudioChunk(tail, audio.SampleRate);
                byte[] thisPacket = compressor.Compress(sample);
                if (thisPacket != null)
                {
                    outputChunks.Add(thisPacket);
                    totalOutputSize += thisPacket.Length;
                }
            }

            byte[] footer = compressor.Close();
            if (footer != null && footer.Length > 0)
            {
                outputChunks.Add(footer);
                totalOutputSize += footer.Length;
            }

            byte[] returnVal = new byte[totalOutputSize];
            int outCur = 0;
            foreach (byte[] chunk in outputChunks)
            {
                Array.Copy(chunk, 0, returnVal, outCur, chunk.Length);
                outCur += chunk.Length;
            }
            return returnVal;
        }

        public static AudioChunk DecompressAudioUsingStream(ArraySegment<byte> input, IAudioCodec codec, string encodeParams, Guid? traceId = null)
        {
            using (IAudioDecompressionStream decompressor = codec.CreateDecompressionStream(encodeParams, traceId))
            {
                return DecompressAudioUsingStream(input, decompressor);
            }
        }

        /// <summary>
        /// Sends an encoded audio sample through a decompressor and returns the decoded audio
        /// </summary>
        /// <param name="input"></param>
        /// <param name="decompressor"></param>
        /// <returns></returns>
        public static AudioChunk DecompressAudioUsingStream(ArraySegment<byte> input, IAudioDecompressionStream decompressor)
        {
            if (decompressor == null)
            {
                throw new NullReferenceException("IAudioDecompressionStream");
            }

            using (BucketAudioStream audioOut = new BucketAudioStream())
            {
                int outSampleRate = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE;

                const int blockSize = 4096;
                int input_ptr;
                for (input_ptr = 0; input_ptr < input.Count - blockSize; input_ptr += blockSize)
                {
                    AudioChunk thisPacket = decompressor.Decompress(new ArraySegment<byte>(input.Array, input.Offset + input_ptr, blockSize));
                    if (thisPacket != null && thisPacket.DataLength > 0)
                    {
                        audioOut.Write(thisPacket.Data);
                        outSampleRate = thisPacket.SampleRate;
                    }
                }
                if (input_ptr < input.Count)
                {
                    AudioChunk thisPacket = decompressor.Decompress(new ArraySegment<byte>(input.Array, input.Offset + input_ptr, input.Count - input_ptr));
                    if (thisPacket != null && thisPacket.DataLength > 0)
                    {
                        audioOut.Write(thisPacket.Data);
                        outSampleRate = thisPacket.SampleRate;
                    }
                }

                AudioChunk final = decompressor.Close();
                if (final != null && final.DataLength > 0)
                {
                    audioOut.Write(final.Data);
                    outSampleRate = final.SampleRate;
                }

                short[] allAudio = audioOut.GetAllData();

                return new AudioChunk(allAudio, outSampleRate);
            }
        }
    }
}
