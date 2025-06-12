using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.MathExt
{
    public struct GeoCoordinate : IEquatable<GeoCoordinate>
    {
        public double Latitude;
        public double Longitude;

        public GeoCoordinate(double lat, double lon)
        {
            Latitude = lat;
            Longitude = lon;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is GeoCoordinate))
            {
                return false;
            }

            GeoCoordinate other = (GeoCoordinate)obj;
            return Equals(other);
        }

        public bool Equals(GeoCoordinate other)
        {
            return Latitude == other.Latitude &&
                   Longitude == other.Longitude;
        }

        public override int GetHashCode()
        {
            var hashCode = -1416534245;
            hashCode = hashCode * -1521134295 + Latitude.GetHashCode();
            hashCode = hashCode * -1521134295 + Longitude.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(GeoCoordinate left, GeoCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GeoCoordinate left, GeoCoordinate right)
        {
            return !(left == right);
        }
    }
}
