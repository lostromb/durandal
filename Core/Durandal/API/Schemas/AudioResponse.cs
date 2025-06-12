namespace Durandal.API
{
    using Durandal.Common.Utils;
    using Durandal.Common.Audio;
    using Newtonsoft.Json;
    using System;
    using Durandal.Common.Audio.Codecs;
    using System.Threading.Tasks;
    using Durandal.Common.Logger;

    /// <summary>
    /// This class contains custom audio that may be passed by an answer plugin to be played on the client.
    /// It is basically the same format as the other audio types, except that it specifies an "ordering"
    /// parameter which defines how it will interact with TTS
    /// </summary>
    public class AudioResponse
    {
        public AudioData Data { get; set; }

        public AudioOrdering Ordering { get; set; }
        
        public AudioResponse(byte[] compressedData, string codec, string codecParams, AudioOrdering ordering = AudioOrdering.AfterSpeech)
        {
            Data = new AudioData();
            Data.Data = new ArraySegment<byte>(compressedData);
            Data.Codec = codec;
            Data.CodecParams = codecParams;
            Ordering = ordering;
        }

        /// <summary>
        /// Creates audio data from the given audio sample. The sample will be encoded to PCM data as a part of this operation.
        /// </summary>
        /// <param name="audio"></param>
        /// <param name="ordering"></param>
        /// <returns></returns>
        public static async Task<AudioResponse> CreateFromAudioSample(AudioSample audio, AudioOrdering ordering = AudioOrdering.AfterSpeech)
        {
            IAudioCodecFactory codecFactory = new RawPcmCodecFactory();
            AudioData data = await AudioHelpers.EncodeAudioSampleUsingCodec(audio, codecFactory, RawPcmCodecFactory.CODEC_NAME_PCM_S16LE, NullLogger.Singleton).ConfigureAwait(false);
            return new AudioResponse(data, ordering);
        }

        [JsonConstructor]
        public AudioResponse(AudioData data, AudioOrdering ordering = AudioOrdering.AfterSpeech)
        {
            Data = data;
            Ordering = ordering;
        }
    }
}
