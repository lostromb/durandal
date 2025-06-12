using System;
using System.Collections.Generic;
using System.Speech.Synthesis;
using System.Threading;
using Durandal.API;
using Durandal.Common.Speech;
using Durandal.Common.Utils;
using Durandal.Common;
using System.Windows.Input;
using Durandal.Common.Net;

namespace Durandal.Common.Client
{
    public class DurandalAudioClient
    {
        private TriggerWordCollection triggers;
        private IMicrophone audioIn;
        private Configuration clientConfig;
        private bool showDebugInfo = false;
        private MultiTurnBehavior nextTurnBehavior = MultiTurnBehavior.None;
        private int retryCount = 0;
        private AudioChunk lastPrompt = new AudioChunk();
        private string _clientId;
        private string _clientName;
        private DialogHttpClient _clientInterface;
        private Cache<string> _pageCache;
        private PresentationWebServer _webServer;

        // TODO: Negotiate what happens with local/remote speech results
        private SpeechSynthesizer localSpeechSynth;

        public DurandalAudioClient(Configuration config, DialogHttpClient clientInterface)
        {
            _clientInterface = clientInterface;
            clientConfig = config;
            showDebugInfo = clientConfig.GetBool("showListenerInfo", false);
            localSpeechSynth = new SpeechSynthesizer();
            localSpeechSynth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult);
            _clientId = clientConfig.GetString("clientId", Guid.NewGuid().ToString("N"));
            _clientName = clientConfig.GetString("clientName", string.Empty);

            // Load the presentation server frontend
            _pageCache = new Cache<string>(10);
            _webServer = new PresentationWebServer(config, _pageCache, null);
        }

        public DialogHttpClient GetRawClient()
        {
            return _clientInterface;
        }

        private void Initialize()
        {
            // Start the presentation module
            _webServer.StartServer("Audio client presentation server");
            
            // Load the trigger words
            triggers = new TriggerWordCollection(".\\triggers\\", clientConfig);
            DataLogger.Log("Attempting to get audio line...");
            audioIn = new FilteredNAudioMicrophone(clientConfig,
                clientConfig.GetInt("microphoneSampleRate", 16000),
                clientConfig.GetFloat("microphonePreamp", 1.0f));
            audioIn.StartRecording();
            DataLogger.Log("Good!");
            GC.Collect();
        }

        private bool WaitForPrompt()
        {
            DataLogger.Log("Waiting for prompt...");
            audioIn.ClearBuffers();
            triggers.Reset();
            bool triggered = false;
            while (!triggered)
            {
                AudioChunk newAudio = audioIn.ReadMicrophone(triggers.ExpectedChunkSize);
                if (Keyboard.IsKeyDown(Key.F7))
                    return true;
                triggered = triggers.Try(newAudio);
            }
            return false;
        }

        public ServerResponse MakeCustomDialogRequest(string actionKey)
        {
            ClientRequest request = new ClientRequest();
            PopulateClientContext(ref request);
            request.ClientData["ActionKey"] = actionKey;
            ServerResponse response = _clientInterface.MakeDialogActionRequest(request);
            if (response == null)
            {
                Console.WriteLine("Response was null");
            }
            else if (response.ResponseCode == Result.Success)
            {
                Console.WriteLine("Client response OK!");
                if (!string.IsNullOrWhiteSpace(response.ResponseText))
                    Console.WriteLine(response.ResponseText);
            }
            else if (response.ResponseCode == Result.Skip)
            {
                Console.WriteLine("Input ignored (Shouldn't happen during a dialog action!)");
            }
            else
            {
                Console.WriteLine("An error occurred");
            }

            return response;
        }

