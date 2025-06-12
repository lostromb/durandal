using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Statistics;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.UserID
{
    public class CognitiveSpeakerIdentifier
    {
        private static readonly string COGNITIVE_SERVICES_URL_BASE = "https://westus.api.cognitive.microsoft.com";
        private static readonly string IDENTIFY_URL = "/spid/v1.0/identify?shortAudio=true&identificationProfileIds={0}";
        private static readonly string PROFILE_URL = "/spid/v1.0/identificationProfiles";
        private static readonly string PROFILE_GET_URL = "/spid/v1.0/identificationProfiles/{0}";
        private static readonly string ENROLL_URL = "/spid/v1.0/identificationProfiles/{0}/enroll?shortAudio=true";

        private readonly string _apiKey = "";
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;

        public CognitiveSpeakerIdentifier(IHttpClientFactory httpClientFactory, ILogger logger, string apiKey)
        {
            _httpClient = httpClientFactory.CreateHttpClient(new Uri(COGNITIVE_SERVICES_URL_BASE), logger);
            _apiKey = apiKey;
            _logger = logger;
        }

        public async Task<SpeakerIdentificationProfile> CreateSpeakerProfile(string locale, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(PROFILE_URL, "POST"))
            {
                request.RequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
                string content = "{ \"locale\": \"" + locale + "\" }"; // FIXME validate locale
                request.SetContent(content, HttpConstants.MIME_TYPE_JSON);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, cancelToken, realTime).ConfigureAwait(false))
                {
                    try
                    {
                        if (netResponse.Success && netResponse.Response.ResponseCode == 200)
                        {
                            HttpResponse response = netResponse.Response;
                            JObject responseObject = JObject.Parse(await response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false));
                            return new SpeakerIdentificationProfile()
                            {
                                SpeakerId = responseObject["identificationProfileId"].Value<string>(),
                                CreatedTime = DateTimeOffset.Now,
                                EnrollmentDataAccumulated = TimeSpan.Zero,
                                EnrollmentDataStillRequired = TimeSpan.Zero,
                                EnrollmentStatus = SpeakerEnrollmentStatus.InEnrollment,
                                LastUsageTime = DateTimeOffset.MinValue,
                                Locale = locale
                            };
                        }

                        return null;
                    }
                    finally
                    {
                        if (netResponse != null)
                        {
                            await netResponse.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        public async Task<bool> AddEnrollmentData(string speakerId, byte[] wavFile, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format(ENROLL_URL, speakerId), "POST"))
            {
                request.RequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
                //byte[] header = SimpleWaveReader.BuildRiffHeader(audio.DataLength * 2, audio.SampleRate);
                //byte[] audioBytes = audio.GetDataAsBytes();
                //request.PayloadData = new byte[header.Length + audioBytes.Length];
                //Array.Copy(header, 0, request.PayloadData, 0, header.Length);
                //Array.Copy(audioBytes, 0, request.PayloadData, header.Length, audioBytes.Length);
                request.SetContent(wavFile, "application/octet-stream");

                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, cancelToken, realTime).ConfigureAwait(false))
                {
                    try
                    {
                        if (netResponse.Success && netResponse.Response.ResponseCode == 200)
                        {
                            HttpResponse response = netResponse.Response;
                            _logger.Log(await response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false));
                            return true;
                        }

                        return false;
                    }
                    finally
                    {
                        if (netResponse != null)
                        {
                            await netResponse.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        public async Task<SpeakerIdentificationProfile> GetProfileStatus(string speakerId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format(PROFILE_GET_URL, speakerId), "GET"))
            {
                request.RequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
                using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(request, cancelToken, realTime).ConfigureAwait(false))
                {
                    try
                    {
                        if (netResponse.Success && netResponse.Response.ResponseCode == 200)
                        {
                            HttpResponse response = netResponse.Response;
                            JObject responseObject = JObject.Parse(await response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false));
                            SpeakerIdentificationProfile returnVal = new SpeakerIdentificationProfile();
                            returnVal.SpeakerId = responseObject["identificationProfileId"].Value<string>();
                            returnVal.Locale = responseObject["locale"].Value<string>();
                            returnVal.CreatedTime = DateTimeOffset.Parse(responseObject["createdDateTime"].Value<string>());
                            returnVal.LastUsageTime = DateTimeOffset.Parse(responseObject["lastActionDateTime"].Value<string>());
                            returnVal.EnrollmentDataAccumulated = TimeSpan.FromSeconds(responseObject["enrollmentSpeechTime"].Value<double>());
                            returnVal.EnrollmentDataStillRequired = TimeSpan.FromSeconds(responseObject["remainingEnrollmentSpeechTime"].Value<double>());
                            string enrollmentEnum = responseObject["enrollmentStatus"].Value<string>();
                            if (string.Equals(enrollmentEnum, "Enrolling"))
                            {
                                returnVal.EnrollmentStatus = SpeakerEnrollmentStatus.InEnrollment;
                            }
                            else if (string.Equals(enrollmentEnum, "Training"))
                            {
                                returnVal.EnrollmentStatus = SpeakerEnrollmentStatus.Training;
                            }
                            else if (string.Equals(enrollmentEnum, "Enrolled"))
                            {
                                returnVal.EnrollmentStatus = SpeakerEnrollmentStatus.Ready;
                            }
                            else
                            {
                                returnVal.EnrollmentStatus = SpeakerEnrollmentStatus.Unknown;
                            }

                            return returnVal;
                        }

                        return null;
                    }
                    finally
                    {
                        if (netResponse != null)
                        {
                            await netResponse.FinishAsync(CancellationToken.None, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        public async Task<IList<Hypothesis<string>>> Identify(IList<string> speakerIds, byte[] wavFile, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            List<Hypothesis<string>> returnVal = new List<Hypothesis<string>>();

            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format(IDENTIFY_URL, string.Join(",", speakerIds)), "POST"))
            {
                request.RequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
                request.SetContent(wavFile, "application/octet-stream");

                using (HttpResponse response = await _httpClient.SendRequestAsync(request, cancelToken, realTime).ConfigureAwait(false))
                {
                    try
                    {
                        if (response != null && response.ResponseCode == 202)
                        {
                            string operationUrl = response.ResponseHeaders["Operation-Location"];
                            using (HttpRequest statusRequest = HttpRequest.CreateOutgoing(operationUrl, "GET"))
                            {
                                statusRequest.RequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
                                await Task.Delay(1000).ConfigureAwait(false); // It normally takes at least a second to process the audio, so we might as well just sit here a while
                                for (int c = 0; c < 200; c++)
                                {
                                    using (HttpResponse statusResponse = await _httpClient.SendRequestAsync(statusRequest, cancelToken, realTime).ConfigureAwait(false))
                                    {
                                        try
                                        {
                                            if (statusResponse != null)
                                            {
                                                string responseStuff = await statusResponse.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                                                if (!string.IsNullOrEmpty(responseStuff))
                                                {
                                                    JObject responseJson = JObject.Parse(responseStuff);
                                                    if (responseJson["status"] != null && string.Equals(responseJson["status"].Value<string>(), "succeeded"))
                                                    {
                                                        JObject processingResult = (JObject)responseJson["processingResult"];
                                                        string profileId = processingResult["identifiedProfileId"].Value<string>();
                                                        string confidence = processingResult["confidence"].Value<string>();
                                                        float conf = 0;
                                                        if (string.Equals(confidence, "High"))
                                                        {
                                                            conf = 0.9f;
                                                        }
                                                        else if (string.Equals(confidence, "Normal"))
                                                        {
                                                            conf = 0.7f;
                                                        }
                                                        else if (string.Equals(confidence, "Low"))
                                                        {
                                                            conf = 0.5f;
                                                        }

                                                        returnVal.Add(new Hypothesis<string>(profileId, conf));
                                                        return returnVal;
                                                    }
                                                }
                                            }
                                        }
                                        finally
                                        {
                                            if (statusResponse != null)
                                            {
                                                await statusResponse.FinishAsync(CancellationToken.None, realTime).ConfigureAwait(false);
                                            }
                                        }

                                        await Task.Delay(100).ConfigureAwait(false);
                                    }
                                }

                                return returnVal;
                            }
                        }

                        return returnVal;
                    }
                    finally
                    {
                        if (response != null)
                        {
                            await response.FinishAsync(CancellationToken.None, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        public Task DeleteSpeakerProfile(string speakerId)
        {
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
