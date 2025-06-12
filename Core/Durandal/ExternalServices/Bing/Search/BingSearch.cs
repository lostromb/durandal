using Durandal.Common.Utils;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.ExternalServices.Bing.Search.Schemas;
using Durandal.Common.Ontology;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Durandal.Common.Time;
using System.Threading;

using SchemaDotOrg = Durandal.Internal.CoreOntology.SchemaDotOrg;
using Durandal.Common.NLP.Language;

namespace Durandal.ExternalServices.Bing.Search
{
    public class BingSearch
    {
        private static readonly Regex GUID_MATCHER = new Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        private static readonly Regex PARENTHESES_REMOVER = new Regex(@"\s\(.+?\)$");
        
        private readonly IHttpClient _apiClient;
        private readonly string _apiKey;
        private readonly BingApiVersion _apiVersion;

        public BingSearch(string apiKey, IHttpClientFactory httpClientFactory, ILogger logger, BingApiVersion apiVersion)
        {
            _apiKey = apiKey;
            _apiVersion = apiVersion;

            if (apiVersion == BingApiVersion.V5 ||
                apiVersion == BingApiVersion.V7)
            {
                _apiClient = httpClientFactory.CreateHttpClient("api.cognitive.microsoft.com", 443, true, logger);
            }
            else if (apiVersion == BingApiVersion.V7Internal)
            {
                _apiClient = httpClientFactory.CreateHttpClient("www.bingapis.com", 443, true, logger);
            }
            else
            {
                throw new ArgumentException("Unsupported bing API version " + apiVersion);
            }
        }

