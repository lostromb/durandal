using System;
using System.Collections.Generic;
using System.Text;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.IO;
using Durandal.Common.Compression.LZ4;
using System.IO;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Utils;
using ZstdSharp;

namespace Durandal.Extensions.Compression.ZStandard.Dialog
{
    /// <summary>
    /// Wraps a ZStd compression layer around an existing dialog transport protocol, prefixing the protocol name with "zstd".
    /// This impementation can optionally use custom dictionaries which are shared common state on both ends to reduce
    /// the amount of data that actually needs to be sent on the wire.
    /// </summary>
    public abstract class ZstdDialogProtocolWrapper : IDialogTransportProtocol
    {
        private readonly IDialogTransportProtocol _innerProtocol;
        private readonly int _compressionLevel;
        private readonly byte[] _dictionary;

        public ZstdDialogProtocolWrapper(
            IDialogTransportProtocol innerProtocol,
            string protocolName,
            string contentEncoding = "zstd",
            byte[] dictionary = null,
            int compressionLevel = 8)
        {
            if (compressionLevel < Compressor.MinCompressionLevel ||
                compressionLevel > Compressor.MaxCompressionLevel)
            {
                throw new ArgumentOutOfRangeException("Zstd compression level must be between "
                    + Compressor.MinCompressionLevel + " and " + Compressor.MaxCompressionLevel);
            }

            _innerProtocol = innerProtocol;
            _compressionLevel = compressionLevel;
            ContentEncoding = contentEncoding.AssertNonNullOrEmpty(nameof(contentEncoding));
            ProtocolName = protocolName.AssertNonNullOrEmpty(nameof(protocolName));
            _dictionary = dictionary;
        }

        /// <inheritdoc />
        public string ContentEncoding { get; private set; }

        /// <inheritdoc />
        public string MimeType => _innerProtocol.MimeType;

        /// <inheritdoc />
        public string ProtocolName { get; private set; }

        /// <inheritdoc />
        public DialogRequest ParseClientRequest(PooledBuffer<byte> compressed, ILogger logger)
        {
            PooledBuffer<byte> uncompressed = Decompress(compressed.AsArraySegment);
            compressed.Dispose();
            return _innerProtocol.ParseClientRequest(uncompressed, logger);
        }

        /// <inheritdoc />
        public DialogRequest ParseClientRequest(ArraySegment<byte> compressed, ILogger logger)
        {
            PooledBuffer<byte> uncompressed = Decompress(compressed);
            return _innerProtocol.ParseClientRequest(uncompressed, logger);
        }

        /// <inheritdoc />
        public DialogResponse ParseClientResponse(PooledBuffer<byte> compressed, ILogger logger)
        {
            PooledBuffer<byte> uncompressed = Decompress(compressed.AsArraySegment);
            compressed.Dispose();
            return _innerProtocol.ParseClientResponse(uncompressed, logger);
        }

        /// <inheritdoc />
        public DialogResponse ParseClientResponse(ArraySegment<byte> compressed, ILogger logger)
        {
            PooledBuffer<byte> uncompressed = Decompress(compressed);
            return _innerProtocol.ParseClientResponse(uncompressed, logger);
        }

        /// <inheritdoc />
        public PooledBuffer<byte> WriteClientRequest(DialogRequest input, ILogger logger)
        {
            using (PooledBuffer<byte> uncompressed = _innerProtocol.WriteClientRequest(input, logger))
            {
                return Compress(uncompressed);
            }
        }

        /// <inheritdoc />
        public PooledBuffer<byte> WriteClientResponse(DialogResponse input, ILogger logger)
        {
            using (PooledBuffer<byte> uncompressed = _innerProtocol.WriteClientResponse(input, logger))
            {
                return Compress(uncompressed);
            }
        }

        private PooledBuffer<byte> Compress(PooledBuffer<byte> uncompressed)
        {
            using (RecyclableMemoryStream outStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (CompressionStream compressor = new CompressionStream(outStream, level: _compressionLevel))
                using (MemoryStream inStream = new MemoryStream(uncompressed.Buffer, 0, uncompressed.Length))
                {
                    if (_dictionary != null)
                    {
                        compressor.LoadDictionary(_dictionary);
                    }

                    Span<byte> lengthHeaderBytes = stackalloc byte[4];
                    BinaryHelpers.Int32ToByteSpanLittleEndian(uncompressed.Length, ref lengthHeaderBytes);
                    compressor.Write(lengthHeaderBytes);
                    inStream.CopyToPooled(compressor);
                }

                return outStream.ToPooledBuffer();
            }
        }

        private PooledBuffer<byte> Decompress(ArraySegment<byte> compressed)
        {
            using (RecyclableMemoryStream outStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (MemoryStream inStream = new MemoryStream(compressed.Array, compressed.Offset, compressed.Count))
                using (DecompressionStream decompressor = new DecompressionStream(inStream))
                {
                    if (_dictionary != null)
                    {
                        decompressor.LoadDictionary(_dictionary);
                    }

                    Span<byte> lengthHeaderBytes = stackalloc byte[4];
                    decompressor.Read(lengthHeaderBytes);
                    int uncompressedLength = BinaryHelpers.ByteSpanToInt32LittleEndian(ref lengthHeaderBytes);
                    decompressor.CopyToPooled(outStream);
                }

                return outStream.ToPooledBuffer();
            }
        }
    }
}

