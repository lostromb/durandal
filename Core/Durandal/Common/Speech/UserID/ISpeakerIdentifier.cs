using Durandal.Common.Audio;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.UserID
{
    public interface ISpeakerIdentifier
    {
        Task<SpeakerIdentificationProfile> CreateSpeakerProfile(string locale);

        Task<bool> AddEnrollmentData(string speakerId, AudioSample audio);

        Task<SpeakerIdentificationProfile> GetProfileStatus(string speakerId);

        //Task<IList<Hypothesis<string>>> Identify(IList<string> speakerIds, ChunkedAudioStream audioStream);

        Task DeleteSpeakerProfile(string speakerId);
    }
}
