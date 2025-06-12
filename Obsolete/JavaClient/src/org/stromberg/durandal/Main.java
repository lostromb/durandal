/*
 * To change this template, choose Tools | Templates
 * and open the template in the editor.
 */
package org.stromberg.durandal;

import java.util.Scanner;
import stromberg.audio.AudioChunk;
import stromberg.audio.AudioUtils;
import stromberg.audio.IAudioPlayer;
import stromberg.audio.IMicrophone;
import stromberg.audio.JavaMicrophone;
import stromberg.audio.JavaSoundPlayer;
import stromberg.config.Configuration;

/**
 *
 * @author lostromb
 */
public class Main
{
    public static void main(String[] args)
    {
        Configuration clientConfig = new Configuration("config.ini");
        int selection = 1;
        if (clientConfig.getBool("debugMode"))
        {
            System.out.println("Select an option:");
            System.out.println("1) Run headless audio client");
            System.out.println("2) Run mic / speaker check");
            System.out.println("3) Run debug client");
            Scanner scanner = new Scanner(System.in);
            selection = scanner.nextInt();
        }
        
        switch (selection)
        {
            case 1:
                HeadlessAudioClient client = new HeadlessAudioClient(clientConfig);
                client.run();
                break;
            case 2:
                runAudioCheck(clientConfig);
                break;
            case 3:
                //runDebugClient();
                break;
        }
        
        System.out.println("Shutting down...");
    }
    
    private static void runAudioCheck(Configuration config)
    {
        float amplify = (float)config.getDouble("microphonePreamp");
        System.out.println("Running audio check - type \'q\' to quit");
        IMicrophone audioIn = new JavaMicrophone(config.getInt("microphoneSampleRate"));
        IAudioPlayer audioOut = new JavaSoundPlayer(config.getInt("speakerSampleRate"));
        Scanner userInput = new Scanner(System.in);
        boolean running = true;
        while (running)
        {
            String input = userInput.nextLine();
            if (input.startsWith("q"))
            {
                running = false;
            }
            else
            {
                audioIn.startRecording();
                audioIn.clearBuffers();
                AudioChunk utterance = AudioUtils.recordUtteranceOfFixedLength(audioIn, 3000);
                audioIn.stopRecording();
                if (utterance == null)
                {
                    System.out.println("Didn't hear anything");
                }
                else
                {
                    utterance = utterance.amplify(amplify);
                    System.out.println("Got utterance");
                    float peak = utterance.peak() * 100 / (float)Short.MAX_VALUE;
                    System.out.println("Peak was " + peak + "%");
                    audioOut.playSound(utterance, false);
                }
            }
        }
    }
    
    /*private ClientRequest createTextQuery(String text, String clientId)
    {
        ClientRequest returnVal = new ClientRequest();
        returnVal.setSource(InputMethod.Typed);
        
        SpeechHypothesis speech = new SpeechHypothesis();
        speech.setUtterance(text);
        speech.setConfidence(1.0f);
        speech.setLexicalForm(text);
        ArrayList<SpeechHypothesis> speechHyps = new ArrayList<SpeechHypothesis>();
        speechHyps.add(speech);
        returnVal.setQueries(speechHyps);
        
        returnVal.setPreferredAudioCodec("sqrt");
        
        int capabilities =
                ClientCapabilities.DisplayUnlimitedText | 
                ClientCapabilities.HasInternetConnection |
                ClientCapabilities.SupportsCompressedAudio;
        returnVal.setClientContext(createClientContext(clientId, capabilities));
        
        return returnVal;
    }*/
    
    /*private static void runDebugClient()
    {
        Scanner consoleInput = new Scanner(System.in);
        boolean running = true;
        System.out.println("Commands:");
        System.out.println("q | quit");
        System.out.println("a | start recording audio");
        System.out.println("t <query> | send text query");
        System.out.println();
        
        while (running)
        {
            System.out.print("> ");
            String input = consoleInput.nextLine();
            
            if (input.startsWith("q"))
            {
                running = false;
            }
            else if (input.startsWith("a"))
            {
                audioOut.playSound(prompt, true);
                audioIn.clearBuffers();
                System.out.println("Recording utterance...");
                AudioChunk utterance = AudioUtils.recordUtteranceDynamic(audioIn);
                if (utterance == null || utterance.Data.length == 0)
                {
                    System.out.println("No audio recorded");
                }
                else
                {
                    System.out.println("Sending audio request to " + client.getConnectionString());
                    ClientResponse response = client.makeQueryRequest(createAudioQuery(utterance, clientId));
                    handleResponse(response, true);
                }
            }
            else if (input.startsWith("t"))
            {
                String utterance = input.replaceFirst("t +", "");
                ClientResponse response = client.makeQueryRequest(createTextQuery(utterance, clientId));
                handleResponse(response, false);
            }
        }
        audioIn.stopRecording();
    }*/
}
