
namespace Durandal.Plugins.Weather
{
    using Common.Client.Actions;
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Ontology;
    using Durandal.Common.Statistics;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.UnitConversion;
    using Durandal.Common.Utils;
    using Durandal.CommonViews;
    using Durandal.ExternalServices.Bing.Maps;
    using Durandal.ExternalServices.Darksky;
    using Durandal.Internal.CoreOntology.SchemaDotOrg;
    using Durandal.Plugins.Weather.Views;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    public class WeatherPlugin : DurandalPlugin
    {
        private WeatherBackgroundGenerator _backgroundGenerator;
        private DarkskyApi _darksky;
        private BingMaps _maps;

        public WeatherPlugin() : base("weather")
        {
        }

        public override async Task OnLoad(IPluginServices services)
        {
            _backgroundGenerator = await WeatherBackgroundGenerator.Build(services.FileSystem, services.PluginViewDirectory.Combine("bg")).ConfigureAwait(false);
            string darkskyApiKey = services.PluginConfiguration.GetString("DarkskyApiKey");
            if (string.IsNullOrEmpty(darkskyApiKey))
            {
                services.Logger.Log("No Darksky API key provided in configuration! Weather plugin will be broken", LogLevel.Err);
                _darksky = null;
            }
            else
            {
                _darksky = new DarkskyApi(services.HttpClientFactory, services.Logger, darkskyApiKey);
            }

            string bingMapsApiKey = services.PluginConfiguration.GetString("BingMapsApiKey");
            if (string.IsNullOrEmpty(bingMapsApiKey))
            {
                services.Logger.Log("No Bing Maps API key provided in configuration! Weather plugin will be broken", LogLevel.Err);
                _maps = null;
            }
            else
            {
                _maps = new BingMaps(bingMapsApiKey, services.HttpClientFactory, services.Logger);
            }
        }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode getWeatherNode = returnVal.CreateNode(GetWeather);
            returnVal.AddStartState("get_weather", getWeatherNode);
            getWeatherNode.CreateNormalEdge("get_weather_multiturn", getWeatherNode);

            returnVal.AddStartState("sunset_query", WhenIsSunset);
            returnVal.AddStartState("sunrise_query", WhenIsSunrise);

            returnVal.AddStartState("refresh", RefreshWeatherDisplay);

            return returnVal;
        }

        public async Task<PluginResult> GetWeather(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Check if the user referenced a remote location
            SchemaDotOrg.Place remoteLocation = TryResolveLocation(queryWithContext, services);

            if (remoteLocation != null)
            {
                // REMOTE LOCATION PATH ("Weather in Seattle")
                services.Logger.Log("Using remote location path, remote location is " + remoteLocation.Name.Value);
                SchemaDotOrg.GeoCoordinates geoCoords = remoteLocation.Geo_as_GeoCoordinates.ValueInMemory;
                if (geoCoords == null || !geoCoords.Latitude_as_number.Value.HasValue)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Geo coordinates were null"
                    };
                }

                string remoteLocationName = "Unknown location";
                if (!string.IsNullOrEmpty(remoteLocation.Name.Value))
                {
                    // TODO if we wanted to be really smart, we should determine how far away the remote location is from current location, and only show what parts are relevant
                    // (For example, only show country name if the query is made for a different country from the user's own)
                    remoteLocationName = remoteLocation.Name.Value;
                }
                
                GeoCoordinate coords = new GeoCoordinate((double)geoCoords.Latitude_as_number.Value.Value, (double)geoCoords.Longitude_as_number.Value.Value);
                DarkskyWeatherResult weatherData = await _darksky.GetWeatherData(
                    coords,
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton,
                    services.Logger,
                    DarkskyRequestFeatures.CurrentWeather | DarkskyRequestFeatures.HourlyWeather | DarkskyRequestFeatures.DailyWeather | DarkskyRequestFeatures.Flags,
                    queryWithContext.ClientContext.Locale.Iso639_1,
                    DarkskyMeasurementUnit.SI).ConfigureAwait(false);

