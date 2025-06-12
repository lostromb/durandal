using Durandal.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Client.Actions
{
    /// <summary>
    /// EventArgs class representing a dialog action to be executed, expressed by action ID and interaction method.
    /// </summary>
    public class DialogActionEventArgs : EventArgs
    {
        public DialogActionEventArgs(string actionId, InputMethod interactionMethod)
        {
            ActionId = actionId;
            InteractionMethod = interactionMethod;
        }

        /// <summary>
        /// The dialog action ID, expressed as an arbitrary string (usually a guid)
        /// </summary>
        public string ActionId { get; set; }

        /// <summary>
        /// The dialog action interaction method.
        /// </summary>
        public InputMethod InteractionMethod { get; set; }
    }
}
