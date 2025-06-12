using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Photon;
using Durandal.Common.Utils.Tasks;
using Newtonsoft.Json;
using Microsoft.Bot.Connector;
using Durandal.Common.Net.Http;
using Photon.Common.Schemas;

/// <summary>
/// check to and from
/// check valid response
/// check valid image
/// </summary>

namespace Photon.Common.Validators
{
    public class FeedValidator : IHttpResponseValidator
    {
        string toAccountId = "skillmaker.proactive";
        string fromAccountId = Guid.Parse("fa14c07e-8d40-4d31-aaba-c6b0c0181047").ToString();
        public FeedValidator()
        {
        }

        public class ProactiveFeedResponse
        {
            public string FeedName { get; set; }
            public List<Activity> Activities { get; set; }
        }

        public class ProactiveFeedsResponse
        {
            public List<ProactiveFeedResponse> FeedsResponse { get; set; }
        }

        public async Task<SingleTestResult> Validate(HttpResponse response)
        {
            if (response.PayloadData.Length == 0)
            {
                await DurandalTaskExtensions.NoOpTask;
                return new SingleTestResult()
                {
                    Success = false,
                    ErrorMessage = "Null or empty response from service"
                };
            }

            ProactiveFeedsResponse responses = JsonConvert.DeserializeObject<ProactiveFeedsResponse>(response.GetPayloadAsString());
            foreach (ProactiveFeedResponse feeds in responses.FeedsResponse)
            {
                if (feeds.FeedName == "Weather" || feeds.FeedName == "Newyork Times" || feeds.FeedName == "Groupon")
                {
                    if (!ValidateFeed(feeds))
                    {
                        return new SingleTestResult()
                        {
                            Success = false,
                            ErrorMessage = "Invalid Activity Obtained for feed " + feeds.FeedName
                        };
                    }
                    if (!ValidateResponseImage(feeds))
                    {
                        return new SingleTestResult()
                        {
                            Success = false,
                            ErrorMessage = "Incorrect Attachment received for feed " + feeds.FeedName
                        };
                    }

                }
            }
            return new SingleTestResult()
            {
                Success = true
            };
        }

        private bool ValidateResponseImage(ProactiveFeedResponse feed)
        {
            foreach (Activity act in feed.Activities)
            {
                if (act == null)
                {
                    return false;
                }
                if (act.Attachments == null || act.Attachments.Count < 1 || act.Attachments[0].ContentType != "application/vnd.microsoft.card.hero")
                {
                    return false;
                }
            }
            return true;

        }

        private bool ValidateFeed(ProactiveFeedResponse feed)
        {
            if ((feed == null) ||
            (feed.Activities.Count < 1) ||
           !string.Equals(feed.Activities[0].Recipient.Id, fromAccountId) ||
           !string.Equals(feed.Activities[0].From.Id, toAccountId))
            {
                return false;
            }
            return true;

        }
    }
}