        private void PopulateClientContext(ref ClientRequest request)
        {
            request.ClientFlags = (uint)(ClientCapabilities.IsOnLocalMachine |
                    ClientCapabilities.CanRecognizeSpeech |
                    ClientCapabilities.CanSynthesizeSpeech |
                    ClientCapabilities.HasDisplay);
            request.ClientId = _clientId;
            request.ClientData["ClientName"] = _clientName;
            request.ClientData["ClientLocale"] = "en-us";
            request.ClientData["ClientTime"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            request.ClientData["ClientLatitude"] = clientConfig.GetFloat("clientLatitude", 0).ToString();
            request.ClientData["ClientLongitude"] = clientConfig.GetFloat("clientLongitude", 0).ToString();

            // Send the current screen size
            //request.ClientData["ClientScreenWidth"] = ((int)SystemParameters.VirtualScreenWidth).ToString();
            //request.ClientData["ClientScreenHeight"] = ((int)SystemParameters.VirtualScreenHeight).ToString();
        }

        public void Run()
        {
            DataLogger.Log("Initializing audio client");
            Initialize();
            DataLogger.Log("Running client");
            bool manualTrigger = false;

            // Add a handler for resources that need to be disposed when the program closes
            using (new DisposerHook(audioIn, localSpeechSynth))
            {
                while (true)
                {
                    if (nextTurnBehavior.IsImmediate)
                    {
                        // Honor the suggested pause delay returned by the plugin, to a max of 4 seconds
                        int pauseAmount = 200;
                        if (nextTurnBehavior.SuggestedPauseDelay >= 0)
                            pauseAmount = Math.Max(0, Math.Min(4000, nextTurnBehavior.SuggestedPauseDelay));
                        Thread.Sleep(pauseAmount);
                    }

                    if (!nextTurnBehavior.IsImmediate)
                        manualTrigger = WaitForPrompt();
                    else
                        manualTrigger = false;

                    /*Console.WriteLine("Training..." + DateTime.Now.Ticks);
                    triggers.GiveFeedback(false);
                    continue;*/
                    AudioUtils.PlayWaveFile(".\\data\\Prompt.wav");

                    AudioChunk utterance = AudioUtils.RecordUtterance(audioIn);

                    // If all we got was silence, loop back
                    if (utterance == null)
                    {
                        DataLogger.Log("Silence / No utterance detected");
                        AudioUtils.PlayWaveFile(".\\data\\Fail.wav");

                        if (!nextTurnBehavior.Continues && !manualTrigger)
                        {
                            triggers.GiveFeedback(false); // Train the trigger to listen better next time
                        }

                        // Force multiturn conversations to continue
                        if (nextTurnBehavior.IsImmediate)
                        {
                            if (retryCount++ > nextTurnBehavior.MaxRetries)
                            {
                                // Too many retries
                                retryCount = 0;
                                nextTurnBehavior = MultiTurnBehavior.None;
                            }
                            else
                            {
                                localSpeechSynth.Speak("I didn't catch that.");
                                if (nextTurnBehavior.RepeatPromptOnRetry)
                                    AudioUtils.PlaySound(lastPrompt);
                            }
                        }

                        continue;
                    }

                    utterance.WriteToFile(".\\data\\lastutterance.wav");

                    IList<SpeechHypothesis> recoResults = SpeechUtils.UnderstandSpeech(utterance, clientConfig.GetInt("webRetryThreshold"));

                    if (recoResults.Count == 0)
                    {
                        DataLogger.Log("No speech reco result, ignoring...");
                        AudioUtils.PlayWaveFile(".\\data\\Fail.wav");

                        // TODO: If there is no speech reco on a multiturn, the entire conversation will be lost
                        // Worse still, LU doesn't know about the cancellation.
                        // As a placeholder, do an endless retry
                        // TODO Implement retries properly
                        if (nextTurnBehavior.IsImmediate)
                        {
                            if (retryCount++ > nextTurnBehavior.MaxRetries)
                            {
                                // Too many retries
                                retryCount = 0;
                                nextTurnBehavior = MultiTurnBehavior.None;
                            }
                            else
                            {
                                localSpeechSynth.Speak("I didn't catch that.");
                                if (nextTurnBehavior.RepeatPromptOnRetry)
                                    AudioUtils.PlaySound(lastPrompt);
                            }
                        }
                        else if (!manualTrigger)
                            triggers.GiveFeedback(false);
                    }
                    else
                    {
                        retryCount = 0;
                        int c = 0;
                        foreach (SpeechHypothesis result in recoResults)
                        {
                            DataLogger.Log("SpeechHypothesis[" + c++ + "] = \"" + result.Utterance + "\" " + result.Confidence);
                        }
                        ClientRequest request = new ClientRequest();
                        request.Queries = new List<SpeechHypothesis>();

                        // Technically not needed if we've already run speech reco
                        /*request.QueryAudio = new AudioData();
                        request.QueryAudio.SampleRate = utterance.SampleRate;
                        request.QueryAudio.WavData = new BondBlob(AudioChunk.ShortsToBytes(utterance.Data));*/

                        request.Queries.AddRange(recoResults);
                        PopulateClientContext(ref request);

                        ServerResponse cortanaResult = _clientInterface.MakeQueryRequest(request);
                        if (cortanaResult == null)
                        {
                            DataLogger.Log("Null response from server, assuming network error");
                            AudioUtils.PlayWaveFile(".\\data\\Fail.wav");
                            localSpeechSynth.Speak("Sorry, I can't connect right now");
                        }
                        else
                        {
                            ProcessResult(cortanaResult, manualTrigger);
                        }
                    }
                }
            }
        }

        private void ProcessResult(ServerResponse durandalResult, bool manualTrigger)
        {
            if (durandalResult.ResponseCode == Result.Success)
            {
                string clientURL = string.Empty;
                // Did the client return an action URL?
                if (durandalResult.ResponseData.ContainsKey("ActionURL"))
                {
                    // Set top open that URL
                    clientURL = durandalResult.ResponseData["ActionURL"];
                }

                // Did the client return an HTML page?
                if (durandalResult.ResponseData.ContainsKey("HTMLPage"))
                {
                    // Store the page in the cache and generate a URL to access it
                    string pageKey = _pageCache.Store(durandalResult.ResponseData["HTMLPage"]);
                    clientURL = _webServer.GetBaseURL() + "/dialog?page=" + pageKey;
                }

                if (!string.IsNullOrWhiteSpace(clientURL))
                {
                    //OpenURL(clientURL);
                }

                AudioUtils.PlayWaveFile(".\\data\\Confirm.wav");

                if (!nextTurnBehavior.Continues && !manualTrigger)
                {
                    triggers.GiveFeedback(true);
                }

                if (durandalResult.ResponseAudio != null && durandalResult.ResponseAudio.WavData.Count > 0)
                {
                    // Speak any text that was returned.
                    AudioChunk spokenChunk = new AudioChunk(durandalResult.ResponseAudio.WavData.Data.Array, durandalResult.ResponseAudio.SampleRate);
                    AudioUtils.PlaySound(spokenChunk);

                    if (durandalResult.NextTurnBehavior.IsImmediate)
                    {
                        lastPrompt = spokenChunk;
                    }
                }

                nextTurnBehavior = durandalResult.NextTurnBehavior;
            }
            else if (durandalResult.ResponseCode == Result.Skip)
            {
                // This usually happens when the utterance is tagged as side speech.
                // Just play the "cancel" sound and back out silently. Don't treat it
                // as a major error.
                AudioUtils.PlayWaveFile(".\\data\\Fail.wav");
                nextTurnBehavior = MultiTurnBehavior.None;
                if (!manualTrigger)
                {
                    triggers.GiveFeedback(false);
                }
            }
            else
            {
                localSpeechSynth.Speak("I'm sorry. An error occurred");
                nextTurnBehavior = MultiTurnBehavior.None;
            }
        }
    }
}
