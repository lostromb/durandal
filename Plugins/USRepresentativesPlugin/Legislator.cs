using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Plugins.USRepresentatives
{
    public class Legislator
    {
        public string CID { get; set; }
        public string FullName { get; set; }
        public string LastName { get; set; }
        public PoliticalParty Party { get; set; }
        public string PartyName { get; set; }
        public string RepresentingState { get; set; }
        public int? DistrictNumber { get; set; }
        public int? PositionNumber { get; set; }
        public GovernmentOffice Office { get; set; }
        public Gender Gender { get; set; }
        public DateTime FirstElected { get; set; }
        public string Comments { get; set; }
        public string PhoneNumber { get; set; }
        public string FaxNumber { get; set; }
        public Uri WebSite { get; set; }
        public Uri WebContactForm { get; set; }
        public string CapitolAddress { get; set; }
        public string LocalOfficeAddress { get; set; }
        public string BioGuideId { get; set; }
        public string VoteSmartId { get; set; }
        public string FECCANDId { get; set; }
        public string TwitterHandle { get; set; }
        public string YoutubeUrl { get; set; }
        public string FacebookId { get; set; }
        public DateTime BirthDate { get; set; }
        public Uri PortraitImage { get; set; }
        public Uri PortaitThumbnail { get; set; }
    }
}
