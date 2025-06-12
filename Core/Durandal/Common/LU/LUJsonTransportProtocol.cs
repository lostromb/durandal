using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Durandal.API;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Newtonsoft.Json;

namespace Durandal.Common.LU
{
    public class LUJsonTransportProtocol : ILUTransportProtocol
    {
        private static readonly int EXPECTED_PROTOCOL_VERSION = new LURequest().ProtocolVersion;
        private static readonly JsonSerializer JSON_SERIALIZER = new JsonSerializer();

        public string MimeType => "application/json";
        public string ProtocolName => "json";

        public LURequest ParseLURequest(ArraySegment<byte> input, ILogger logger)
        {
            LURequest parsedRequest;
            using (MemoryStream memStream = new MemoryStream(input.Array, input.Offset, input.Count, false))
            using (StreamReader streamReader = new StreamReader(memStream, StringUtils.UTF8_WITHOUT_BOM))
            using (JsonReader jsonReader = new JsonTextReader(streamReader))
            {
                parsedRequest = JSON_SERIALIZER.Deserialize<LURequest>(jsonReader);
            }

            if (parsedRequest.ProtocolVersion < EXPECTED_PROTOCOL_VERSION)
            {
                // Incoming request is legacy.
                logger.Log("Incoming request is using an older protocol version (" + parsedRequest.ProtocolVersion + "), this may not work", LogLevel.Wrn);
            }
            else if (parsedRequest.ProtocolVersion > EXPECTED_PROTOCOL_VERSION)
            {
                // Incoming request is from the future
                logger.Log("Incoming request is using a protocol version from the future (" + parsedRequest.ProtocolVersion + "), this may not work", LogLevel.Wrn);
            }

            return parsedRequest;
        }

        public PooledBuffer<byte> WriteLURequest(LURequest input, ILogger logger)
        {
            using (RecyclableMemoryStream memoryStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            using (Utf8StreamWriter writer = new Utf8StreamWriter(memoryStream))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JSON_SERIALIZER.Serialize(jsonWriter, input);
                jsonWriter.Flush();
                return memoryStream.ToPooledBuffer();
            }
        }

        public LUResponse ParseLUResponse(ArraySegment<byte> input, ILogger logger)
        {
            LUResponse parsedResponse;
            using (MemoryStream memStream = new MemoryStream(input.Array, input.Offset, input.Count, false))
            using (StreamReader streamReader = new StreamReader(memStream, StringUtils.UTF8_WITHOUT_BOM))
            using (JsonReader jsonReader = new JsonTextReader(streamReader))
            {
                parsedResponse = JSON_SERIALIZER.Deserialize<LUResponse>(jsonReader);
            }

            if (parsedResponse.ProtocolVersion < EXPECTED_PROTOCOL_VERSION)
            {
                // Incoming request is legacy.
                logger.Log("Response is using an older protocol version (" + parsedResponse.ProtocolVersion + "), this may not work", LogLevel.Wrn);
            }
            else if (parsedResponse.ProtocolVersion > EXPECTED_PROTOCOL_VERSION)
            {
                // Incoming request is from the future
                logger.Log("Response is using a protocol version from the future (" + parsedResponse.ProtocolVersion + "), this may not work", LogLevel.Wrn);
            }

            return parsedResponse;
        }

        public PooledBuffer<byte> WriteLUResponse(LUResponse input, ILogger logger)
        {
            using (RecyclableMemoryStream memoryStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            using (Utf8StreamWriter writer = new Utf8StreamWriter(memoryStream))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JSON_SERIALIZER.Serialize(jsonWriter, input);
                jsonWriter.Flush();
                return memoryStream.ToPooledBuffer();
            }
        }
    }
}