        public async Task<BingResponse> Query(
            string query,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            LanguageCode locale,
            IEnumerable<Type> typeConstraints = null)
        {
            // Detect if we are using internal API or public
            if (string.IsNullOrEmpty(_apiKey))
            {
                return null;
            }

            RawSearchResponse parsedResponse;
            if (_apiVersion == BingApiVersion.V7Internal)
            {
                // Bing internal API (if api key is an appid)
                using (HttpRequest request = HttpRequest.CreateOutgoing("/api/v7/search"))
                {
                    request.GetParameters["q"] = query;
                    request.GetParameters["appid"] = _apiKey;
                    request.GetParameters["responseformat"] = "json";
                    request.GetParameters["mkt"] = locale.ToBcp47Alpha3String();
                    request.GetParameters["setlang"] = locale.ToBcp47Alpha3String();

                    using (HttpResponse netResp = await _apiClient.SendRequestAsync(request, cancelToken, realTime, NullLogger.Singleton).ConfigureAwait(false))
                    {
                        if (netResp == null)
                        {
                            queryLogger.Log("Null response from Bing search!", LogLevel.Err);
                            return null;
                        }

                        parsedResponse = await netResp.ReadContentAsJsonObjectAsync<RawSearchResponse>(cancelToken, realTime).ConfigureAwait(false);
                        await netResp.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
            }
            else if (_apiVersion == BingApiVersion.V5)
            {
                using (HttpRequest request = HttpRequest.CreateOutgoing("/bing/v5.0/search"))
                {
                    request.GetParameters["q"] = query;
                    //request.GetParameters["count"] = "1";
                    //request.GetParameters["offset"] = "0";
                    request.GetParameters["mkt"] = locale.ToBcp47Alpha3String();
                    request.GetParameters["safesearch"] = "Strict";
                    request.RequestHeaders["Ocp-Apim-Subscription-Key"] = _apiKey;

                    using (HttpResponse netResp = await _apiClient.SendRequestAsync(request, cancelToken, realTime, NullLogger.Singleton).ConfigureAwait(false))
                    {
                        if (netResp == null)
                        {
                            queryLogger.Log("Null response from Bing search!", LogLevel.Err);
                            return null;
                        }

                        parsedResponse = await netResp.ReadContentAsJsonObjectAsync<RawSearchResponse>(cancelToken, realTime).ConfigureAwait(false);
                        await netResp.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                    }
                }
            }
            else if (_apiVersion == BingApiVersion.V7)
            {
                using (HttpRequest request = HttpRequest.CreateOutgoing("/bing/v7.0/search"))
                {
                    request.GetParameters["q"] = query;
                    //request.GetParameters["count"] = "1";
                    //request.GetParameters["offset"] = "0";
                    request.GetParameters["mkt"] = locale.ToBcp47Alpha3String();
                    request.GetParameters["safesearch"] = "Strict";
                    request.RequestHeaders["Ocp-Apim-Subscription-Key"] = _apiKey;

                    using (HttpResponse netResp = await _apiClient.SendRequestAsync(request, cancelToken, realTime, NullLogger.Singleton).ConfigureAwait(false))
                    {
                        if (netResp == null)
                        {
                            queryLogger.Log("Null response from Bing search!", LogLevel.Err);
                            return null;
                        }

                        parsedResponse = await netResp.ReadContentAsJsonObjectAsync<RawSearchResponse>(cancelToken, realTime).ConfigureAwait(false);
                        await netResp.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                throw new NotImplementedException("Invalid bing API version");
            }

            BingResponse returnVal = new BingResponse();

            // Extract facts
            if (parsedResponse.Facts != null && parsedResponse.Facts.value != null)
            {
                foreach (FactResponseEntry fact in parsedResponse.Facts.value)
                {
                    returnVal.Facts.Add(new BingFactResponse()
                        {
                            Text = fact.description,
                            Subtitle = fact.subjectName
                        });
                }
            }

            // Make a context
            returnVal.KnowledgeContext = new KnowledgeContext();

            // Extract computation
            if (parsedResponse.Computation != null)
            {
                returnVal.Computation = parsedResponse.Computation;
            }

            // Extract currency
            if (parsedResponse.Currency != null)
            {
                returnVal.Currency = parsedResponse.Currency;
            }

            // Extract all places
            if (parsedResponse.Places != null)
            {
                HashSet<string> idsAdded = new HashSet<string>();
                foreach (SearchEntity place in parsedResponse.Places.value)
                {
                    Entity converted = TryConvertSearchEntity(place, returnVal.KnowledgeContext, queryLogger);
                    if (converted != null)
                    {
                        // deduplicate bing local results
                        if (idsAdded.Contains(converted.EntityId))
                        {
                            continue;
                        }

                        idsAdded.Add(converted.EntityId);

                        returnVal.EntityReferences.Add(converted.EntityId);
                    }
                }
            }

            // Now extract all other entities
            if (parsedResponse.Entities != null)
            {
                foreach (SearchEntity entity in parsedResponse.Entities.value)
                {
                    Entity converted = TryConvertSearchEntity(entity, returnVal.KnowledgeContext, queryLogger);
                    if (converted != null)
                    {
                        if (typeConstraints == null)
                        {
                            returnVal.EntityReferences.Add(converted.EntityId);
                        }
                        else
                        {
                            // Apply type filter if needed
                            bool satisfiesTypeConstraints = true;
                            foreach (Type type in typeConstraints)
                            {
                                if (!converted.IsA(type))
                                {
                                    satisfiesTypeConstraints = false;
                                    break;
                                }
                            }

                            if (satisfiesTypeConstraints)
                            {
                                returnVal.EntityReferences.Add(converted.EntityId);
                            }
                        }
                    }
                }
            }

            return returnVal;
        }
        
        private static string ConvertBingEntityId(string entityId)
        {
            Match m = GUID_MATCHER.Match(entityId);
            if (m.Success)
            {
                return m.Value;
            }

            return entityId;
        }
        
        private static Entity TryConvertSearchEntity(SearchEntity entity, KnowledgeContext context, ILogger queryLogger)
        {
            if (entity.entityPresentationInfo == null ||
                entity.entityPresentationInfo.entityTypeHints == null ||
                string.IsNullOrEmpty(entity.name))
            {
                return null;
            }

            // Type info seems to go from least specific to most specific, so reverse the list
            entity.entityPresentationInfo.entityTypeHints.Reverse();

            string bingId = entity.bingId;
            if (string.IsNullOrEmpty(bingId))
            {
                bingId = ConvertBingEntityId(entity.id);
            }

            foreach (string typeIdHint in entity.entityPresentationInfo.entityTypeHints)
            {
                if (string.Equals(typeIdHint, "Person") || string.Equals(typeIdHint, "Actor") || string.Equals(typeIdHint, "Artist"))
                {
                    SchemaDotOrg.Person returnVal = new SchemaDotOrg.Person(context, "bing://" + bingId);
                    returnVal.Description.Value = entity.description;
                    returnVal.Name.Value = entity.name;
                    returnVal.Telephone.Value = entity.telephone;
                    returnVal.Url.Value = entity.webSearchUrl;
                    if (entity.image != null)
                    {
                        if (!string.IsNullOrEmpty(entity.image.contentUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.contentUrl;
                        }
                        else if (!string.IsNullOrEmpty(entity.image.thumbnailUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.thumbnailUrl;
                        }
                    }

                    //Console.WriteLine(JsonConvert.SerializeObject(entity, Formatting.Indented));
                    
                    return returnVal;
                }
                else if (string.Equals(typeIdHint, "City"))
                {
                    SchemaDotOrg.City returnVal = new SchemaDotOrg.City(context, "bing://" + bingId);
                    returnVal.Description.Value = entity.description;
                    returnVal.Name.Value = entity.name;
                    returnVal.Url.Value = entity.webSearchUrl;
                    if (entity.image != null)
                    {
                        if (!string.IsNullOrEmpty(entity.image.contentUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.contentUrl;
                        }
                        else if (!string.IsNullOrEmpty(entity.image.thumbnailUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.thumbnailUrl;
                        }
                    }

                    // Pull geo info
                    if (entity.geo != null)
                    {
                        SchemaDotOrg.GeoCoordinates coords = new SchemaDotOrg.GeoCoordinates(context);
                        coords.Latitude_as_number.Value = (decimal)entity.geo.latitude;
                        coords.Longitude_as_number.Value = (decimal)entity.geo.longitude;
                        returnVal.Geo_as_GeoCoordinates.SetValue(coords);
                    }

                    if (entity.address != null)
                    {
                        SchemaDotOrg.PostalAddress address = new SchemaDotOrg.PostalAddress(context);
                        address.AddressCountry_as_string.Value = entity.address.addressCountry;
                        address.AddressRegion.Value = entity.address.addressRegion;
                        returnVal.Address_as_PostalAddress.SetValue(address);
                    }
                    
                    return returnVal;
                }
                else if (string.Equals(typeIdHint, "Locality"))
                {
                    SchemaDotOrg.Place returnVal = new SchemaDotOrg.Place(context, "bing://" + bingId);
                    returnVal.Name.Value = StringUtils.RegexRemove(PARENTHESES_REMOVER, entity.name);
                    returnVal.Description.Value = entity.description;
                    returnVal.Url.Value = entity.webSearchUrl;
                    if (entity.image != null)
                    {
                        if (!string.IsNullOrEmpty(entity.image.contentUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.contentUrl;
                        }
                        else if (!string.IsNullOrEmpty(entity.image.thumbnailUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.thumbnailUrl;
                        }
                    }

                    // Pull geo info
                    if (entity.geo != null)
                    {
                        SchemaDotOrg.GeoCoordinates coords = new SchemaDotOrg.GeoCoordinates(context);
                        coords.Latitude_as_number.Value = (decimal)entity.geo.latitude;
                        coords.Longitude_as_number.Value = (decimal)entity.geo.longitude;
                        returnVal.Geo_as_GeoCoordinates.SetValue(coords);
                    }

                    if (entity.address != null)
                    {
                        SchemaDotOrg.PostalAddress address = new SchemaDotOrg.PostalAddress(context);
                        address.AddressCountry_as_string.Value = entity.address.addressCountry;
                        address.AddressRegion.Value = entity.address.addressRegion;
                        returnVal.Address_as_PostalAddress.SetValue(address);
                    }
                    
                    return returnVal;
                }
                else if (string.Equals(typeIdHint, "State"))
                {
                    SchemaDotOrg.Place returnVal = new SchemaDotOrg.Place(context, "bing://" + bingId);
                    returnVal.Name.Value = StringUtils.RegexRemove(PARENTHESES_REMOVER, entity.name);
                    returnVal.Description.Value = entity.description;
                    returnVal.Url.Value = entity.webSearchUrl;
                    if (entity.image != null)
                    {
                        if (!string.IsNullOrEmpty(entity.image.contentUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.contentUrl;
                        }
                        else if (!string.IsNullOrEmpty(entity.image.thumbnailUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.thumbnailUrl;
                        }
                    }

                    // Pull geo info
                    if (entity.geo != null)
                    {
                        SchemaDotOrg.GeoCoordinates coords = new SchemaDotOrg.GeoCoordinates(context);
                        coords.Latitude_as_number.Value = (decimal)entity.geo.latitude;
                        coords.Longitude_as_number.Value = (decimal)entity.geo.longitude;
                        returnVal.Geo_as_GeoCoordinates.SetValue(coords);
                    }

                    if (entity.address != null)
                    {
                        SchemaDotOrg.PostalAddress address = new SchemaDotOrg.PostalAddress(context);
                        address.AddressCountry_as_string.Value = entity.address.addressCountry;
                        returnVal.Address_as_PostalAddress.SetValue(address);
                    }

                    return returnVal;
                }
                else if (string.Equals(typeIdHint, "Country"))
                {
                    SchemaDotOrg.Place returnVal = new SchemaDotOrg.Place(context, "bing://" + bingId);
                    returnVal.Name.Value = StringUtils.RegexRemove(PARENTHESES_REMOVER, entity.name);
                    returnVal.Description.Value = entity.description;
                    returnVal.Url.Value = entity.webSearchUrl;
                    if (entity.image != null)
                    {
                        if (!string.IsNullOrEmpty(entity.image.contentUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.contentUrl;
                        }
                        else if (!string.IsNullOrEmpty(entity.image.thumbnailUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.thumbnailUrl;
                        }
                    }

                    // Pull geo info
                    if (entity.geo != null)
                    {
                        SchemaDotOrg.GeoCoordinates coords = new SchemaDotOrg.GeoCoordinates(context);
                        coords.Latitude_as_number.Value = (decimal)entity.geo.latitude;
                        coords.Longitude_as_number.Value = (decimal)entity.geo.longitude;
                        returnVal.Geo_as_GeoCoordinates.SetValue(coords);
                    }

                    return returnVal;
                }
                else if (string.Equals(typeIdHint, "Organization"))
                {
                    SchemaDotOrg.Organization returnVal = new SchemaDotOrg.Organization(context, "bing://" + bingId);
                    returnVal.Name.Value = StringUtils.RegexRemove(PARENTHESES_REMOVER, entity.name);
                    returnVal.Description.Value = entity.description;
                    if (entity.image != null)
                    {
                        if (!string.IsNullOrEmpty(entity.image.contentUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.contentUrl;
                        }
                        else if (!string.IsNullOrEmpty(entity.image.thumbnailUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.thumbnailUrl;
                        }
                    }
                    return returnVal;
                }
                else if (string.Equals(typeIdHint, "Movie"))
                {
                    SchemaDotOrg.Movie returnVal = new SchemaDotOrg.Movie(context, "bing://" + bingId);
                    returnVal.Name.Value = StringUtils.RegexRemove(PARENTHESES_REMOVER, entity.name);
                    returnVal.Description.Value = entity.description;
                    if (entity.image != null)
                    {
                        if (!string.IsNullOrEmpty(entity.image.contentUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.contentUrl;
                        }
                        else if (!string.IsNullOrEmpty(entity.image.thumbnailUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.thumbnailUrl;
                        }
                    }
                    return returnVal;
                }
                else if (string.Equals(typeIdHint, "TelevisionShow"))
                {
                    SchemaDotOrg.TVSeries returnVal = new SchemaDotOrg.TVSeries(context, "bing://" + bingId);
                    returnVal.Name.Value = StringUtils.RegexRemove(PARENTHESES_REMOVER, entity.name);
                    returnVal.Description.Value = entity.description;
                    if (entity.image != null)
                    {
                        if (!string.IsNullOrEmpty(entity.image.contentUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.contentUrl;
                        }
                        else if (!string.IsNullOrEmpty(entity.image.thumbnailUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.thumbnailUrl;
                        }
                    }
                    return returnVal;
                }
                else if (string.Equals(typeIdHint, "Book"))
                {
                    SchemaDotOrg.Book returnVal = new SchemaDotOrg.Book(context, "bing://" + bingId);
                    returnVal.Name.Value = StringUtils.RegexRemove(PARENTHESES_REMOVER, entity.name);
                    returnVal.Description.Value = entity.description;
                    if (entity.image != null)
                    {
                        if (!string.IsNullOrEmpty(entity.image.contentUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.contentUrl;
                        }
                        else if (!string.IsNullOrEmpty(entity.image.thumbnailUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.thumbnailUrl;
                        }
                    }
                    return returnVal;
                }
                else if (string.Equals(typeIdHint, "Restaurant"))
                {
                    SchemaDotOrg.Restaurant returnVal = new SchemaDotOrg.Restaurant(context, "bing://" + bingId);
                    returnVal.Name.Value = StringUtils.RegexRemove(PARENTHESES_REMOVER, entity.name);
                    returnVal.Description.Value = entity.description;
                    if (entity.image != null)
                    {
                        if (!string.IsNullOrEmpty(entity.image.contentUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.contentUrl;
                        }
                        else if (!string.IsNullOrEmpty(entity.image.thumbnailUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.thumbnailUrl;
                        }
                    }

                    // Pull geo info
                    if (entity.geo != null)
                    {
                        SchemaDotOrg.GeoCoordinates coords = new SchemaDotOrg.GeoCoordinates(context);
                        coords.Latitude_as_number.Value = (decimal)entity.geo.latitude;
                        coords.Longitude_as_number.Value = (decimal)entity.geo.longitude;
                        returnVal.Geo_as_GeoCoordinates.SetValue(coords);
                    }

                    if (entity.address != null)
                    {
                        SchemaDotOrg.PostalAddress address = new SchemaDotOrg.PostalAddress(context);
                        address.StreetAddress.Value = entity.address.streetAddress;
                        address.PostalCode.Value = entity.address.postalCode;
                        address.AddressLocality.Value = entity.address.addressLocality;
                        address.AddressCountry_as_string.Value = entity.address.addressCountry;
                        address.AddressRegion.Value = entity.address.addressRegion;
                        address.Description.Value = entity.address.text;
                        returnVal.Address_as_PostalAddress.SetValue(address);
                    }

                    return returnVal;
                }
                else if (string.Equals(typeIdHint, "LocalBusiness"))
                {
                    //Restaurant returnVal = new Restaurant();
                    //returnVal.Name = entity.name;
                    //returnVal.Description = entity.description;
                    //if (entity.image != null && !string.IsNullOrEmpty(entity.image.thumbnailUrl))
                    //{
                    //    returnVal.RepresentativeImage = entity.image.thumbnailUrl;
                    //}
                    //returnVal.Ids.Add("Bing", ConvertBingEntityId(entity.id));
                    //return returnVal;
                }
                else if (string.Equals(typeIdHint, "MusicGroup"))
                {
                    //MusicGroup returnVal = new MusicGroup(context, "bing://" + entity.bingId);
                    //returnVal.Name.Value = StringUtils.RegexRemove(ParenthesesRemover, entity.name);
                    //returnVal.Description.Value = entity.description;
                    //if (entity.image != null)
                    //{
                    //    if (!string.IsNullOrEmpty(entity.image.contentUrl))
                    //    {
                    //        returnVal.Image_as_URL.Value = entity.image.contentUrl;
                    //    }
                    //    else if (!string.IsNullOrEmpty(entity.image.thumbnailUrl))
                    //    {
                    //        returnVal.Image_as_URL.Value = entity.image.thumbnailUrl;
                    //    }
                    //}
                    //return returnVal;
                }
                else if (string.Equals(typeIdHint, "Attraction"))
                {
                    SchemaDotOrg.TouristAttraction returnVal = new SchemaDotOrg.TouristAttraction(context, "bing://" + bingId);
                    returnVal.Name.Value = StringUtils.RegexRemove(PARENTHESES_REMOVER, entity.name);
                    returnVal.Description.Value = entity.description;
                    if (entity.image != null)
                    {
                        if (!string.IsNullOrEmpty(entity.image.contentUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.contentUrl;
                        }
                        else if (!string.IsNullOrEmpty(entity.image.thumbnailUrl))
                        {
                            returnVal.Image_as_URL.Value = entity.image.thumbnailUrl;
                        }
                    }

                    // Pull geo info
                    if (entity.geo != null)
                    {
                        SchemaDotOrg.GeoCoordinates coords = new SchemaDotOrg.GeoCoordinates(context);
                        coords.Latitude_as_number.Value = (decimal)entity.geo.latitude;
                        coords.Longitude_as_number.Value = (decimal)entity.geo.longitude;
                        returnVal.Geo_as_GeoCoordinates.SetValue(coords);
                    }

                    return returnVal;
                }
                else if (string.Equals(typeIdHint, "School"))
                {
                    entity.GetHashCode();
                }
                else if (string.Equals(typeIdHint, "Place"))
                {
                    entity.GetHashCode();
                }
                else if (string.Equals(typeIdHint, "Region"))
                {
                    entity.GetHashCode();
                }
                else if (string.Equals(typeIdHint, "CollegeOrUniversity"))
                {
                    entity.GetHashCode();
                } // georg washington
                else if (string.Equals(typeIdHint, "MusicRecording"))
                {
                    entity.GetHashCode();
                } // who sings bohemian rhapsody
                else if (string.Equals(typeIdHint, "MusicAlbum"))
                {
                    entity.GetHashCode();
                } //nsync
                else if (string.Equals(typeIdHint, "Composition"))
                {
                    entity.GetHashCode();
                } // who wrote bohemian rhapsody
                else
                {
                    queryLogger.Log("Unknown entity type " + typeIdHint + " for entity " + entity.name);
                }
            }

            return null;
        }
    }
}