                if (weatherData == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "No weather results were found"
                    };
                }

                ConversionHelpers.ConvertAllTemperatures(weatherData, UnitName.CELSIUS, UnitName.FAHRENHEIT, services.Logger);

                // Generate a refresh action URL
                string clientAction;
                DialogAction refreshAction = GenerateRefreshAction(queryWithContext, coords, remoteLocationName, services, TimeSpan.FromMinutes(30), out clientAction);
                ConditionsView responseHTML = await GenerateCurrentConditionsHTML(remoteLocationName, weatherData, queryWithContext, services, refreshAction).ConfigureAwait(false);
                
                return await services.LanguageGenerator.GetPattern("CurrentRemoteConditions", queryWithContext.ClientContext, services.Logger)
                    .Sub("temp", weatherData.Currently.ApparentTemperature.GetValueOrDefault(-99))
                    .Sub("condition", weatherData.Hourly.Summary)
                    .Sub("location", remoteLocation.Name.Value)
                    .Sub("unit", "F")
                    .ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        ResponseHtml = responseHTML.Render(),
                        MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                        ClientAction = clientAction,
                        ResponsePrivacyClassification = DataPrivacyClassification.PublicNonPersonalData
                    }).ConfigureAwait(false);
            }
            else
            {
                // LOCAL LOCATION PATH ("Weather outside")
                if (!queryWithContext.ClientContext.Latitude.HasValue ||
                    !queryWithContext.ClientContext.Longitude.HasValue)
                {
                    services.Logger.Log("Expected client to send its GPS location in Latitude/Longitude fields", LogLevel.Wrn);
                    ILGPattern pattern = services.LanguageGenerator.GetPattern("NoLocationInfo", queryWithContext.ClientContext, services.Logger);
                    return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        ResponseHtml = new MessageView()
                        {
                            Content = (await pattern.Render().ConfigureAwait(false)).Text,
                            ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                        }.Render(),
                        ResponsePrivacyClassification = DataPrivacyClassification.SystemMetadata
                    }).ConfigureAwait(false);
                }

                // Get the client context coordinates
                GeoCoordinate coords = new GeoCoordinate(queryWithContext.ClientContext.Latitude.Value, queryWithContext.ClientContext.Longitude.Value);
                services.Logger.Log(LogLevel.Std, DataPrivacyClassification.EndUserIdentifiableInformation,
                    "Using local location path, coords are {0}:{1}", coords.Latitude, coords.Longitude);

                Task<IList<Hypothesis<BingMapsPlace>>> locationFetchTask = _maps.ReverseGeocode(
                    coords,
                    services.Logger,
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton,
                    queryWithContext.ClientContext.Locale);

                Task<DarkskyWeatherResult> weatherFetchTask = _darksky.GetWeatherData(
                    coords,
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton,
                    services.Logger,
                    DarkskyRequestFeatures.CurrentWeather | DarkskyRequestFeatures.HourlyWeather | DarkskyRequestFeatures.DailyWeather | DarkskyRequestFeatures.Flags,
                    queryWithContext.ClientContext.Locale.Iso639_1,
                    DarkskyMeasurementUnit.SI);

                DarkskyWeatherResult weatherData = await weatherFetchTask.ConfigureAwait(false);

                if (weatherData == null)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "No weather results were found"
                    };
                }

                ConversionHelpers.ConvertAllTemperatures(weatherData, UnitName.CELSIUS, UnitName.FAHRENHEIT, services.Logger);

                IList<Hypothesis<BingMapsPlace>> locationResolutionResult = await locationFetchTask.ConfigureAwait(false);
                string currentLocationName = "Unknown location";
                if (locationResolutionResult != null && locationResolutionResult.Count > 0)
                {
                    currentLocationName = locationResolutionResult[0].Value.Locality + ", " + locationResolutionResult[0].Value.AdminDistrict;
                }

                // Generate a refresh action URL
                string clientAction;
                DialogAction refreshAction = GenerateRefreshAction(queryWithContext, coords, currentLocationName, services, TimeSpan.FromMinutes(30), out clientAction);

                ConditionsView responseHTML = await this.GenerateCurrentConditionsHTML(currentLocationName, weatherData, queryWithContext, services, refreshAction).ConfigureAwait(false);
                
                return await services.LanguageGenerator.GetPattern("CurrentLocalConditions", queryWithContext.ClientContext, services.Logger)
                    .Sub("temp", weatherData.Currently.ApparentTemperature.GetValueOrDefault(-99))
                    .Sub("condition", weatherData.Hourly.Summary)
                    .Sub("unit", "F")
                    .ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        ResponseHtml = responseHTML.Render(),
                        MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                        ClientAction = clientAction,
                        ResponsePrivacyClassification = DataPrivacyClassification.PublicNonPersonalData
                    }).ConfigureAwait(false);
            }
        }

        public async Task<PluginResult> RefreshWeatherDisplay(QueryWithContext queryWithContext, IPluginServices services)
        {
            string cachedCoordinateString = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "cached_coordinate");
            if (string.IsNullOrEmpty(cachedCoordinateString))
            {
                services.Logger.Log("RefreshWeatherDisplay called, but no cached_coordinate slot is present!", LogLevel.Wrn);

                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Weather refresh called with no cached coordinate"
                };
            }

            services.Logger.Log("Getting weather for coordinate" + cachedCoordinateString);
            string[] coordStringParts = cachedCoordinateString.Split(new char[] { ',' }, 3);
            if (coordStringParts.Length < 3)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Invalid cached coordinate string"
                };
            }

            double lat = double.Parse(coordStringParts[0]);
            double lng = double.Parse(coordStringParts[1]);
            GeoCoordinate cachedCoordinate = new GeoCoordinate(lat, lng);
            string locationName = coordStringParts[2];

            DarkskyWeatherResult weatherData = await _darksky.GetWeatherData(
                    cachedCoordinate,
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton,
                    services.Logger,
                    DarkskyRequestFeatures.CurrentWeather | DarkskyRequestFeatures.HourlyWeather | DarkskyRequestFeatures.DailyWeather | DarkskyRequestFeatures.Flags,
                    queryWithContext.ClientContext.Locale.Iso639_1,
                    DarkskyMeasurementUnit.SI).ConfigureAwait(false);

            if (weatherData == null)
            {
                // If we fail to refresh the weather, then show a temporary error associated with a refresh action
                string errorFallbackAction;
                GenerateRefreshAction(queryWithContext, cachedCoordinate, locationName, services, TimeSpan.FromSeconds(30), out errorFallbackAction);
                if (string.IsNullOrEmpty(errorFallbackAction))
                {
                    // Client does not support delayed actions, so not much we can do here
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "An error happened while getting weather information."
                    };
                }
                else
                {
                    return new PluginResult(Result.Success)
                    {
                        ResponseText = "Something went wrong. Please wait a moment...",
                        MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                        ClientAction = errorFallbackAction,
                        ResponsePrivacyClassification = DataPrivacyClassification.SystemMetadata
                    };
                }
            }

            services.Logger.Log("Successfully got weather refresh data");
            ConversionHelpers.ConvertAllTemperatures(weatherData, UnitName.CELSIUS, UnitName.FAHRENHEIT, services.Logger);

            // Generate a refresh action URL
            string clientAction;
            DialogAction refreshAction = GenerateRefreshAction(queryWithContext, cachedCoordinate, locationName, services, TimeSpan.FromMinutes(30), out clientAction);
            ConditionsView responseHTML = await this.GenerateCurrentConditionsHTML(locationName, weatherData, queryWithContext, services, refreshAction).ConfigureAwait(false);
            
            return await services.LanguageGenerator.GetPattern("CurrentLocalConditions", queryWithContext.ClientContext, services.Logger)
                .Sub("temp", weatherData.Currently.ApparentTemperature.GetValueOrDefault(-99))
                .Sub("condition", weatherData.Hourly.Summary)
                .ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = responseHTML.Render(),
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                    ClientAction = clientAction,
                    ResponsePrivacyClassification = DataPrivacyClassification.PublicNonPersonalData
                }).ConfigureAwait(false);
        }

        private static DialogAction GenerateRefreshAction(QueryWithContext queryWithContext, GeoCoordinate coord, string locationName, IPluginServices services, TimeSpan timeToWait, out string clientAction)
        {
            DialogAction refreshAction = new DialogAction()
            {
                Domain = "weather",
                Intent = "refresh",
                Slots = new List<SlotValue>(),
                InteractionMethod = InputMethod.Programmatic
            };
            refreshAction.Slots.Add(new SlotValue("cached_coordinate", string.Format("{0},{1},{2}", coord.Latitude, coord.Longitude, locationName), SlotValueFormat.DialogActionParameter));

            clientAction = string.Empty;

            // If the client supports the DelayedAction schema, send the serialized action
            if (queryWithContext.ClientContext.SupportedClientActions != null &&
                queryWithContext.ClientContext.SupportedClientActions.Contains(ExecuteDelayedAction.ActionName))
            {
                string actionId = services.RegisterDialogAction(refreshAction);

                ExecuteDelayedAction delayedRefreshAction = new ExecuteDelayedAction()
                {
                    ActionId = actionId,
                    DelaySeconds = (int)timeToWait.TotalSeconds,
                    InteractionMethod = Enum.GetName(typeof(InputMethod), InputMethod.Programmatic)
                };

                refreshAction = null;
                clientAction = JsonConvert.SerializeObject(delayedRefreshAction);
            }

            return refreshAction;
        }

        private static SchemaDotOrg.Place TryResolveLocation(QueryWithContext queryWithContext, IPluginServices services)
        {
            SlotValue queryLocation = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "location");
            if (queryLocation == null)
            {
                // No location, skip (most likely to fall back on local results)
                return null;
            }

            IList<ContextualEntity> allEntities = queryLocation.GetEntities(services.EntityContext);

            // Did the location annotator return any results?
            if (allEntities.Count == 0)
            {
                //services.Logger.Log("Location resolver did not return any results; falling back to raw location search for \"" + queryLocation.Value + "\"");
                //data = new WeatherData(queryLocation.Value, services.Logger);
                return null;
            }

            SchemaDotOrg.Place resolvedLocation = allEntities[0].Entity.As<SchemaDotOrg.Place>();

            services.Logger.Log("Resolved location is \"" + resolvedLocation.Name.Value + "\"");

            return resolvedLocation;
        }

        private static string ConvertBearingDegreesToHeading(double bearingDegrees)
        {
            if (bearingDegrees >= 22.5 && bearingDegrees < 67.5)
            {
                return "NE";
            }
            else if (bearingDegrees >= 67.5 && bearingDegrees < 112.5)
            {
                return "E";
            }
            else if (bearingDegrees >= 112.5 && bearingDegrees < 157.5)
            {
                return "SE";
            }
            else if (bearingDegrees >= 157.5 && bearingDegrees < 202.5)
            {
                return "S";
            }
            else if (bearingDegrees >= 202.5 && bearingDegrees < 247.5)
            {
                return "SW";
            }
            else if (bearingDegrees >= 247.5 && bearingDegrees < 292.5)
            {
                return "W";
            }
            else if (bearingDegrees >= 292.5 && bearingDegrees < 337.5)
            {
                return "NW";
            }
            else
            {
                return "N";
            }
        }

        private async Task<ConditionsView> GenerateCurrentConditionsHTML(string locationName, DarkskyWeatherResult data, QueryWithContext query, IPluginServices services, DialogAction refreshAction)
        {
            // Localize wind bearing (go from degrees into "NE", "NW", etc.)
            double windBearingDegrees = data.Currently.WindBearing.GetValueOrDefault(0);
            string windBearing = ConvertBearingDegreesToHeading(windBearingDegrees);
            string windBearingLocalized = (await services.LanguageGenerator.GetPattern("Bearing", query.ClientContext, services.Logger).Sub("bearing", windBearing).Render().ConfigureAwait(false)).Text;

            // Convert atmospheric pressure to desired locale
            double? pressureInHg = ConversionHelpers.DoSingleConversion(data.Currently.Pressure, UnitName.MILLIBAR, UnitName.INCHES_MERCURY, services.Logger);

            // Convert wind speed to desired locale
            double? windSpeedMph = ConversionHelpers.DoSingleConversion(data.Currently.WindSpeed, UnitName.KILOMETER_PER_HOUR, UnitName.MILE_PER_HOUR, services.Logger);
            
            double relativeHumidity = data.Currently.Humidity.GetValueOrDefault(0);
            
            ConditionsView page = new ConditionsView();
            page.FullWeatherResult = data;
            page.Location = locationName;
            page.DetailedConditions = data.Currently.Summary;
            page.Temperature = data.Currently.ApparentTemperature.GetValueOrDefault(-99).ToString("F1") + "°";
            page.ConditionImageName = "partly-cloudy-day.png";
            
            WeatherTimeOfDay parsedTimeOfDay = WeatherTimeOfDayExtensions.Parse(data.Currently.Time);
            WeatherCondition parsedCondition = WeatherConditionExtensions.Parse(data.Currently.Icon);

            string backgroundImageUrl = _backgroundGenerator.GetBackgroundImage(parsedTimeOfDay, parsedCondition);
            page.BackgroundImageName = WebUtility.UrlEncode(backgroundImageUrl);
            if (data.Currently.WindSpeed.HasValue)
            {
                page.WindCondition = windSpeedMph.GetValueOrDefault(0).ToString("F1") + " mph " + windBearingLocalized;
            }
            else
            {
                page.WindCondition = "0 mph";
            }

            if (data.Currently.PrecipAccumulation.HasValue) // FIXME this refers to snow accumulation, not rain. Use PrecipIntensity instead
            {
                page.RainCondition = data.Currently.PrecipAccumulation.Value + " in.";
            }
            else
            {
                page.RainCondition = "0 in.";
            }

            page.HumidityCondition = ((int)(relativeHumidity * 100)).ToString() + "%";
            page.PressureCondition = pressureInHg.GetValueOrDefault(0).ToString("F2") + " in.";

            if (refreshAction != null)
            {
                page.RefreshUrl = services.RegisterDialogActionUrl(refreshAction, query.ClientContext.ClientId);
            }
            else
            {
                page.RefreshUrl = null;
            }

            return page;
        }

        public async Task<PluginResult> WhenIsSunset(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!queryWithContext.ClientContext.Latitude.HasValue ||
                !queryWithContext.ClientContext.Longitude.HasValue)
            {
                return await services.LanguageGenerator.GetPattern("NoLocationInfo", queryWithContext.ClientContext, services.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Skip)
                    {
                        ErrorMessage = "Expected client to send its GPS location in Latitude/Longitude fields"
                    }).ConfigureAwait(false);
            }

            // Get the client context
            double clientLatitude = queryWithContext.ClientContext.Latitude.Value;
            double clientLongitude = queryWithContext.ClientContext.Longitude.Value;
            GeoCoordinate clientCoord = new GeoCoordinate(clientLatitude, clientLongitude);

            DarkskyWeatherResult weatherData = await _darksky.GetWeatherData(
                    clientCoord,
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton,
                    services.Logger,
                    DarkskyRequestFeatures.DailyWeather | DarkskyRequestFeatures.Flags,
                    queryWithContext.ClientContext.Locale.Iso639_1,
                    DarkskyMeasurementUnit.SI).ConfigureAwait(false);

            if (weatherData == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "No weather results were found"
                };
            }

            if (weatherData.Daily == null ||
                weatherData.Daily.Data.Count == 0 ||
                !weatherData.Daily.Data[0].SunsetTime.HasValue)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Weather result did not contain sunset data"
                };
            }

            ILGPattern pattern = services.LanguageGenerator.GetPattern("SunsetToday", queryWithContext.ClientContext, services.Logger)
                .Sub("time", weatherData.Daily.Data[0].SunsetTime.Value.ToString("t"));

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render().ConfigureAwait(false)).Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            }).ConfigureAwait(false);
        }

        public async Task<PluginResult> WhenIsSunrise(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!queryWithContext.ClientContext.Latitude.HasValue ||
                !queryWithContext.ClientContext.Longitude.HasValue)
            {
                return await services.LanguageGenerator.GetPattern("NoLocationInfo", queryWithContext.ClientContext, services.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Skip)
                    {
                        ErrorMessage = "Expected client to send its GPS location in Latitude/Longitude fields"
                    }).ConfigureAwait(false);
            }

            // Get the client context
            double clientLatitude = queryWithContext.ClientContext.Latitude.Value;
            double clientLongitude = queryWithContext.ClientContext.Longitude.Value;
            GeoCoordinate clientCoord = new GeoCoordinate(clientLatitude, clientLongitude);

            DarkskyWeatherResult weatherData = await _darksky.GetWeatherData(
                    clientCoord,
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton,
                    services.Logger,
                    DarkskyRequestFeatures.DailyWeather | DarkskyRequestFeatures.Flags,
                    queryWithContext.ClientContext.Locale.Iso639_1,
                    DarkskyMeasurementUnit.SI).ConfigureAwait(false);

            if (weatherData == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "No weather results were found"
                };
            }

            if (weatherData.Daily == null ||
                weatherData.Daily.Data.Count == 0 ||
                !weatherData.Daily.Data[0].SunriseTime.HasValue)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Weather result did not contain sunrise data"
                };
            }

            ILGPattern pattern = services.LanguageGenerator.GetPattern("SunriseToday", queryWithContext.ClientContext, services.Logger)
                .Sub("time", weatherData.Daily.Data[0].SunriseTime.Value.ToString("t"));

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render().ConfigureAwait(false)).Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            }).ConfigureAwait(false);
        }

        public override async Task<CrossDomainRequestData> CrossDomainRequest(string targetIntent)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            if (targetIntent.Equals("current_remote_conditions"))
            {
                CrossDomainRequestData returnVal = new CrossDomainRequestData();
                CrossDomainSlot locationSlot = new CrossDomainSlot("location", true);
                locationSlot.AcceptedSchemas.Add("http://schema.org/Place");
                locationSlot.AcceptedSchemas.Add("http://schema.org/City");
                locationSlot.AcceptedSchemas.Add("http://schema.org/AdministrativeArea");
                //locationSlot.AcceptedSchemas.Add("http://schema.org/Country");
                //locationSlot.AcceptedSchemas.Add("http://schema.org/State");
                locationSlot.AcceptedSchemas.Add("http://freebase.com/location/mailing_address/citytown");
                locationSlot.AcceptedSchemas.Add("mso:location.address.city_entity");
                //locationSlot.AcceptedSchemas.Add("mso:location.address.country_entity");
                returnVal.RequestedSlots.Add(locationSlot);
                return returnVal;
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
                    InternalName = "Weather",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Weather",
                    ShortDescription = "A cloudy cloud calculator",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("How cold is it outside?");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("What's the weather like?");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Is it raining in San Francisco?");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("What time is sunset tonight?");

                return returnVal;
            }
        }
    }
}
