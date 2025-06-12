//using Durandal.Common.Audio;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Durandal.Common.Audio;
//using Durandal.Common.Net;
//using Durandal.Common.Logger;
//using Durandal.Common.Tasks;
//using System.Threading;
//using Durandal.Common.Utils;
//using Durandal.Common.Net.WebSocket;
//using Durandal.Common.Time;

//namespace Durandal.Common.Speech.SR.Cortana
//{
//    public class CortanaSpeechRecognizer : ISpeechRecognizer
//    {
//        private const int FINAL_READ_TIMEOUT = 2000;
        
//        private readonly ILogger _logger;
//        private readonly string _locale;
//        private readonly SemaphoreSlim _finalResponseSignal = new SemaphoreSlim(0, 1);
        
//        private TrumanWebSocketClient _webSocket = null;
//        private ConversationResponse _finalResponse = null;
//        private string _lastIntermediateResponse = string.Empty;
//        private int _disposed = 0;

//        private CortanaSpeechRecognizer(ISocket baseSocket, string locale, ILogger logger, string authToken, IRealTimeProvider realTime)
//        {
//            baseSocket.ReceiveTimeout = 5000;
//            _webSocket = new TrumanWebSocketClient(baseSocket, authToken, locale, logger.Clone("CUSocket"), GotWebsocketMessage, realTime);
//            _logger = logger;
//            _locale = locale;
//        }

//        ~CortanaSpeechRecognizer()
//        {
//            Dispose(false);
//        }

//        private Task<bool> OpenConnection()
//        {
//            return _webSocket.OpenStream();
//        }

//        public static async Task<CortanaSpeechRecognizer> OpenConnection(ISocket baseSocket, string locale, string authToken, ILogger logger, IRealTimeProvider realTime)
//        {
//            CortanaSpeechRecognizer returnVal = new CortanaSpeechRecognizer(baseSocket, locale, logger, authToken, realTime);
//            if (await returnVal.OpenConnection().ConfigureAwait(false))
//            {
//                return returnVal;
//            }

//            returnVal.Dispose();
//            return null;
//        }

//        public Task<string> ContinueUnderstandSpeech(AudioChunk continualData)
//        {
//            _webSocket.SendAudio(continualData, false);
//            return Task.FromResult(_lastIntermediateResponse);
//        }

//        public async Task<Durandal.API.SpeechRecognitionResult> FinishUnderstandSpeech(AudioChunk continualData = null)
//        {
//            try
//            {
//                _webSocket.SendAudio(continualData, true);
//                if (!(await _finalResponseSignal.WaitAsync(FINAL_READ_TIMEOUT).ConfigureAwait(false)))
//                {
//                    // Final response timed out
//                    return new Durandal.API.SpeechRecognitionResult()
//                    {
//                        RecognitionStatus = API.SpeechRecognitionStatus.BabbleTimeout
//                    };
//                }

//                if (_finalResponse == null || _finalResponse.SpeechRecognitionResult == null ||
//                    _finalResponse.SpeechRecognitionResult.RecognitionStatus != 200)
//                {
//                    // Reco failed for other reason
//                    return new Durandal.API.SpeechRecognitionResult()
//                    {
//                        RecognitionStatus = API.SpeechRecognitionStatus.Error
//                    };
//                }

//                Durandal.API.SpeechRecognitionResult returnVal = Convert(_finalResponse);
                
//                return returnVal;
//            }
//            finally
//            {
//                _webSocket.Dispose();
//            }
//        }

//        public void Dispose()
//        {
//            Dispose(true);
//            GC.SuppressFinalize(this);
//        }

//        protected virtual void Dispose(bool disposing)
//        {
//            if (!AtomicOperations.ExecuteOnce(ref _disposed))
//            {
//                return;
//            }

//            if (disposing)
//            {
//                if (_webSocket != null)
//                {
//                    _webSocket.Dispose();
//                }
                
//                _finalResponseSignal.Dispose();
//            }
//        }

//        private Durandal.API.SpeechRecognitionResult Convert(ConversationResponse input)
//        {
//            Durandal.API.SpeechRecognitionResult returnVal = new Durandal.API.SpeechRecognitionResult();
//            if (input.ConfusionNetworkResult != null)
//            {
//                returnVal.ConfusionNetworkData = new Durandal.API.ConfusionNetwork();
//                foreach (ConfusionNetworkArc arc in input.ConfusionNetworkResult.Arcs)
//                {
//                    returnVal.ConfusionNetworkData.Arcs.Add(new Durandal.API.ConfusionNetworkArc()
//                    {
//                        IsLastArc = arc.IsLastArc,
//                        NextNodeIndex = arc.NextNodeIndex,
//                        PreviousNodeIndex = arc.PreviousNodeIndex,
//                        Score = arc.Score,
//                        WordStartIndex = arc.WordStartIndex
//                    });
//                }
//                foreach (ConfusionNetworkNode node in input.ConfusionNetworkResult.Nodes)
//                {
//                    returnVal.ConfusionNetworkData.Nodes.Add(new Durandal.API.ConfusionNetworkNode()
//                    {
//                        AudioTimeOffset = (uint)node.AudioTimeOffset.TotalMilliseconds,
//                        FirstFollowingArc = node.FirstFollowingArc
//                    });
//                }
//                returnVal.ConfusionNetworkData.BestArcsIndexes = input.ConfusionNetworkResult.BestArcsIndexes;
//                returnVal.ConfusionNetworkData.WordTable = input.ConfusionNetworkResult.WordTable;
//            }

