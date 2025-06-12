using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Statistics.Ranking
{
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.NLP.Feature;
    using Durandal.Common.NLP.Train;

    public class RankingFeatureExtractor
    {
        public List<string> ExtractFeatures(RecoResult result)
        {
            List<string> returnVal = new List<string>();

            string actualDomainIntent = result.Domain + "/" + result.Intent;
            
            // current result vs. top result
            // returnVal.Add("dom:" + result.Domain + "/" + result.Intent + " " + topResult.Domain + "/" + topResult.Intent);
            // Slot count
            returnVal.Add("sc:" + actualDomainIntent + "/" + result.MostLikelyTags.Slots.Count);
            // Slot names and values
            for (int tagHyp = 0; tagHyp < result.TagHyps.Count; tagHyp++)
            {
                foreach (SlotValue slot in result.TagHyps[tagHyp].Slots)
                {
                    returnVal.Add("sn:" + actualDomainIntent + "/" + slot.Name + "/" + tagHyp);
                    returnVal.Add("sv:" + actualDomainIntent + "/" + slot.Name +"/" + tagHyp + "=" + slot.Value);
                    foreach (var annotation in slot.Annotations)
                    {
                        if (annotation.Key.Equals(SlotPropertyName.StartIndex) ||
                            annotation.Key.Equals(SlotPropertyName.StringLength))
                            continue;

                        returnVal.Add("ann:" + actualDomainIntent + "/" + slot.Name + "/" + tagHyp + "/" + annotation.Key);
                    }
                }
            }
            return returnVal;
        }

        public List<RankingFeature> ExtractTrainingFeatures(int utteranceId, List<RecoResult> validationResults, RecoResult expectedResult)
        {
            List<RankingFeature> returnVal = new List<RankingFeature>();

            foreach (RecoResult actualResult in validationResults)
            {
                IList<string> rawFeatures = this.ExtractFeatures(actualResult);
                
                if (rawFeatures.Count > 0)
                {
                    foreach (string feat in rawFeatures)
                    {
                        returnVal.Add(new RankingFeature(utteranceId, feat, expectedResult.Domain + "/" + expectedResult.Intent));
                    }
                }
            }

            return returnVal;
        }
    }
}
