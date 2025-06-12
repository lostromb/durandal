using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Time.TimeZone
{
    /// <summary>
    /// Schema for entries in the IANA zone1970.tab file
    /// </summary>
    internal class IanaTimeZoneMetadata
    {
        public ISet<string> Countries;
        public GeoCoordinate PrincipalCoordinate;
        public string ZoneName;
        public string Comment;
    }
}
