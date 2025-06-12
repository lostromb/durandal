using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Answers.SmartThingsAnswer
{
    public class EndpointInfo
    {
        public OAuthClientInfo oauthClient { get; set; }
        public LocationInfo location { get; set; }
        public string uri { get; set; }
        public string base_url { get; set; }
        public string url { get; set; }
    }

    public class OAuthClientInfo
    {
        public string clientId;
    }

    public class LocationInfo
    {
        public string id;
        public string Home;
    }
}
