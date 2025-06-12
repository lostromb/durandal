namespace Durandal.API
{
    /// <summary>
    /// These are possible hints that can be set in the FormFactor field of a client context to
    /// determine what type of device is making the request.
    /// </summary>
    public enum FormFactor
    {
        Unknown = 0,

        /// <summary>
        /// A multipurpose desktop or laptop computer
        /// </summary>
        Desktop = 1,

        /// <summary>
        /// A tablet or phone; something you hold in your hand
        /// </summary>
        Portable = 2,

        /// <summary>
        /// Watch/band/Bluetooth device. Something small that is carried on you
        /// </summary>
        Wearable = 3,

        /// <summary>
        /// A web browser
        /// </summary>
        Browser = 4,

        /// <summary>
        /// Another Durandal server acting as a client
        /// </summary>
        Durandal = 5,

        /// <summary>
        /// A standalone device integrated with Durandal client, for example an RPi or set-top device
        /// </summary>
        Integrated = 6,

        /// <summary>
        /// A messaging protocol interface, like SMS, Telegram, Facebook, Discord, etc. May potentially have support for images and audio
        /// </summary>
        Messenger = 7
    }
}
