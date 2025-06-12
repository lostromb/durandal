/*
 * To change this template, choose Tools | Templates
 * and open the template in the editor.
 */
package org.stromberg.durandal;

import com.microsoft.bond.BondBlob;
import java.io.IOException;
import java.net.URLEncoder;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.HashMap;
import org.stromberg.durandal.api.*;
import org.stromberg.durandal.client.DialogHttpClient;
import org.stromberg.durandal.net.DurandalHttpRequest;
import org.stromberg.durandal.net.DurandalHttpResponse;
import org.stromberg.durandal.net.HttpClient;
import org.stromberg.durandal.security.ClientAuthenticator;
import stromberg.audio.AudioChunk;
import stromberg.audio.AudioUtils;
import stromberg.audio.IAudioPlayer;
import stromberg.audio.IMicrophone;
import stromberg.audio.JavaMicrophone;
import stromberg.audio.JavaSoundPlayer;
import stromberg.audio.SquareDeltaCodec;
import stromberg.audio.TimeSpan;
import stromberg.audio.sampling.Resampler;
import stromberg.config.Configuration;
import stromberg.util.BasicBuffer;
import stromberg.util.MovingAverage;

/**
 *
 * @author lostromb
 */
public class HeadlessAudioClient
{
    private Configuration config;
    private IMicrophone audioIn;
    private IAudioPlayer audioOut;
    private AudioChunk confirm;
    private AudioChunk fail;
    private AudioChunk prompt;
    private DialogHttpClient client;
    private SquareDeltaCodec audioCodec;
    private ClientAuthenticator authenticator;
    
    public HeadlessAudioClient(Configuration configuration)
    {
        config = configuration;
        client = new DialogHttpClient(config.getString("dialogHost"), config.getInt("dialogPort"));
        client.resetConversationState(config.getString("clientId"));
        
        confirm = new AudioChunk("./data/Confirm.wav");
        fail = new AudioChunk("./data/Fail.wav");
        prompt = new AudioChunk("./data/Prompt.wav");
        
        audioCodec = new SquareDeltaCodec();
        
        authenticator = new ClientAuthenticator(config.getString("clientId"), config.getString("clientName"));
    }
    
    public void run()
    {
        boolean running = true;
        
        System.out.println("Starting to run headless audio client...");
        System.out.println("Client id is " + config.getString("clientId"));
        System.out.println("Dialog host is " + config.getString("dialogHost") + ":" + config.getInt("dialogPort"));
        System.out.println("Trigger host is " + config.getString("triggerHost") + ":" + config.getInt("triggerPort"));
        
        System.out.println("Loading auth info...");
        if (!authenticator.loadPrivateKeyFromFile("client_authorization.xml"))
        {
            System.out.println("Failed!");
        }
        else
        {
            System.out.println("Success! Sending client handshake...");
            DialogHttpClient.AuthHelloResponse authHello = client.makeAuthHelloRequest(authenticator);
            if (authHello.Success)
            {
                if (authHello.SecondTurnRequired)
                {
                    System.out.println("Answering server challenge...");
                    if (client.makeAuthAnswerRequest(authenticator))
                    {
                        System.out.println("Client is now authenticated");
                    }
                    else
                    {
                        System.out.println("Auth_answer failed!");
                    }
                }
                else
                {
                    System.out.println("No second turn required (Client is already trusted)");
                }
            }
            else
            {
                System.out.println("Auth_hello failed!");
            }
        }
        
        float amplify = (float)config.getDouble("microphonePreamp");
        
        audioOut = new JavaSoundPlayer(config.getInt("speakerSampleRate"), config.getInt("outputMixerLine"));
        audioIn = new JavaMicrophone(config.getInt("microphoneSampleRate"), config.getInt("inputMixerLine"));
        audioIn.startRecording();

        HttpClient triggerClient = new HttpClient(config.getString("triggerHost"), config.getInt("triggerPort"));
        SquareDeltaCodec codec = new SquareDeltaCodec();
        
        System.out.println("Listening for input queries");
        System.out.println("(Any input to the console will stop the program)");
        
        // Send the hello request
        sendTriggerRequest(triggerClient, null);
        audioOut.playSound(confirm, false);
        
        while (running)
        {
            AudioChunk chunk = audioIn.readMicrophone(new TimeSpan(100));
            chunk = chunk.resampleTo(16000, Resampler.MAGIC);
            chunk = chunk.amplify(amplify);

            boolean triggered = false;
            //byte[] compressedAudio = codec.compress(chunk);

            DurandalHttpResponse triggerResponse = sendTriggerRequest(triggerClient, chunk.getDataAsBytes());
            if (triggerResponse != null && triggerResponse.ResponseCode == 200)
            {
                if (triggerResponse.ResponseHeaders.containsKey("Triggered") &&
                    triggerResponse.ResponseHeaders.get("Triggered").equalsIgnoreCase("true"))
                {
                    triggered = true;
                }
            }
            else if (config.getBool("debugMode"))
            {
                System.out.println("No response from trigger service");
            }

            if (triggered)
            {
                audioOut.playSound(prompt, false);
                audioIn.clearBuffers();
                System.out.println("Recording utterance...");
                AudioChunk utterance = AudioUtils.recordUtteranceDynamic(audioIn);
                if (utterance == null || utterance.Data.length == 0)
                {
                    System.out.println("No audio recorded");
                    audioOut.playSound(fail, false);
                }
                else
                {
                    utterance = utterance.normalize();
                    System.out.println("Sending audio request to " + client.getConnectionString());
                    ClientResponse response = client.makeQueryRequest(createAudioQuery(utterance));
                    handleResponse(response);
                }
            }
            
            /*else if (timeSinceLastQuery > config.getInt("triggerKeepAliveTime"))
            {
                lastQueryTime = System.currentTimeMillis();
                // Send a keepalive query every 30 seconds
                sendTriggerRequest(triggerClient, null);
            }*/
            
            try {
                if (System.in.available() > 0) { running = false; }
            } catch (IOException e) {}
            
        }
        
        authenticator.savePrivateKeyToFile("client_authorization.xml");
        audioIn.stopRecording();
    }
    
