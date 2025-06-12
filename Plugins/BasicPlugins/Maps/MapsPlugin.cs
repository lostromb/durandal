using Durandal.Common.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Utils;
using Durandal.CommonViews;
using System.IO;
using Durandal.Internal.CoreOntology.SchemaDotOrg;
using Durandal.Common.Ontology;
using Durandal.Common.IO;
using Durandal.Plugins.Bing;
using Durandal.Common.File;
using Durandal.ExternalServices.Bing.Maps;
using Durandal.ExternalServices.Bing.Search;
using Durandal.Common.Tasks;
using Durandal.ExternalServices.Bing.Search.Schemas;
using SchemaDotOrg = Durandal.Plugins.Basic.SchemaDotOrg;
using Durandal.Common.Statistics;
using System.Threading;
using Durandal.Common.Time;

namespace Durandal.Plugins.Maps
{
    public class MapsPlugin : DurandalPlugin
    {
        private BingMaps _bingMapsApi;
        private BingSearch _bingSearchApi;

        public MapsPlugin() : base("maps")
        {
        }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode getDirectionsNode = tree.CreateNode(GetDirections);
            IConversationNode getMapNode = tree.CreateNode(GetMap);
            tree.AddStartState("get_directions", getDirectionsNode);
            tree.AddStartState("get_map", getMapNode);
            return tree;
        }