//            if (input.SpeechRecognitionResult != null)
//            {
//                switch (input.SpeechRecognitionResult.RecognitionStatus)
//                {
//                    case 0:
//                    case 100: // Intermediate
//                        returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.None;
//                        break;
//                    case 200:
//                        returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.Success;
//                        break;
//                    case 201:
//                        returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.Cancelled;
//                        break;
//                    case 301:
//                        returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.NoMatch;
//                        break;
//                    case 303:
//                        returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.InitialSilenceTimeout;
//                        break;
//                    case 304:
//                    case 305: // Hot word maximum time
//                        returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.BabbleTimeout;
//                        break;
//                    case 500:
//                        returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.Error;
//                        break;
//                    default:
//                        returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.None;
//                        break;
//                }

//                if (input.SpeechRecognitionResult.RecognizedPhrases != null)
//                {
//                    foreach (var inputPhrase in input.SpeechRecognitionResult.RecognizedPhrases)
//                    {
//                        Durandal.API.SpeechRecognizedPhrase newPhrase = new Durandal.API.SpeechRecognizedPhrase();
//                        newPhrase.AudioTimeLength = inputPhrase.MediaDuration;
//                        newPhrase.AudioTimeOffset = inputPhrase.StartTime;
//                        newPhrase.DisplayText = inputPhrase.DisplayText;
//                        newPhrase.InverseTextNormalizationResults = inputPhrase.InverseTextNormalizationResults;
//                        newPhrase.IPASyllables = inputPhrase.LexicalForm;
//                        newPhrase.Locale = _locale;
//                        newPhrase.MaskedInverseTextNormalizationResults = inputPhrase.MaskedInverseTextNormalizationResults;
//                        if (inputPhrase.PhraseElements != null)
//                        {
//                            newPhrase.PhraseElements = new List<Durandal.API.SpeechPhraseElement>();
//                            foreach (var inputElement in inputPhrase.PhraseElements)
//                            {
//                                Durandal.API.SpeechPhraseElement newElement = new Durandal.API.SpeechPhraseElement();
//                                newElement.DisplayText = inputElement.DisplayText;
//                                newElement.IPASyllables = inputElement.LexicalForm;
//                                newElement.Pronunciation = null;
//                                newElement.AudioTimeLength = inputElement.MediaDuration;
//                                newElement.AudioTimeOffset = inputElement.AudioTimeOffset;
//                                newElement.SREngineConfidence = inputElement.SREngineConfidence;
//                                newPhrase.PhraseElements.Add(newElement);
//                            }
//                        }
//                        newPhrase.ProfanityTags = null;
//                        newPhrase.SREngineConfidence = inputPhrase.Confidence;
//                        returnVal.RecognizedPhrases.Add(newPhrase);
//                    }
//                }
//            }

//            return returnVal;
//        }

//        /// <summary>
//        /// Processes an incoming websocket message
//        /// </summary>
//        /// <param name="message"></param>
//        /// <returns>True if we expect more messages to be sent by the remote server</returns>
//        private bool GotWebsocketMessage(WebSocketMessage message)
//        {
//            //_logger.Log("GOT PACKET: " + Enum.GetName(typeof(WebSocketOpcode), message.Opcode));
//            //string ps = Encoding.UTF8.GetString(message.Data, 0, message.Data.Length);
//            //_logger.Log(ps);
//            try
//            {
//                TrumanWebSocketMessage cumsg = TrumanWebSocketMessage.Parse(message);
//                string responseTypeHeader = null;
//                if (!cumsg.Headers.TryGetValue("X-Lobby-ServiceResponseType", out responseTypeHeader))
//                {
//                    if (!cumsg.Headers.TryGetValue("X-CU-ResultType", out responseTypeHeader))
//                    {
//                        _logger.Log("Got unknown CU response headers; ignoring...", LogLevel.Wrn);
//                        foreach (var header in cumsg.Headers)
//                        {
//                            _logger.Log(header.Key + ": " + header.Value, LogLevel.Vrb);
//                        }

//                        _logger.Log(cumsg.Content, LogLevel.Vrb);
//                    }
//                }

//                if (responseTypeHeader != null)
//                {
//                    if (string.Equals(responseTypeHeader, "ConversationResponse"))
//                    {
//                        // Legacy path
//                        ConversationResponse response = ConversationResponse.ParseFromXml(cumsg.Content);
//                        _finalResponse = response;
//                        _finalResponseSignal.Release();
//                        return false;
//                    }
//                    else if (string.Equals(responseTypeHeader, "PhraseResult"))
//                    {
//                        // New path
//                        ConversationResponse response = ConversationResponse.ParseFromXml(cumsg.Content);
//                        _finalResponse = response;
//                        _finalResponseSignal.Release();
//                        return false;
//                    }
//                    else if (string.Equals(responseTypeHeader, "IntermediateResponse"))
//                    {
//                        // Legacy path
//                        IntermediateResponse response = IntermediateResponse.ParseFromXml(cumsg.Content);
//                        if (response != null && !string.IsNullOrEmpty(response.DisplayText))
//                        {
//                            _lastIntermediateResponse = response.DisplayText;
//                        }
//                    }
//                    else if (string.Equals(responseTypeHeader, "IntermediateResult"))
//                    {
//                        // New path
//                        IntermediateResponse response = IntermediateResponse.ParseFromXml(cumsg.Content);
//                        if (response != null && !string.IsNullOrEmpty(response.DisplayText))
//                        {
//                            _lastIntermediateResponse = response.DisplayText;
//                        }
//                    }
//                    else
//                    {
//                        _logger.Log("Got unknown service response type " + responseTypeHeader, LogLevel.Wrn);
//                    }
//                }
//            }
//            catch (Exception e)
//            {
//                _logger.Log(e, LogLevel.Err);
//            }

//            return true;
//        }
//    }
//}
