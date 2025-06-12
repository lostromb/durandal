using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Client
{
    public enum ClientInteractionState
    {
        /// <summary>
        /// The client has not been initialized
        /// </summary>
        NotStarted,

        /// <summary>
        /// The client is initializing or handshaking with the dialog server
        /// </summary>
        Initializing,

        /// <summary>
        /// The client is idly waiting for a user interaction. If audio is enabled, this means
        /// that the microphone is listening and audio is being continually processed by the trigger
        /// </summary>
        WaitingForUserInput,

        /// <summary>
        /// The client is actively recording an audio query by the user and performing SR
        /// </summary>
        RecordingUtterance,

        /// <summary>
        /// The client is sending a request to dialog or waiting for a response to come back
        /// </summary>
        MakingRequest,

        /// <summary>
        /// The client is playing a primary audio response from the dialog server
        /// </summary>
        PlayingAudio,

        /// <summary>
        /// The client has just finished playing the dialog response audio and is waiting for
        /// a little bit before it prompts the user for more input
        /// </summary>
        DelayBeforePrompt
    }
}
