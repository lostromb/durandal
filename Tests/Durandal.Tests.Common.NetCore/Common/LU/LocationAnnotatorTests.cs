using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.Config;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Annotation;
using Durandal.Common.Test;
using Durandal.Common.Ontology;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.Dialog;
using Durandal.Tests.Common.LU;
using Durandal.Tests.EntitySchemas;
using Durandal.Common.NLP.Language;

namespace Durandal.Tests.Common.LU
{
    [TestClass]
    [DoNotParallelize]
    public class LocationAnnotatorTests
    {
        private static ILogger _logger;
        private static FakeLocationServer _fakeLocationServer;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _logger = new ConsoleLogger("Main", LogLevel.All);
            _fakeLocationServer = new FakeLocationServer();
        }

        [TestMethod]
        public async Task TestLocationAnnotator()
        {
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            IHttpClientFactory httpClientFactory = new DirectHttpClientFactory(_fakeLocationServer);
            LocationEntityAnnotator annotator = new LocationEntityAnnotator("DummyKey", httpClientFactory, _logger);
            Assert.IsTrue(annotator.Initialize());

            KnowledgeContext entityContext = new KnowledgeContext();

            RecoResult rr = new RecoResult()
            {
                Confidence = 0.95f,
                Domain = "places",
                Intent = "find",
                Source = "LU",
                Utterance = new Sentence("where is london"),
                TagHyps = new List<TaggedData>()
                {
                    new TaggedData()
                    {
                        Utterance = "where is london",
                        Annotations = new Dictionary<string, string>(),
                        Confidence = 0.95f,
                        Slots = new List<SlotValue>()
                        {
                            new SlotValue("place", "london", SlotValueFormat.SpokenText)
                        }
                    }
                }
            };

            LURequest luRequest = new LURequest()
            {
                Context = new ClientContext()
                {
                    Locale = LanguageCode.EN_US,
                    ClientId = "test",
                    UserId = "test",
                    Capabilities = ClientCapabilities.DisplayUnlimitedText,
                    ClientName = "unit test"
                }
            };

            IConfiguration modelConfig = new InMemoryConfiguration(_logger);
            modelConfig.Set("SlotAnnotator_LocationEntity", new string[] { "find/place" });

            _fakeLocationServer.NextResponse = LondonResponse;

            object state = await annotator.AnnotateStateless(rr, luRequest, modelConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            await annotator.CommitAnnotation(state, rr, luRequest, entityContext, modelConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);

            // Verify that a place entity came back
            SlotValue annotatedSlot = DialogHelpers.TryGetSlot(rr, "place");
            IList<ContextualEntity> placeEntities = annotatedSlot.GetEntities(entityContext);
            Assert.AreNotEqual(0, placeEntities.Count);
            Assert.IsTrue(placeEntities[0].Entity.IsA<Place>());
            Place resolvedPlace = placeEntities[0].Entity.As<Place>();
            Assert.AreEqual("London, London, United Kingdom", resolvedPlace.Name.Value);
            PostalAddress address = resolvedPlace.Address_as_PostalAddress.ValueInMemory;
            Assert.IsNotNull(address);
            Assert.AreEqual("England", address.AddressRegion.Value);
            Assert.AreEqual("United Kingdom", address.AddressCountry_as_string.Value);
        }

        private static HttpResponse LondonResponse
        {
            get
            {
                HttpResponse returnVal = HttpResponse.OKResponse();
                returnVal.SetContent("{\"authenticationResultCode\":\"ValidCredentials\",\"brandLogoUri\":\"http:\\/\\/dev.virtualearth.net\\/Branding\\/logo_powered_by.png\",\"copyright\":\"Copyright © 2018 Microsoft and its suppliers. All rights reserved. This API cannot be accessed and the content and any results may not be used, reproduced or transmitted in any manner without express written permission from Microsoft Corporation.\",\"resourceSets\":[{\"estimatedTotal\":5,\"resources\":[{\"__type\":\"Location:http:\\/\\/schemas.microsoft.com\\/search\\/local\\/ws\\/rest\\/v1\",\"bbox\":[51.1714782714844,-1.0028589963913,51.8642425537109,0.7984259724617],\"name\":\"London, London, United Kingdom\",\"point\":{\"type\":\"Point\",\"coordinates\":[51.506420135498,-0.127210006117821]},\"address\":{\"adminDistrict\":\"England\",\"adminDistrict2\":\"London\",\"countryRegion\":\"United Kingdom\",\"formattedAddress\":\"London, London, United Kingdom\",\"locality\":\"London\"},\"confidence\":\"High\",\"entityType\":\"PopulatedPlace\",\"geocodePoints\":[{\"type\":\"Point\",\"coordinates\":[51.506420135498,-0.127210006117821],\"calculationMethod\":\"Rooftop\",\"usageTypes\":[\"Display\"]}],\"matchCodes\":[\"Ambiguous\"]},{\"__type\":\"Location:http:\\/\\/schemas.microsoft.com\\/search\\/local\\/ws\\/rest\\/v1\",\"bbox\":[42.8245124816895,-81.3906707763672,43.0730667114258,-81.1070709228516],\"name\":\"London, ON\",\"point\":{\"type\":\"Point\",\"coordinates\":[42.9869003295898,-81.2462387084961]},\"address\":{\"adminDistrict\":\"ON\",\"adminDistrict2\":\"Middlesex\",\"countryRegion\":\"Canada\",\"formattedAddress\":\"London, ON\",\"locality\":\"London\"},\"confidence\":\"Low\",\"entityType\":\"PopulatedPlace\",\"geocodePoints\":[{\"type\":\"Point\",\"coordinates\":[42.9869003295898,-81.2462387084961],\"calculationMethod\":\"Rooftop\",\"usageTypes\":[\"Display\"]}],\"matchCodes\":[\"Ambiguous\"]},{\"__type\":\"Location:http:\\/\\/schemas.microsoft.com\\/search\\/local\\/ws\\/rest\\/v1\",\"bbox\":[51.2868309020996,-0.5103600025177,51.6923294067383,0.334039986133575],\"name\":\"London, United Kingdom\",\"point\":{\"type\":\"Point\",\"coordinates\":[51.5442810058594,-0.107905998826027]},\"address\":{\"adminDistrict\":\"England\",\"adminDistrict2\":\"London\",\"countryRegion\":\"United Kingdom\",\"formattedAddress\":\"London, United Kingdom\"},\"confidence\":\"Low\",\"entityType\":\"AdminDivision2\",\"geocodePoints\":[{\"type\":\"Point\",\"coordinates\":[51.5442810058594,-0.107905998826027],\"calculationMethod\":\"Rooftop\",\"usageTypes\":[\"Display\"]}],\"matchCodes\":[\"Ambiguous\"]},{\"__type\":\"Location:http:\\/\\/schemas.microsoft.com\\/search\\/local\\/ws\\/rest\\/v1\",\"bbox\":[51.7145614624023,-0.308620005846024,51.7312889099121,-0.279410004615784],\"name\":\"London Colney, Hertfordshire, United Kingdom\",\"point\":{\"type\":\"Point\",\"coordinates\":[51.7227821350098,-0.2950100004673]},\"address\":{\"adminDistrict\":\"England\",\"adminDistrict2\":\"Hertfordshire\",\"countryRegion\":\"United Kingdom\",\"formattedAddress\":\"London Colney, Hertfordshire, United Kingdom\",\"locality\":\"London Colney\"},\"confidence\":\"Low\",\"entityType\":\"PopulatedPlace\",\"geocodePoints\":[{\"type\":\"Point\",\"coordinates\":[51.7227821350098,-0.2950100004673],\"calculationMethod\":\"Rooftop\",\"usageTypes\":[\"Display\"]}],\"matchCodes\":[\"Ambiguous\"]},{\"__type\":\"Location:http:\\/\\/schemas.microsoft.com\\/search\\/local\\/ws\\/rest\\/v1\",\"bbox\":[52.0826416015625,-2.56325006484985,52.1473503112793,-2.54541993141174],\"name\":\"London, United Kingdom\",\"point\":{\"type\":\"Point\",\"coordinates\":[52.1394309997559,-2.55640006065369]},\"address\":{\"adminDistrict\":\"England\",\"adminDistrict2\":\"Herefordshire\",\"countryRegion\":\"United Kingdom\",\"formattedAddress\":\"London, United Kingdom\"},\"confidence\":\"Low\",\"entityType\":\"River\",\"geocodePoints\":[{\"type\":\"Point\",\"coordinates\":[52.1394309997559,-2.55640006065369],\"calculationMethod\":\"Rooftop\",\"usageTypes\":[\"Display\"]}],\"matchCodes\":[\"Ambiguous\"]}]}],\"statusCode\":200,\"statusDescription\":\"OK\",\"traceId\":\"c07aeea7e11d4169b42ce402325fb955|CO356342F5|7.7.0.0|Ref A: C8EAF0A6438348D88E1FF0B773C8D413 Ref B: CO1EDGE0515 Ref C: 2018-09-12T16:24:05Z\"}", "application/json");
                return returnVal;
            }
        }

        public class FakeLocationServer : IHttpServerDelegate
        {
            public HttpResponse NextResponse;

            public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                HttpResponse resp = NextResponse;
                if (resp != null)
                {
                    try
                    {
                        await serverContext.WritePrimaryResponse(resp, _logger, cancelToken, realTime).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                    }
                }
            }
        }
    }
}
