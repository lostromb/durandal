using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Frames
{
    /// <summary>
    /// Defines an HTTP/2 SETTINGS frame.
    /// </summary>
    public class Http2SettingsFrame : Http2Frame
    {
        /// <inheritdoc />
        public override Http2FrameType FrameType => Http2FrameType.Settings;

        /// <summary>
        /// When set, bit 0 indicates that this frame acknowledges receipt and application of the peer's SETTINGS frame.
        /// When this bit is set, the payload of the SETTINGS frame MUST be empty. Receipt of a SETTINGS frame with the
        /// ACK flag set and a length field value other than 0 MUST be treated as a connection error of type FRAME_SIZE_ERROR.
        /// </summary>
        public bool Ack { get; private set; }

        /// <summary>
        /// The actual settings (may be null if this is just an ACK frame)
        /// </summary>
        public Http2Settings Settings { get; private set; }

        /// <summary>
        /// Creates an outgoing HTTP2 settings ACK frame (does not send any setting values)
        /// </summary>
        /// <returns></returns>
        public static Http2SettingsFrame CreateOutgoingAckFrame()
        {
            return new Http2SettingsFrame(
                NetworkDirection.Outgoing,
                null,
                flags: 0x1,
                streamId: 0,
                settings: null,
                ack: true);
        }

        /// <summary>
        /// Creates an outgoing HTTP2 settings frame with the given settings (or null, to send default settings)
        /// </summary>
        /// <param name="settings">The settings to specify (if null, will use default settings)</param>
        /// <param name="serializeAllSettings">Whether to explicitly serialize all settings with default values</param>
        /// <returns></returns>
        public static Http2SettingsFrame CreateOutgoingSettingsFrame(Http2Settings settings, bool serializeAllSettings = false)
        {
            settings = settings.AssertNonNull(nameof(settings));
            PooledBuffer<byte> serializedSettings = Http2Helpers.SerializeSettings(settings, serializeAllSettings);
            return new Http2SettingsFrame(
                NetworkDirection.Outgoing,
                serializedSettings,
                flags: 0,
                streamId: 0,
                settings: settings,
                ack: false);
        }

        /// <summary>
        /// Creates a SETTINGS frame using data read from a socket
        /// </summary>
        /// <param name="payload">The raw frame data</param>
        /// <param name="flags">The flag bitfield</param>
        /// <param name="streamId">The associated stream ID (must be 0)</param>
        /// <param name="isServer">Whether the local HTTP2 endpoing is acting as a server</param>
        /// <returns>The parsed settings</returns>
        /// <exception cref="Http2ProtocolException"></exception>
        public static Http2SettingsFrame CreateIncoming(PooledBuffer<byte> payload, byte flags, int streamId, bool isServer)
        {
            bool ack = (flags & 0x1) != 0;
            Http2Settings parsedSettings = null;
            // handle errors at the session level, not at the frame level here
            //if (ack)
            //{
            //    if (payload != null && payload.Length > 0)
            //    {
            //        throw new Http2ProtocolException("Incoming SETTINGS frame contained a payload as well as Ack flag");
            //    }
            //}
            //else
            //{
            //    parsedSettings = Http2Helpers.ParseSettings(payload);
            //}
            parsedSettings = Http2Helpers.ParseSettings(payload, isServer);
            return new Http2SettingsFrame(NetworkDirection.Incoming, payload, flags, streamId, parsedSettings, ack);
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="payload"></param>
        /// <param name="flags"></param>
        /// <param name="streamId"></param>
        /// <param name="settings"></param>
        /// <param name="ack"></param>
        private Http2SettingsFrame(NetworkDirection direction, PooledBuffer<byte> payload, byte flags, int streamId, Http2Settings settings, bool ack)
            : base(direction, payload, flags, streamId)
        {
            // handle errors at the session level, not at the frame level here
            //if (streamId != 0)
            //{
            //    throw new Http2ProtocolException("SETTINGS frame must have stream ID of 0");
            //}

            Settings = settings;
            Ack = ack;
        }
    }
}
