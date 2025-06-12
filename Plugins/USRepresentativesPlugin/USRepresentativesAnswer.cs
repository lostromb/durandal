using Durandal.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Tasks;
using Durandal.Common.File;

namespace Durandal.Plugins.Plugins.USRepresentatives
{
    public class USRepresentativesAnswer : DurandalPlugin
    {
        private MyRepresentative.MyRepresentativeApi _myRepresentativeApi;
        private OpenSecrets.OpenSecretsApi _openSecretsApi;
        private BingImageApi _bingImageApi;

        public USRepresentativesAnswer() : base("us_representatives")
        {
        }

        public override async Task OnLoad(IPluginServices services)
        {
            _myRepresentativeApi = new MyRepresentative.MyRepresentativeApi(services);
            _openSecretsApi = new OpenSecrets.OpenSecretsApi(services);
            _bingImageApi = new BingImageApi(services);
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
        }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            tree.AddStartState("find_representatives", ShowRepresentatives);
            return tree;
        }

        public async Task<PluginResult> ShowRepresentatives(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Find out the user's state and district
            if (!queryWithContext.ClientContext.Latitude.HasValue ||
                !queryWithContext.ClientContext.Longitude.HasValue)
            {
                return new PluginResult(Result.Success)
                {
                    ResponseText = "I can't answer because I don't know where you are. TODO ask for zip code"
                };
            }

            // TODO Geocode
            
            return await ShowRepresentatives(queryWithContext, services, 98027, "Washington").ConfigureAwait(false);
        }

        private async Task<PluginResult> ShowRepresentatives(QueryWithContext queryWithContext, IPluginServices services, int zipCode, string stateName)
        {
            StateDistrict stateInfo = await _myRepresentativeApi.GetStateDistrictInfo(services.Logger, zipCode).ConfigureAwait(false);

            // Find all legislators
            List<Legislator> legislators = await _openSecretsApi.GetLegislators(services.Logger, services, stateInfo.StateCode, stateInfo.District).ConfigureAwait(false);

            // Resolve portraits for all of them
            List<Task<BingImageApi.ImageSearchImage>> imageTasks = new List<Task<BingImageApi.ImageSearchImage>>();
            for (int c = 0; c < legislators.Count; c++)
            {
                Legislator l = legislators[c];
                string query = (l.Office == GovernmentOffice.Senate ? "Senator" : "Representative") + " " + l.FullName;
                imageTasks.Add(_bingImageApi.GetRepresentativeImage(query, services.Logger));
            }

            int repDistrict = 0;

            for (int c = 0; c < legislators.Count; c++)
            {
                Legislator l = legislators[c];
                BingImageApi.ImageSearchImage result = await imageTasks[c].ConfigureAwait(false);
                if (result != null)
                {
                    l.PortraitImage = new Uri(result.contentUrl);
                    l.PortaitThumbnail = new Uri(result.thumbnailUrl);
                }

                if (l.Office == GovernmentOffice.HouseOfRepresentatives)
                {
                    repDistrict = l.DistrictNumber.GetValueOrDefault(0);
                }
            }

            RepresentativesListPage renderedPage = new RepresentativesListPage()
                {
                    Legislators = legislators,
                    UserRepresentativeDistrict = repDistrict,
                    UserState = stateName
                };

            return new PluginResult(Result.Success)
            {
                ResponseText = "Here are your representatives",
                ResponseSsml = "Here are your representatives",
                ResponseHtml = renderedPage.Render(),
            };
        }
    }
}
