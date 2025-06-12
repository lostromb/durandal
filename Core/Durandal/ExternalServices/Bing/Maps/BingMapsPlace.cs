using Durandal.Common.Ontology;
using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Text;

using SchemaDotOrg = Durandal.Internal.CoreOntology.SchemaDotOrg;

namespace Durandal.ExternalServices.Bing.Maps
{
    public class BingMapsPlace
    {
        public string Name { get; set; }
        public string StreetAddress { get; set; }
        public string Locality { get; set; }
        public string AdminDistrict { get; set; }
        public string CountryRegion { get; set; }
        public string PostalCode { get; set; }

        public GeoCoordinate? CenterCoord { get; set; }
        public GeoCoordinate? BoundingBoxUpperLeft { get; set; }
        public GeoCoordinate? BoundingBoxLowerRight { get; set; }
        
        public Entity ConvertToSchemaDotOrg(KnowledgeContext targetContext)
        {
            SchemaDotOrg.Place convertedPlace = new SchemaDotOrg.Place(targetContext);
            convertedPlace.Name.Value = this.Name;

            SchemaDotOrg.PostalAddress convertedAddr = new SchemaDotOrg.PostalAddress(targetContext);
            convertedAddr.Name.Value = this.Name;
            convertedAddr.AddressLocality.Value = this.Locality;
            convertedAddr.AddressRegion.Value = this.AdminDistrict;
            convertedAddr.AddressCountry_as_string.Value = this.CountryRegion;
            convertedAddr.StreetAddress.Value = this.StreetAddress;
            convertedAddr.PostalCode.Value = this.PostalCode;

            convertedPlace.Address_as_PostalAddress.SetValue(convertedAddr);

            if (this.CenterCoord.HasValue)
            {
                SchemaDotOrg.GeoCoordinates convertedCoords = new SchemaDotOrg.GeoCoordinates(targetContext);
                convertedCoords.Latitude_as_number.Value = (decimal)this.CenterCoord.Value.Latitude;
                convertedCoords.Longitude_as_number.Value = (decimal)this.CenterCoord.Value.Longitude;
                convertedPlace.Geo_as_GeoCoordinates.SetValue(convertedCoords);
            }

            if (this.BoundingBoxUpperLeft.HasValue && this.BoundingBoxLowerRight.HasValue)
            {
                double bbox0 = this.BoundingBoxUpperLeft.Value.Latitude;
                double bbox1 = this.BoundingBoxUpperLeft.Value.Longitude;
                double bbox2 = this.BoundingBoxLowerRight.Value.Latitude;
                double bbox3 = this.BoundingBoxLowerRight.Value.Longitude;
                GeoCoordinate centroid = new GeoCoordinate(
                    (bbox0 + bbox2) / 2,
                    (bbox1 + bbox3) / 2);
                // Calculate the radius of a circle needed to inscribe the bounding box
                GeoCoordinate[] bboxCoords = new GeoCoordinate[4]
                {
                                new GeoCoordinate(bbox0, bbox1),
                                new GeoCoordinate(bbox0, bbox3),
                                new GeoCoordinate(bbox2, bbox3),
                                new GeoCoordinate(bbox2, bbox1),
                };

                double circle_radius_meters =
                    (GeoMath.CalculateGeoDistance(centroid, bboxCoords[0]) +
                    GeoMath.CalculateGeoDistance(centroid, bboxCoords[1]) +
                    GeoMath.CalculateGeoDistance(centroid, bboxCoords[2]) +
                    GeoMath.CalculateGeoDistance(centroid, bboxCoords[3])) * 1000 / 4;
                
                // Express this shape in as many forms as we can (GeoCircle, GeoShape.circle, GeoShape.box, GeoShape.polygon)
                SchemaDotOrg.GeoCircle shape = new SchemaDotOrg.GeoCircle(targetContext);
                shape.GeoRadius_as_number.Value = (decimal)(circle_radius_meters);
                SchemaDotOrg.GeoCoordinates centerCoord = new SchemaDotOrg.GeoCoordinates(targetContext);
                centerCoord.Latitude_as_number.Value = (decimal)centroid.Latitude;
                centerCoord.Longitude_as_number.Value = (decimal)centroid.Longitude;
                shape.GeoMidpoint.SetValue(centerCoord);
                shape.Box.Value = string.Format("{0},{1} {2},{3}", bbox0, bbox1, bbox2, bbox3);
                shape.Circle.Value = string.Format("{0},{1} {2}", centroid.Latitude, centroid.Longitude, circle_radius_meters);
                shape.Polygon.Value = string.Format("{0},{1} {0},{3} {2},{3} {2},{1} {0},{1}", bbox0, bbox1, bbox2, bbox3);
                convertedPlace.Geo_as_GeoShape.Add(shape.As<SchemaDotOrg.GeoShape>());
            }

            return convertedPlace;
        }

        public static BingMapsPlace FromSchemaDotOrg(Entity entity)
        {
            if (!entity.IsA<SchemaDotOrg.Place>())
            {
                throw new ArgumentException("Input entity not a valid schema.org place");
            }

            SchemaDotOrg.Place place = entity.As<SchemaDotOrg.Place>();
            BingMapsPlace returnVal = new BingMapsPlace();
            returnVal.Name = place.Name.Value;

            SchemaDotOrg.GeoCoordinates coords = place.Geo_as_GeoCoordinates.ValueInMemory;
            if (coords != null)
            {
                returnVal.CenterCoord = new GeoCoordinate(
                    (double)coords.Latitude_as_number.Value.Value,
                    (double)coords.Longitude_as_number.Value.Value);
            }

            SchemaDotOrg.PostalAddress postalAddress = place.Address_as_PostalAddress.ValueInMemory;
            if (postalAddress != null)
            {
                returnVal.StreetAddress = postalAddress.StreetAddress.Value;
                returnVal.Locality = postalAddress.AddressLocality.Value;
                returnVal.AdminDistrict = postalAddress.AddressRegion.Value;
                returnVal.CountryRegion = postalAddress.AddressCountry_as_string.Value;
                returnVal.PostalCode = postalAddress.PostalCode.Value;
            }

            return returnVal;
        }
    }
}
