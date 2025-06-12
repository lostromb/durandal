namespace Durandal.Plugins.Flights
{
    using System;
    using Durandal.API;
        using Durandal.Common.Utils;
    using Durandal.CommonViews;
    using System.Collections.Generic;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Logger;
    using Durandal.Common.IO;
    using System.Net;
    using Newtonsoft.Json;
    using System.IO;
    using System.Threading.Tasks;
    using Durandal.Common.NLP;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Net;
    using Durandal.Common.File;
    using Durandal.Plugins.Flights.Schema;
    using Durandal.Common.Time;
    using Durandal.Common.MathExt;
    using Durandal.Common.Statistics;

    public class FlightsPlugin : DurandalPlugin
    {
        private readonly IHttpClientFactory _overrideHttpClientFactory = null;
        private readonly IRealTimeProvider _realTime = DefaultRealTimeProvider.Singleton;
        private IList<NamedEntity<string>> _carrierNameMapping;
        private IDictionary<string, string> _allCarrierNames;
        private FlightStatsAPI _flightsApi;

        public FlightsPlugin(IHttpClientFactory overrideHttpClientFactory, IRealTimeProvider timeProvider) : this()
        {
            _overrideHttpClientFactory = overrideHttpClientFactory;
            _realTime = timeProvider;
        }

        public FlightsPlugin()
            : base("flights")
        {
        }

        public override async Task OnLoad(IPluginServices services)
        {
            _carrierNameMapping = new List<NamedEntity<string>>();
            _allCarrierNames = new Dictionary<string, string>();
            if (_overrideHttpClientFactory != null)
            {
                _flightsApi = new FlightStatsAPI("MOCK_TEST_APPID", "MOCK_TEST_KEY", _overrideHttpClientFactory, services.Logger);
            }
            else
            {
                string appId = services.PluginConfiguration.GetString("appId");
                string apiKey = services.PluginConfiguration.GetString("apiKey");
                if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(apiKey))
                {
                    services.Logger.Log("Flightstatus apiKey or appId is missing! This plugin will not work.", LogLevel.Err);
                    _flightsApi = null;
                }
                else
                {
                    _flightsApi = new FlightStatsAPI(appId, apiKey, services.HttpClientFactory, services.Logger);
                }
            }

            VirtualPath carrierSemanticMappingFile = services.PluginDataDirectory + "\\canonical_carriers.txt";
            if (await services.FileSystem.ExistsAsync(carrierSemanticMappingFile).ConfigureAwait(false))
            {
                IDictionary<string, List<LexicalString>> tempDict = new Dictionary<string, List<LexicalString>>();
                IEnumerable<string> lines = await services.FileSystem.ReadLinesAsync(carrierSemanticMappingFile).ConfigureAwait(false);
                foreach (string l in lines)
                {
                    string[] parts = l.Split('\t');
                    if (parts.Length != 2)
                        continue;

                    if (!tempDict.ContainsKey(parts[1]))
                    {
                        tempDict[parts[1]] = new List<LexicalString>();
                    }
                    tempDict[parts[1]].Add(new LexicalString(parts[0]));
                }

                foreach (var item in tempDict)
                {
                    _carrierNameMapping.Add(new NamedEntity<string>(item.Key, item.Value));
                }
            }

            VirtualPath allCarrierNamesFile = services.PluginDataDirectory + "\\all_carriers.txt";
            if (await services.FileSystem.ExistsAsync(allCarrierNamesFile).ConfigureAwait(false))
            {
                IDictionary<string, List<string>> tempDict = new Dictionary<string, List<string>>();
                IEnumerable<string> lines = await services.FileSystem.ReadLinesAsync(allCarrierNamesFile).ConfigureAwait(false);
                foreach (string l in lines)
                {
                    string[] parts = l.Split('\t');
                    if (parts.Length != 2)
                        continue;

                    _allCarrierNames.Add(parts[0], parts[1]);
                }
            }
        }

        public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (_flightsApi == null)
            {
                services.Logger.Log("No flights api key in configuration", LogLevel.Err);
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Flights plugin is misconfigured"
                };
            }

            LexicalString airline = DialogHelpers.TryGetLexicalSlotValue(queryWithContext.Understanding, "airline");
            string flightNum = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "flight_num");

            if (airline == null ||
                string.IsNullOrEmpty(airline.WrittenForm))
            {
                services.Logger.Log("Unknown airline. todo: prompt user to clarify", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }
            if (string.IsNullOrEmpty(flightNum))
            {
                services.Logger.Log("No flight number", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }

            // Canonicalize the flight carrier
            IList<Hypothesis<string>> carrierHyps = await services.EntityResolver.ResolveEntity(airline, _carrierNameMapping, queryWithContext.ClientContext.Locale, services.Logger).ConfigureAwait(false);

            if (carrierHyps.Count == 0)
            {
                services.Logger.Log("Carrier ID for \"" + airline + "\" did not resolve to anything", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }

            if (carrierHyps[0].Conf < 0.7)
            {
                services.Logger.Log("Carrier ID for \"" + airline + "\" is too ambiguous (" + carrierHyps[0].Conf + "), skipping", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }

            string carrierId = carrierHyps[0].Value;

            FlightStatusAPIResponse apiResponse = await _flightsApi.GetFlightStatus(carrierId, flightNum, _realTime.Time, services.Logger).ConfigureAwait(false);
            if (apiResponse == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ResponseText = "Sorry, I could not connect to flight status service."
                };
            }

            FlightStatus status = GetRelevantFlightStatus(queryWithContext, services, apiResponse, _realTime.Time);

            if (status == null)
            {
                services.Logger.Log("No API response for " + carrierId + " flight " + flightNum, LogLevel.Err);
                return new PluginResult(Result.Success)
                {
                    ResponseText = "I could not find the flight you wanted.",
                    ResponseHtml = new MessageView()
                    {
                        Content = "I could not find the flight you wanted",
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                };
            }

            string carrierName;
            if (!_allCarrierNames.TryGetValue(carrierId, out carrierName))
            {
                carrierName = airline.WrittenForm;
            }

            DateTimeOffset arrivalDateLocalTime = status.ArrivalDate.DateLocal;
            Airport arrivalAirport = ResolveAirportCode(apiResponse, status.ArrivalAirportFsCode);
            string arrivalAirportCity = arrivalAirport.City;

            ILGPattern responseLg = services.LanguageGenerator.GetPattern("FlightArrivalOnTime", queryWithContext.ClientContext, services.Logger)
                .Sub("carrier", carrierName)
                .Sub("flight_num", flightNum)
                .Sub("airport", arrivalAirportCity)
                .Sub("arrival_time", arrivalDateLocalTime);

            return await responseLg.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = new MessageView()
                {
                    Content = (await responseLg.Render().ConfigureAwait(false)).Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            }).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Finds the flight status that the user is probably asking about
        /// </summary>
        /// <param name="queryWithContext"></param>
        /// <param name="services"></param>
        /// <param name="apiData"></param>
        /// <returns></returns>
        private static FlightStatus GetRelevantFlightStatus(QueryWithContext queryWithContext, IPluginServices services, FlightStatusAPIResponse apiData, DateTimeOffset currentUtcTime)
        {
            if (apiData == null || apiData.Appendix == null || apiData.FlightStatuses == null || apiData.Appendix.Airports == null | apiData.FlightStatuses.Count == 0)
            {
                return null;
            }

            if (queryWithContext.ClientContext.Latitude.HasValue &&
                queryWithContext.ClientContext.Longitude.HasValue)
            {
                GeoCoordinate userCoord = new GeoCoordinate(queryWithContext.ClientContext.Latitude.Value, queryWithContext.ClientContext.Longitude.Value);

                // See if there's an airport on the route that's within 100 km of the user's location
                Airport userNearestAirport = null;
                double closestDistanceKm = 100;
                
                foreach (Airport p in apiData.Appendix.Airports)
                {
                    GeoCoordinate airportCoord = new GeoCoordinate(p.Latitude, p.Longitude);
                    double dist = GeoMath.CalculateGeoDistance(userCoord, airportCoord);
                    if (dist < closestDistanceKm)
                    {
                        closestDistanceKm = dist;
                        userNearestAirport = p;
                    }
                }

                if (userNearestAirport != null)
                {
                    // The user is near an airport on the route. See if the expected arrival to that airport is in the future
                    FlightStatus status = GetArrivalToAirport(apiData, userNearestAirport);
                    if (status != null && status.ArrivalDate.DateUtc > currentUtcTime)
                    {
                        return status;
                    }

                    // If not, we just fall through
                }
            }

            // By default, return the time of this flight's next landing
            return GetNextLandingTime(apiData, currentUtcTime);
        }

        /// <summary>
        /// Returns the flight status for the leg of flight that lands at a specific airport
        /// </summary>
        /// <param name="apiData"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        private static FlightStatus GetArrivalToAirport(FlightStatusAPIResponse apiData, Airport destination)
        {
            foreach (FlightStatus s in apiData.FlightStatuses)
            {
                if (s.ArrivalAirportFsCode.Equals(destination.FsCode) && (s.Status.Equals("A") || s.Status.Equals("S")))
                {
                    return s;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the flight status for the leg of flight that lands next
        /// </summary>
        /// <param name="apiData"></param>
        /// <returns></returns>
        private static FlightStatus GetNextLandingTime(FlightStatusAPIResponse apiData, DateTimeOffset currentTime)
        {
            double minutesUntilShortestLanding = 999999;
            FlightStatus nextLandingSegment = null;
            foreach (FlightStatus s in apiData.FlightStatuses)
            {
                double minutesUntilLanding = (currentTime - s.ArrivalDate.DateUtc).TotalMinutes;
                if (s.ArrivalDate.DateUtc > currentTime &&
                    minutesUntilLanding < minutesUntilShortestLanding)
                {
                    minutesUntilShortestLanding = minutesUntilLanding;
                    nextLandingSegment = s;
                }
            }

            return nextLandingSegment;
        }

        private static Airport ResolveAirportCode(FlightStatusAPIResponse apiData, string fsCode)
        {
            foreach (var airport in apiData.Appendix.Airports)
            {
                if (airport.FsCode.Equals(fsCode))
                {
                    return airport;
                }
            }

            return null;
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            using (MemoryStream pngStream = new MemoryStream())
            {
                if (pluginDataDirectory != null && pluginDataManager != null)
                {
                    VirtualPath iconFile = pluginDataDirectory + "\\icon.png";
                    if (pluginDataManager.Exists(iconFile))
                    {
                        using (Stream iconStream = pluginDataManager.OpenStream(iconFile, FileOpenMode.Open, FileAccessMode.Read))
                        {
                            iconStream.CopyTo(pngStream);
                        }
                    }
                }

                PluginInformation returnVal = new PluginInformation()
                {
                    InternalName = "FlightsInfo",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Flight Status",
                    ShortDescription = "Looks up flight information",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Status of Southwest flight 755");

                return returnVal;
            }
        }
    }
}
