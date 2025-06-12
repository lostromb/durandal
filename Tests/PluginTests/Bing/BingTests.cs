using Durandal;
using Durandal.Plugins.Fitbit;
using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.Logger;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Net.Http;
using System.IO;
using Durandal.Common.Time;
using Durandal.Plugins.Fitbit.Schemas;
using Durandal.Plugins.Fitbit.Schemas.Responses;
using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.UnitConversion;
using Durandal.Common.Test;
using Durandal.Plugins.Bing;
using Durandal.Common.File;
using Durandal.Common.Test.Builders;
using System.Threading;

namespace DialogTests.Plugins.Joke
{
    [TestClass]
    public class BingTests
    {
        private static BingAnswer _plugin;
        private static InqueTestDriver _testDriver;
        private static FakeBingService _bingService;

        #region Test framework

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _bingService = new FakeBingService();
            _plugin = new BingAnswer(new DirectHttpClientFactory(_bingService));
            string rootEnv = context.Properties["DurandalRootDirectory"]?.ToString();
            if (string.IsNullOrEmpty(rootEnv))
            {
                rootEnv = Environment.GetEnvironmentVariable("DURANDAL_ROOT");
                if (string.IsNullOrEmpty(rootEnv))
                {
                    throw new FileNotFoundException("Cannot find durandal environment directory, either from DurandalRootDirectory test property, or DURANDAL_ROOT environment variable.");
                }
            }

            InqueTestParameters testConfig = PluginTestCommon.CreateTestParameters(_plugin, "BasicPlugins.dupkg", new DirectoryInfo(rootEnv));
            _testDriver = new InqueTestDriver(testConfig);
            _testDriver.Initialize().Await();
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            _testDriver.Dispose();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _testDriver.ResetState();
            _bingService.HttpResponseCode = 404;
            _bingService.RawSearchResponse = null;
            _bingService.ExpectedQuery = null;
        }

        #endregion

        #region Tests
        
        [TestMethod]
        public async Task TestBingUnitConvertBasic()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "how many cups are in a gallon", InputMethod.Typed)
                    .AddRecoResult("bing", "convert", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddCanonicalizedSlot("target_unit", "US_CUP", "cups")
                            .AddCanonicalizedSlot("source_unit", "US_GALLON", "gallon")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.AreEqual("convert", response.SelectedRecoResult.Intent);
            Assert.AreEqual("1 gallon equals 16 cups.", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestBingCurrencyConvert()
        {
            _bingService.HttpResponseCode = 200;
            _bingService.RawSearchResponse =
                "{" +
                "	\"currency\": {" +
                "		\"contractualRules\": [{" +
                "			\"_type\": \"ContractualRules\\/TextAttribution\"," +
                "			\"text\": \"Data from Morningstar\"" +
                "		}]," +
                "		\"attributions\": [{" +
                "			\"providerDisplayName\": \"Morningstar\"," +
                "			\"copyrightMessage\": \"Data from Morningstar\"" +
                "		}]," +
                "		\"value\": {" +
                "			\"fromCurrency\": \"USD\"," +
                "			\"fromValue\": 10," +
                "			\"fromCurrencyName\": \"US Dollar\"," +
                "			\"toCurrency\": \"EUR\"," +
                "			\"toValue\": 8.8," +
                "			\"toCurrencyName\": \"Euro\"," +
                "			\"forwardConversionRate\": 0.879948," +
                "			\"backwardConversionRate\": 1.136431" +
                "		}," +
                "		\"historicData\": []," +
                "		\"lastUpdated\": \"2019-01-22T19:27:30.0000000\"" +
                "	}," +
                "}";
            _bingService.ExpectedQuery = "Convert 10 USD to EUR";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "convert 10 dollars to euros", InputMethod.Typed)
                    .AddRecoResult("bing", "convert", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddNumericSlot("amount", "10", 10)
                            .AddCanonicalizedSlot("source_unit", "USD", "dollars")
                            .AddCanonicalizedSlot("target_unit", "EUR", "euros")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.AreEqual("convert", response.SelectedRecoResult.Intent);
            Assert.AreEqual("10 USD = 8.8 EUR", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestBingCurrencyConvertImpliedSourceCurrency()
        {
            _bingService.HttpResponseCode = 200;
            _bingService.RawSearchResponse =
                "{" +
                "	\"currency\": {" +
                "		\"contractualRules\": [{" +
                "			\"_type\": \"ContractualRules\\/TextAttribution\"," +
                "			\"text\": \"Data from Morningstar\"" +
                "		}]," +
                "		\"attributions\": [{" +
                "			\"providerDisplayName\": \"Morningstar\"," +
                "			\"copyrightMessage\": \"Data from Morningstar\"" +
                "		}]," +
                "		\"value\": {" +
                "			\"fromCurrency\": \"USD\"," +
                "			\"fromValue\": 10," +
                "			\"fromCurrencyName\": \"US Dollar\"," +
                "			\"toCurrency\": \"EUR\"," +
                "			\"toValue\": 8.8," +
                "			\"toCurrencyName\": \"Euro\"," +
                "			\"forwardConversionRate\": 0.879948," +
                "			\"backwardConversionRate\": 1.136431" +
                "		}," +
                "		\"historicData\": []," +
                "		\"lastUpdated\": \"2019-01-22T19:27:30.0000000\"" +
                "	}," +
                "}";
            _bingService.ExpectedQuery = "Convert 10 USD to EUR";

            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "convert 10 dollars to euros", InputMethod.Typed)
                    .AddRecoResult("bing", "convert", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddNumericSlot("amount", "10", 10)
                            .AddCanonicalizedSlot("target_unit", "EUR", "euros")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(Result.Success, response.ExecutionResult);
            Assert.AreEqual("convert", response.SelectedRecoResult.Intent);
            Assert.AreEqual("10 USD = 8.8 EUR", response.ResponseText);
            Assert.IsFalse(response.ContinueImmediately);
        }

        #endregion

        public class FakeBingService : IHttpServerDelegate
        {
            private ILogger _logger;

            public FakeBingService()
            {
                RawSearchResponse = string.Empty;
                HttpResponseCode = 200;
                ExpectedQuery = null;
                _logger = new ConsoleLogger("BingService");
            }

            public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                HttpResponse resp = await HandleConnectionInternal(serverContext.HttpRequest, cancelToken, realTime).ConfigureAwait(false);
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

            private Task<HttpResponse> HandleConnectionInternal(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                string query;
                if (!string.IsNullOrEmpty(ExpectedQuery) && request.GetParameters.TryGetValue("q", out query))
                {
                    Assert.AreEqual(ExpectedQuery, query);
                }

                HttpResponse response = HttpResponse.CreateOutgoing();
                response.ResponseCode = HttpResponseCode;
                if (!string.IsNullOrEmpty(RawSearchResponse))
                {
                    response.SetContent(RawSearchResponse, HttpConstants.MIME_TYPE_JSON);
                }

                return Task.FromResult(response);
            }

            public string ExpectedQuery { get; set; }
            public string RawSearchResponse { get; set; }
            public int HttpResponseCode { get; set; }
        }
    }
}