    private DurandalHttpResponse sendTriggerRequest(HttpClient triggerClient, byte[] payload)
    {
        DurandalHttpRequest thisRequest = new DurandalHttpRequest();
        thisRequest.RequestFile = "/trigger?c=" + config.getString("clientId");
        thisRequest.RequestMethod = "POST";
        if (payload != null)
            thisRequest.PayloadData = payload;
        long startTime = System.currentTimeMillis();
        DurandalHttpResponse triggerResponse = triggerClient.sendRequest(thisRequest, 100);
        long endTime = System.currentTimeMillis();
        if (config.getBool("debugMode"))
        {
            System.out.println("Trigger latency: " + (endTime - startTime) + "ms");
        }
        return triggerResponse;
    }
    
    private void handleResponse(ClientResponse response)
    {
        if (response == null || response.getExecutionResult() != Result.Success)
        {
            System.out.println("Request failed");
            audioOut.playSound(fail, false);
        }
        else
        {
            System.out.println("Request succeeded");
            audioOut.playSound(confirm, false);
            if (response.getAudioToPlay() != null)
            {
                AudioData audioToPlay = response.getAudioToPlay();
                if (audioToPlay.getData() != null && audioToPlay.getData().size() > 0)
                {
                    // Is it compressed?
                    if (audioToPlay.getCodec().isEmpty())
                    {
                        AudioChunk responseAudio = new AudioChunk(audioToPlay.getData().getBuffer(), audioToPlay.getSampleRate());
                        audioOut.playSound(responseAudio, false);
                    }
                    else if (audioToPlay.getCodec().equals(audioCodec.getFormatCode()))
                    {
                        AudioChunk responseAudio = audioCodec.decompress(audioToPlay.getData().getBuffer());
                        responseAudio.SampleRate = audioToPlay.getSampleRate();
                        audioOut.playSound(responseAudio, false);
                    }
                    else
                    {
                        System.err.println("Response audio uses an unsupported codec \"" + audioToPlay.getCodec() + "\"");
                    }
                }
            }
        }
    }
    
    private ClientContext createClientContext(int flags)
    {
        ClientContext returnVal = new ClientContext();
        returnVal.setClientName(config.getString("clientName"));
        returnVal.setLocale(config.getString("locale"));
        returnVal.setClientId(config.getString("clientId"));
        returnVal.setUserId(config.getString("clientId"));
        returnVal.setLatitude(config.getDouble("clientLatitude"));
        returnVal.setLongitude(config.getDouble("clientLongitude"));
        returnVal.setReferenceDateTime(new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss").format(Calendar.getInstance().getTime()));
        returnVal.setUTCOffset(config.getInt("clientUtcOffset"));
        returnVal.setCapabilities(flags);
        
        HashMap<String, String> extraData = new HashMap<String, String>();
        extraData.put("FormFactor", "Integrated");
        returnVal.setData(extraData);
        
        return returnVal;
    }
    
    private ClientRequest createAudioQuery(AudioChunk audio)
    {
        ClientRequest returnVal = new ClientRequest();
        returnVal.setSource(InputMethod.Spoken);
        
        returnVal.setQueries(new ArrayList<SpeechHypothesis>());
        
        audio = audio.resampleTo(16000);
        byte[] compressedAudio = audioCodec.compress(audio);
        
        AudioData queryAudio = new AudioData();
        queryAudio.setSampleRate(audio.SampleRate);
        queryAudio.setCodec(audioCodec.getFormatCode());
        queryAudio.setCodecParams("");
        queryAudio.setData(new BondBlob(compressedAudio, 0, compressedAudio.length));
        returnVal.setQueryAudio(queryAudio);
        
        returnVal.setPreferredAudioCodec(audioCodec.getFormatCode());
        
        int capabilities =
                ClientCapabilities.HasInternetConnection |
                ClientCapabilities.HasSpeakers |
                ClientCapabilities.HasMicrophone |
                ClientCapabilities.SupportsCompressedAudio;
        returnVal.setClientContext(createClientContext(capabilities));
        
        return returnVal;
    }
}
