using System;

namespace Durandal.API
{
    /// <summary>
    /// A set of flags that specifies certain capabilities of the Durandal client
    /// </summary>
    [Flags]
    public enum ClientCapabilities
    {
        None = 0x0,

        /// <summary>
        /// The client can display up to 100 chars to text data
        /// </summary>
        DisplayBasicText = 0x1,

        /// <summary>
        /// The client can display an unlimited amount of text data
        /// </summary>
        DisplayUnlimitedText = 0x1 << 1,

        /// <summary>
        /// The client can play audio
        /// </summary>
        HasSpeakers = 0x1 << 2,

        /// <summary>
        /// The client can record audio
        /// </summary>
        HasMicrophone = 0x1 << 3,

        /// <summary>
        /// The client can display basic HTML information (assume for all purposes that it's running IE6)
        /// </summary>
        DisplayBasicHtml = 0x1 << 4,

        /// <summary>
        /// The client can display advanced HTML5 information
        /// </summary>
        DisplayHtml5 = 0x1 << 5,

        /// <summary>
        /// The client has the ability to turn SSML into audio
        /// </summary>
        CanSynthesizeSpeech = 0x1 << 6,

        /// <summary>
        /// The client is able to locally execute specific actions that are passed in the ClientAction / SupportedClientAction fields
        /// </summary>
        ClientActions = 0x1 << 7,

        /// <summary>
        /// The client has its own Internet connection, and not just a direct connection to the dialog server.
        /// In other words, it can open URLs with scope = external
        /// </summary>
        HasInternetConnection = 0x1 << 8,

        /// <summary>
        /// The client can handle at least one compressed audio codec
        /// </summary>
        SupportsCompressedAudio = 0x1 << 9,

        /// <summary>
        /// This is a hint to the service that the client would prefer voice output
        /// to be verbose. Basically, assume the user is driving and can't look at the screen
        /// </summary>
        VerboseSpeechHint = 0x1 << 10,

        /// <summary>
        /// The client is able to generate RSA keypairs, store them persistently, and use them to authenticate with a server.
        /// </summary>
        RsaEnabled = 0x1 << 11,

        /// <summary>
        /// The client is able to serve and render HTML locally. If false, the dialog server should host the HTML and return a link instead
        /// </summary>
        ServeHtml = 0x1 << 12,

        /// <summary>
        /// The client is running on the same local machine as the dialog engine.
        /// This value cannot be set by the client. It is populated automatically when Dialog recieves a request.
        /// </summary>
        IsOnLocalMachine = 0x1 << 13,

        /// <summary>
        /// This is hint to the server that says "if there is a text response but no HTML, do not render an HTML page to display
        /// that text; I will render it myself"
        /// </summary>
        DoNotRenderTextAsHtml = 0x1 << 14,

        /// <summary>
        /// The client supports a StreamingAudioUrl in the response. Setting this flag will tell the server not
        /// to return raw audio data inside the response body, which greatly reduces response latency.
        /// </summary>
        SupportsStreamingAudio = 0x1 << 15,

        /// <summary>
        /// The client includes a web browser with which it is able to interoperate via Javascript's external object
        /// interface. This is necessary to support some advanced SPA and task form dialogues.
        /// </summary>
        JavascriptExtensions = 0x1 << 16,

        /// <summary>
        /// The client includes a keyword spotter which allows total hands-free use, and which can be programmed with
        /// contextual keywords (in other words, it can make use of the TriggerKeywords field in the response).
        /// </summary>
        KeywordSpotter = 0x1 << 17,

        /// <summary>
        /// The client can render html in an interactive way: animations, clickability, etc.
        /// The lack of this capability implies that all HTML will be rendered only as a static image card or similar.
        /// </summary>
        DynamicHtml = 0x1 << 18,
    }
}
