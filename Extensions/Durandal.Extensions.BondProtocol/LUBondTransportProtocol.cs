using System;
using System.Collections.Generic;
using System.Text;
using Bond;
using Bond.IO.Safe;
using Bond.Protocols;
using Durandal.Common.LU;
using Durandal.Common.Logger;
using BONDAPI = Durandal.Extensions.BondProtocol.API;
using CAPI = Durandal.API;
using Durandal.Common.IO;

namespace Durandal.Extensions.BondProtocol
{
    public class LUBondTransportProtocol : ILUTransportProtocol
    {
        private static readonly int EXPECTED_PROTOCOL_VERSION = new CAPI.LURequest().ProtocolVersion;

        static LUBondTransportProtocol()
        {
            BondConverter.PrecacheSerializers<BONDAPI.LUResponse>();
            BondConverter.PrecacheSerializers<BONDAPI.LURequest>();
            BondConverter.PrecacheSerializers<BONDAPI.ProtocolVersionHeader>();
        }
        
        public string MimeType => "application/bond; proto=compact-binary";
        public string ProtocolName => "bond";

        public CAPI.LURequest ParseLURequest(ArraySegment<byte> input, ILogger logger)
        {
            BONDAPI.ProtocolVersionHeader header;
            // Peek at the protocol version field first
            BondConverter.DeserializeBond(input.Array, input.Offset, input.Count, out header, logger);

            if (header.ProtocolVersion < EXPECTED_PROTOCOL_VERSION)
            {
                // Incoming request is legacy.
                logger.Log("Incoming request is using an older protocol version (" + header.ProtocolVersion + "), this may not work", LogLevel.Wrn);
            }
            else if (header.ProtocolVersion > EXPECTED_PROTOCOL_VERSION)
            {
                // Incoming request is from the future
                logger.Log("Incoming request is using a protocol version from the future (" + header.ProtocolVersion + "), this may not work", LogLevel.Wrn);
            }

            BONDAPI.LURequest bondParsedRequest;
            if (!BondConverter.DeserializeBond(input.Array, input.Offset, input.Count, out bondParsedRequest, logger))
            {
                throw new Exception("Could not deserialize Bond LU request. It is possible that the server and client are using mismatched protocol versions.");
            }

            return BondTypeConverters.Convert(bondParsedRequest);
        }

        public PooledBuffer<byte> WriteLURequest(CAPI.LURequest input, ILogger logger)
        {
            BONDAPI.LURequest convertedInput = BondTypeConverters.Convert(input);
            return BondConverter.SerializeBondPooled(convertedInput, logger);
        }

        public CAPI.LUResponse ParseLUResponse(ArraySegment<byte> input, ILogger logger)
        {
            BONDAPI.ProtocolVersionHeader header;
            // Peek at the protocol version field first
            BondConverter.DeserializeBond(input.Array, input.Offset, input.Count, out header, logger);

            if (header.ProtocolVersion < EXPECTED_PROTOCOL_VERSION)
            {
                // Incoming request is legacy.
                logger.Log("Response is using an older protocol version (" + header.ProtocolVersion + "), this may not work", LogLevel.Wrn);
            }
            else if (header.ProtocolVersion > EXPECTED_PROTOCOL_VERSION)
            {
                // Incoming request is from the future
                logger.Log("Response is using a protocol version from the future (" + header.ProtocolVersion + "), this may not work", LogLevel.Wrn);
            }

            BONDAPI.LUResponse bondParsedRequest;
            if (!BondConverter.DeserializeBond(input.Array, input.Offset, input.Count, out bondParsedRequest, logger))
            {
                throw new Exception("Could not deserialize Bond LU response. It is possible that the server and client are using mismatched protocol versions.");
            }

            return BondTypeConverters.Convert(bondParsedRequest);
        }

        public PooledBuffer<byte> WriteLUResponse(CAPI.LUResponse input, ILogger logger)
        {
            BONDAPI.LUResponse convertedInput = BondTypeConverters.Convert(input);
            return BondConverter.SerializeBondPooled(convertedInput, logger);
        }
    }
}
