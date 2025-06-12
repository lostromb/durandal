using Durandal.API;
using Newtonsoft.Json.Linq;
using System;

namespace Durandal.Common.Client.Actions
{
    /// <summary>
    /// Schema for the SendAudioNextTurn JSON action
    /// </summary>
    public class SendNextTurnAudioAction : IJsonClientAction
    {
        public static readonly string ActionName = "SendAudioNextTurn";

        public string Name
        {
            get
            {
                return ActionName;
            }
        }
    }

    /// <summary>
    /// Schema for the StopListening JSON action
    /// </summary>
    public class StopListeningAction : IJsonClientAction
    {
        public static readonly string ActionName = "StopListening";
        
        public string Name
        {
            get
            {
                return ActionName;
            }
        }

        public int DurationSeconds { get; set; }
    }

    /// <summary>
    /// Schema for the ExecuteDelayedAction JSON action
    /// </summary>
    public class ExecuteDelayedAction : IJsonClientAction
    {
        public static readonly string ActionName = "ExecuteDelayedAction";

        public string Name
        {
            get
            {
                return ActionName;
            }
        }

        public int DelaySeconds { get; set; }
        public string ActionId { get; set; }

        private string _interactionMethod;

        /// <summary>
        /// Defines the interaction method for the executed action. Must be one of the enum values of InputMethod (Programmatic, Spoken, or Typed).
        /// This determines whether the device should speak, display text, or simply update the GUI
        /// </summary>
        public string InteractionMethod
        {
            get
            {
                return _interactionMethod;
            }

            set
            {
                InputMethod parsedValue;
                if (!Enum.TryParse(value, out parsedValue))
                {
                    throw new ArgumentException("Interaction method must be a valid string value of one of the InputMethod enumerations (Programmatic, Spoken, Typed)");
                }

                _interactionMethod = value;
            }
        }
    }

    /// <summary>
    /// Schema for the OAuthLogin JSON action
    /// </summary>
    public class OAuthLoginAction : IJsonClientAction
    {
        public static readonly string ActionName = "OAuthLogin";

        public string Name
        {
            get
            {
                return ActionName;
            }
        }

        /// <summary>
        /// The external login URL on the provider's website
        /// </summary>
        public string LoginUrl { get; set; }

        /// <summary>
        /// The name of the service you are logging into
        /// </summary>
        public string ServiceName { get; set; }
    }

    public class MSAPortableLoginAction : IJsonClientAction
    {
        public static readonly string ActionName = "MSAPortableLoginAction";

        public string Name
        {
            get
            {
                return ActionName;
            }
        }

        /// <summary>
        /// The token used to complete the login
        /// </summary>
        public string ExternalToken { get; set; }

        /// <summary>
        /// The ID of the dialog action to invoke after login successfully completes
        /// </summary>
        public string SuccessActionId { get; set; }

        /// <summary>
        /// Whether to use audio in the callback responses or not
        /// </summary>
        public bool? IsSpeechEnabled { get; set; }
    }

    public class ShowAdaptiveCardAction : IJsonClientAction
    {
        public static readonly string ActionName = "ShowAdaptiveCardAction";

        public string Name
        {
            get
            {
                return ActionName;
            }
        }
        
        public JToken Card { get; set; }
    }
}
