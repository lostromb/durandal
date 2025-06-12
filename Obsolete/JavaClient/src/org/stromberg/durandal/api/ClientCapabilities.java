package org.stromberg.durandal.api;

/**
 *
 * @author lostromb
 */
public class ClientCapabilities
{
    public static final int None = 0x00000000;
    public static final int DisplayBasicText = 0x00000001;
    public static final int DisplayUnlimitedText = 0x00000002;
    public static final int HasSpeakers = 0x00000004;
    public static final int HasMicrophone = 0x00000008;
    public static final int DisplayBasicHtml = 0x00000010;
    public static final int DisplayHtml5 = 0x00000020;
    public static final int CanSynthesizeSpeech = 0x00000040;
    public static final int HasGps = 0x00000080;
    public static final int HasInternetConnection = 0x00000100;
    public static final int SupportsCompressedAudio = 0x00000200;
    public static final int VerboseSpeechHint = 0x00000400;
    public static final int RsaEnabled = 0x00000800;
    public static final int ServeHtml = 0x00001000;
    public static final int IsOnLocalMachine = 0x00002000;
    public static final int DoNotRenderTextAsHtml = 0x00004000;
}
