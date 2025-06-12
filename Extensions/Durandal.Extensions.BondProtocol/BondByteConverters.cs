using Bond;
using Bond.IO.Safe;
using Bond.Protocols;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Text;
using BONDAPI = Durandal.Extensions.BondProtocol.API;
using CAPI = Durandal.API;
using System.IO;
using Durandal.Common.IO;
using Durandal.Common.Logger;

namespace Durandal.Extensions.BondProtocol
{
    public class BondByteConverterConversationStateStack : IByteConverter<CAPI.SerializedConversationStateStack>
    {
        private readonly ILogger _logger;
        public BondByteConverterConversationStateStack(ILogger logger = null)
        {
            _logger = logger ?? NullLogger.Singleton;
            BondConverter.PrecacheSerializers<BONDAPI.SerializedConversationStateStack>();
        }

        public byte[] Encode(CAPI.SerializedConversationStateStack input)
        {
            BONDAPI.SerializedConversationStateStack convertedInput = BondTypeConverters.Convert(input);
            return BondConverter.SerializeBond(convertedInput, _logger);
        }

        public CAPI.SerializedConversationStateStack Decode(byte[] input, int offset, int length)
        {
            BONDAPI.SerializedConversationStateStack returnVal;
            BondConverter.DeserializeBond(input, offset, length, out returnVal, _logger);
            return BondTypeConverters.Convert(returnVal);
        }

        public int Encode(CAPI.SerializedConversationStateStack input, Stream target)
        {
            throw new NotImplementedException();
        }

        public CAPI.SerializedConversationStateStack Decode(Stream input, int length)
        {
            throw new NotImplementedException();
        }
    }

    public class BondByteConverterDialogAction : IByteConverter<CAPI.DialogAction>
    {
        private readonly ILogger _logger;

        public BondByteConverterDialogAction(ILogger logger = null)
        {
            _logger = logger ?? NullLogger.Singleton;
            BondConverter.PrecacheSerializers<BONDAPI.DialogAction>();
        }

        public byte[] Encode(CAPI.DialogAction input)
        {
            BONDAPI.DialogAction convertedInput = BondTypeConverters.Convert(input);
            return BondConverter.SerializeBond(convertedInput, _logger);
        }

        public CAPI.DialogAction Decode(byte[] input, int offset, int length)
        {
            BONDAPI.DialogAction returnVal;
            BondConverter.DeserializeBond(input, offset, length, out returnVal, _logger);
            return BondTypeConverters.Convert(returnVal);
        }

        public int Encode(CAPI.DialogAction input, Stream target)
        {
            throw new NotImplementedException();
        }

        public CAPI.DialogAction Decode(Stream input, int length)
        {
            throw new NotImplementedException();
        }
    }

    public class BondByteConverterInstrumentationEventList : IByteConverter<CAPI.InstrumentationEventList>
    {
        private readonly ILogger _logger;

        public BondByteConverterInstrumentationEventList(ILogger logger = null)
        {
            _logger = logger ?? NullLogger.Singleton;
            BondConverter.PrecacheSerializers<BONDAPI.InstrumentationEventList>();
        }

        public byte[] Encode(CAPI.InstrumentationEventList input)
        {
            BONDAPI.InstrumentationEventList convertedInput = BondTypeConverters.Convert(input);
            return BondConverter.SerializeBond(convertedInput, _logger);
        }

        public CAPI.InstrumentationEventList Decode(byte[] input, int offset, int length)
        {
            BONDAPI.InstrumentationEventList returnVal;
            BondConverter.DeserializeBond(input, offset, length, out returnVal, _logger);
            return BondTypeConverters.Convert(returnVal);
        }

        public int Encode(CAPI.InstrumentationEventList input, Stream target)
        {
            throw new NotImplementedException();
        }

        public CAPI.InstrumentationEventList Decode(Stream input, int length)
        {
            throw new NotImplementedException();
        }
    }

    public class BondByteConverterCachedWebData : IByteConverter<CAPI.CachedWebData>
    {
        private readonly ILogger _logger;

        public BondByteConverterCachedWebData(ILogger logger = null)
        {
            _logger = logger ?? NullLogger.Singleton;
            BondConverter.PrecacheSerializers<BONDAPI.CachedWebData>();
        }

        public byte[] Encode(CAPI.CachedWebData input)
        {
            BONDAPI.CachedWebData convertedInput = BondTypeConverters.Convert(input);
            return BondConverter.SerializeBond(convertedInput, _logger);
        }

        public CAPI.CachedWebData Decode(byte[] input, int offset, int length)
        {
            BONDAPI.CachedWebData returnVal;
            BondConverter.DeserializeBond(input, offset, length, out returnVal, _logger);
            return BondTypeConverters.Convert(returnVal);
        }

        public int Encode(CAPI.CachedWebData input, Stream target)
        {
            throw new NotImplementedException();
        }

        public CAPI.CachedWebData Decode(Stream input, int length)
        {
            throw new NotImplementedException();
        }
    }

    public class BondByteConverterClientContext : IByteConverter<CAPI.ClientContext>
    {
        private readonly ILogger _logger;

        public BondByteConverterClientContext(ILogger logger = null)
        {
            _logger = logger ?? NullLogger.Singleton;
            BondConverter.PrecacheSerializers<BONDAPI.ClientContext>();
        }

        public byte[] Encode(CAPI.ClientContext input)
        {
            BONDAPI.ClientContext convertedInput = BondTypeConverters.Convert(input);
            return BondConverter.SerializeBond(convertedInput, _logger);
        }

        public CAPI.ClientContext Decode(byte[] input, int offset, int length)
        {
            BONDAPI.ClientContext returnVal;
            BondConverter.DeserializeBond(input, offset, length, out returnVal, _logger);
            return BondTypeConverters.Convert(returnVal);
        }

        public int Encode(CAPI.ClientContext input, Stream target)
        {
            throw new NotImplementedException();
        }

        public CAPI.ClientContext Decode(Stream input, int length)
        {
            throw new NotImplementedException();
        }
    }
}