// This is the dictionary creation loop if you need to make a new one
//IDialogTransportProtocol dialogProtocol = new DialogJsonTransportProtocol();
//IDialogTransportProtocol dialogProtocol = new DialogBondTransportProtocol();
//IRandom rand = new FastRandom();
//List<byte[]> dictData = new List<byte[]>();
//for (int c = 0; c < 10000; c++)
//{
//    DialogRequest inputRequest = new DialogRequest()
//    {
//        AuthTokens = new List<SecurityToken>()
//                    {
//                        new SecurityToken()
//                        {
//                            Blue = string.Empty,
//                            Red = string.Empty,
//                            Scope  = ClientAuthenticationScope.UserClient
//                        }
//                    },
//        ClientContext = new ClientContext()
//        {
//            Capabilities =
//                ClientCapabilities.DisplayBasicHtml |
//                ClientCapabilities.DisplayHtml5 |
//                ClientCapabilities.DisplayBasicText |
//                ClientCapabilities.DisplayUnlimitedText |
//                ClientCapabilities.CanSynthesizeSpeech |
//                ClientCapabilities.SupportsCompressedAudio |
//                ClientCapabilities.SupportsStreamingAudio |
//                ClientCapabilities.HasInternetConnection,
//            ClientId = string.Empty,
//            ClientName = string.Empty,
//            ExtraClientContext = new Dictionary<string, string>()
//            {
//            },
//            Latitude = rand.NextDouble() * 50,
//            Longitude = (rand.NextDouble() * 320) - 160,
//            Locale = LanguageCode.Parse("en-us"),
//            UserId = string.Empty,
//            UserTimeZone = string.Empty,
//            UTCOffset = rand.NextInt(-8, 8) * 30,
//        },
//        EntityContext = new ArraySegment<byte>(new byte[0]),
//        EntityInput = new List<EntityReference>()
//        {
//            //new EntityReference()
//            //{
//            //    EntityId = "entityId",
//            //    Relevance = 0.95f
//            //}
//        },
//        InteractionType = InputMethod.Spoken,
//        PreferredAudioCodec = "opus",
//        PreferredAudioFormat = CommonCodecParamHelper.CreateCodecParams(AudioSampleFormat.Stereo(48000)),
//        TextInput = string.Empty,
//        TraceId = string.Empty,
//    };

//    PooledBuffer<byte> serializedData = dialogProtocol.WriteClientRequest(inputRequest, NullLogger.Singleton);
//    byte[] rawRequest = new byte[serializedData.Length];
//    ArrayExtensions.MemCopy(serializedData.Buffer, 0, rawRequest, 0, rawRequest.Length);
//    dictData.Add(rawRequest);

//    //using (PooledBufferMemoryStream dataIn = new PooledBufferMemoryStream(serializedData))
//    //using (FileStream fileOut = new FileStream(@"C:\Code\Durandal\Data\protocol\json\request_" + c + ".json", FileMode.Create, FileAccess.Write))
//    //{
//    //    dataIn.CopyToPooled(fileOut);
//    //}

//    DialogResponse inputResponse = new DialogResponse()
//    {
//        AugmentedFinalQuery = string.Empty,
//        ContinueImmediately = rand.NextDouble() < 0.5,
//        ConversationLifetimeSeconds = rand.NextInt(10, 30000),
//        CustomAudioOrdering = AudioOrdering.BeforeSpeech,
//        ExecutedPlugin = new PluginStrongName("Plugin", rand.NextInt(0, 10), rand.NextInt(0, 10)),
//        ExecutionResult = Result.Success,
//        IsRetrying = rand.NextDouble() < 0.1,
//        ResponseAudio = new AudioData()
//        {
//            Codec = "opus",
//            CodecParams = "framesize=20",
//            Data = new ArraySegment<byte>(new byte[0])
//        },
//        ResponseData = new Dictionary<string, string>()
//        {
//        },
//        ResponseAction = string.Empty,
//        ResponseHtml = string.Empty,
//        ResponseSsml = string.Empty,
//        ResponseText = string.Empty,
//        ResponseUrl = "/cache?page=" + Guid.NewGuid().ToString("N") + "&trace=" + Guid.NewGuid().ToString("N"),
//        SelectedRecoResult = DialogTestHelpers.GetSimpleRecoResult("common", "side_speech", 0.94f, string.Empty),
//        StreamingAudioUrl = "/cache?audio=" + Guid.NewGuid().ToString("N") + "&trace=" + Guid.NewGuid().ToString("N"),
//        SuggestedQueries = new List<string>()
//                    {
//                        string.Empty, string.Empty,
//                    },
//        TraceId = string.Empty,
//        TriggerKeywords = new List<TriggerKeyword>()
//                    {
//                        new TriggerKeyword()
//                        {
//                            TriggerPhrase = string.Empty,
//                            AllowBargeIn = true,
//                            ExpireTimeSeconds = rand.NextInt(0, 10) * 30
//                        }
//                    },
//        UrlScope = UrlScope.Local,
//    };

//    serializedData = dialogProtocol.WriteClientResponse(inputResponse, NullLogger.Singleton);
//    byte[] rawResponse = new byte[serializedData.Length];
//    ArrayExtensions.MemCopy(serializedData.Buffer, 0, rawResponse, 0, rawResponse.Length);
//    dictData.Add(rawResponse);

//    //using (PooledBufferMemoryStream dataIn = new PooledBufferMemoryStream(serializedData))
//    //using (FileStream fileOut = new FileStream(@"C:\Code\Durandal\Data\protocol\json\response_" + c + ".json", FileMode.Create, FileAccess.Write))
//    //{
//    //    dataIn.CopyToPooled(fileOut);
//    //}
//}

//// Now build the dictionary
//byte[] dictionary = ZstdSharp.DictBuilder.TrainFromBuffer(dictData, 4096);
//File.WriteAllBytes(@"C:\Code\Durandal\Data\protocol\durandalbondv1.dict", dictionary);