        public override async Task<TriggerResult> Trigger(QueryWithContext queryWithContext, IPluginServices services)
        {
            bool hasPlaceEntities = services.EntityHistory.FindEntities<SchemaDotOrg.Place>().Count > 0;
            bool hasAnaphora = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "anaphora") != null;
            bool hasDestinationSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "destination") != null;

            if (hasDestinationSlot)
            {
                return new TriggerResult(BoostingOption.NoChange);
            }

            if (hasPlaceEntities && hasAnaphora)
            {
                return new TriggerResult(BoostingOption.NoChange);
            }

            if (!hasDestinationSlot && !hasAnaphora)
            {
                return new TriggerResult(BoostingOption.NoChange);
            }

            return await Task.FromResult(new TriggerResult(BoostingOption.Suppress));
        }

        public override async Task OnLoad(IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;

            string mapsApiKey = null;
            if (services.PluginConfiguration.ContainsKey("MapsApiKey"))
            {
                mapsApiKey = services.PluginConfiguration.GetString("MapsApiKey");
            }

            _bingMapsApi = new BingMaps(mapsApiKey, services.HttpClientFactory, services.Logger);

            string searchAppId = null;
            if (services.PluginConfiguration.ContainsKey("SearchAppId"))
            {
                searchAppId = services.PluginConfiguration.GetString("SearchAppId");
            }

            _bingSearchApi = new BingSearch(searchAppId, services.HttpClientFactory, services.Logger, BingApiVersion.V7Internal);
        }

        public async Task<PluginResult> GetMap(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Look for a location name slot
            Entity rootEntity = null;

            string locationName = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "destination");
            if (!string.IsNullOrEmpty(locationName))
            {
                // Resolve location
                BingResponse bingResponse = await _bingSearchApi.Query(
                    locationName,
                    services.Logger,
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton,
                    queryWithContext.ClientContext.Locale,
                    new Type[] { typeof(SchemaDotOrg.Place) });
                if (bingResponse.EntityReferences.Count > 0)
                {
                    rootEntity = bingResponse.KnowledgeContext.GetEntityInMemory(bingResponse.EntityReferences[0]);
                }
            }

            // If person is still null, try and pull from context
            if (rootEntity == null)
            {
                IList<Hypothesis<SchemaDotOrg.Place>> entities = services.EntityHistory.FindEntities<SchemaDotOrg.Place>();
                if (entities.Count > 0)
                {
                    rootEntity = entities[0].Value;
                }
            }

            // If location is still null, try and get user's current location
            if (rootEntity == null)
            {
                if (queryWithContext.ClientContext.Longitude.HasValue &&
                    queryWithContext.ClientContext.Latitude.HasValue)
                {
                    SchemaDotOrg.Place userLocation = new SchemaDotOrg.Place(services.EntityContext);
                    userLocation.Name.Value = "your current location";
                    SchemaDotOrg.GeoCircle userGeoLocation = new SchemaDotOrg.GeoCircle(services.EntityContext);
                    SchemaDotOrg.GeoCoordinates midPoint = new SchemaDotOrg.GeoCoordinates(services.EntityContext);
                    midPoint.Longitude_as_number.Value = (decimal)queryWithContext.ClientContext.Longitude.Value;
                    midPoint.Latitude_as_number.Value = (decimal)queryWithContext.ClientContext.Latitude.Value;
                    userGeoLocation.GeoMidpoint.SetValue(midPoint);
                    if (queryWithContext.ClientContext.LocationAccuracy.HasValue)
                    {
                        userGeoLocation.GeoRadius_as_number.Value = (decimal)queryWithContext.ClientContext.LocationAccuracy.Value;
                    }
                    else
                    {
                        userGeoLocation.GeoRadius_as_number.Value = 2000M;
                    }
                    userLocation.Geo_as_GeoShape.SetValue(userGeoLocation.As<SchemaDotOrg.GeoShape>());
                    rootEntity = userLocation;
                }
                else
                {
                    // Location name from slot exists but no location was resolved.
                    // can happen when the entity resolver fails to find this location
                    // Can also happen when user asks to "find me on the map" and
                    // there is no client location context
                    return new PluginResult(Result.Skip);
                }
            }

            if (rootEntity == null || !(rootEntity.IsA<SchemaDotOrg.Place>()))
            {
                // No location exists. This should have been a failure in the preconditions
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Expected a location entity input but none was found"
                };
            }

            SchemaDotOrg.Place destination = rootEntity.As<SchemaDotOrg.Place>();

            //// If no geo information, try hydrating (?)
            //if (destination.Geo_as_GeoCoordinates.GetValueInMemory() == null &&
            //    destination.Geo_as_GeoShape.GetValueInMemory() == null)
            //{
            //    destination = await BingMapsAPI.Hydrate(destination);
            //}

            // Get the directions image
            BingMapsPlace bingPlace = BingMapsPlace.FromSchemaDotOrg(destination);
            ArraySegment<byte> routeImage = await _bingMapsApi.GetMapImage(
                bingPlace,
                services.Logger,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton,
                queryWithContext.ClientContext.Locale,
                zoom: 16, mapWidth: 1000, mapHeight: 600);
            if (routeImage == null)
            {
                string responseText = "Sorry, I can't find a map for " + destination.Name.Value;
                return new PluginResult(Result.Success)
                {
                    ResponseText = responseText,
                    ResponseSsml = responseText
                };
            }

            services.EntityHistory.AddOrUpdateEntity(destination);

            // The payload is a PNG. Cache it and return an html page
            string imageUrl = services.CreateTemporaryWebResource(routeImage, "image/png");

            string response = "Here is " + GetStringForPlace(destination);

            MapView html = new MapView()
            {
                Content = response,
                Image = imageUrl,
                ClientContextData = queryWithContext.ClientContext.ExtraClientContext
            };

            return new PluginResult(Result.Success)
            {
                ResponseText = response,
                ResponseSsml = response,
                ResponseHtml = html.Render()
            };
        }

        public async Task<PluginResult> GetDirections(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Look for a location name slot
            Entity rootEntity = null;

            string locationName = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "destination");
            if (!string.IsNullOrEmpty(locationName))
            {
                BingResponse bingResponse = await _bingSearchApi.Query(
                    locationName,
                    services.Logger,
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton,
                    queryWithContext.ClientContext.Locale,
                    new Type[] { typeof(SchemaDotOrg.Place) });
                if (bingResponse.EntityReferences.Count > 0)
                {
                    rootEntity = bingResponse.KnowledgeContext.GetEntityInMemory(bingResponse.EntityReferences[0]);
                }
            }

            // If person is still null, try and pull from context
            if (rootEntity == null)
            {
                IList<Hypothesis<SchemaDotOrg.Place>> entities = services.EntityHistory.FindEntities<SchemaDotOrg.Place>();
                if (entities.Count > 0)
                {
                    rootEntity = entities[0].Value;
                }
            }

            // If location is still null, fail out
            if (rootEntity == null && !string.IsNullOrEmpty(locationName))
            {
                // Location name from slot exists but no location was resolved.
                // can happen when the entity resolver fails to find this location
                return new PluginResult(Result.Skip);
            }

            if (rootEntity == null || !(rootEntity.IsA<SchemaDotOrg.Place>()))
            {
                // No location exists. This should have been a failure in the preconditions
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Expected a location entity input but none was found"
                };
            }

            SchemaDotOrg.Place destination = rootEntity.As<SchemaDotOrg.Place>();

            services.EntityHistory.AddOrUpdateEntity(rootEntity);

            // Do we have an origin slot? (TODO)
            // Use the user's current location
            SchemaDotOrg.Place origin = new SchemaDotOrg.Place(services.EntityContext);
            if (queryWithContext.ClientContext.Latitude.HasValue && queryWithContext.ClientContext.Latitude.Value != 0 &&
                queryWithContext.ClientContext.Longitude.HasValue && queryWithContext.ClientContext.Longitude.Value != 0)
            {
                SchemaDotOrg.GeoCoordinates userCoords = new SchemaDotOrg.GeoCoordinates(services.EntityContext);
                userCoords.Latitude_as_number.Value = (decimal)queryWithContext.ClientContext.Latitude.Value;
                userCoords.Longitude_as_number.Value = (decimal)queryWithContext.ClientContext.Longitude.Value;
                userCoords.Name.Value = "Your current location";
                origin.Name.Value = "Your current location";
                origin.Geo_as_GeoCoordinates.SetValue(userCoords);
            }
            else
            {
                string responseText = "I can't find directions because I don't know where you are";
                return new PluginResult(Result.Success)
                {
                    ResponseText = responseText,
                    ResponseSsml = responseText
                };
            }

            // Get the directions image
            BingMapsPlace bingOrigin = BingMapsPlace.FromSchemaDotOrg(origin);
            BingMapsPlace bingDestination = BingMapsPlace.FromSchemaDotOrg(destination);
            ArraySegment<byte> routeImage = await _bingMapsApi.GetRouteImage(
                bingOrigin,
                bingDestination,
                services.Logger,
                queryWithContext.ClientContext.Locale, mapWidth: 1000, mapHeight: 600);
            if (routeImage == null)
            {
                string responseText = "Something went wrong while pulling up the map";
                return new PluginResult(Result.Failure)
                {
                    ResponseText = responseText,
                    ResponseSsml = responseText
                };
            }

            // The payload is a PNG. Cache it and return an html page
            string imageUrl = services.CreateTemporaryWebResource(routeImage, "image/png");

            string response = "Here are directions to " + GetStringForPlace(destination);

            MapView html = new MapView()
            {
                Content = response,
                Image = imageUrl,
                ClientContextData = queryWithContext.ClientContext.ExtraClientContext
            };

            return new PluginResult(Result.Success)
            {
                ResponseText = response,
                ResponseSsml = response,
                ResponseHtml = html.Render()
            };
        }

        private static string GetStringForPlace(SchemaDotOrg.Place place)
        {
            if (!string.IsNullOrEmpty(place.Name.Value))
            {
                return place.Name.Value;
            }

            SchemaDotOrg.PostalAddress address = place.Address_as_PostalAddress.ValueInMemory;
            if (address != null)
            {
                return address.Name.Value ?? address.StreetAddress.Value ?? address.AddressLocality.Value ?? address.AddressRegion.Value ?? address.AddressCountry_as_string.Value ?? "Somewhere";
            }

            return "Somewhere";
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            MemoryStream pngStream = new MemoryStream();
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
                InternalName = "Maps",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
                IconPngData = new ArraySegment<byte>(pngStream.ToArray())
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Maps",
                ShortDescription = "Shows maps and directions",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("show me how to get there");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("get directions to seattle");

            return returnVal;
        }
    }
}
