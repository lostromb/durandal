using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Net.Http;
using Durandal.Common.Ontology;
using Durandal.Common.Statistics;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using Durandal.Internal.CoreOntology.SchemaDotOrg;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Quotes
{
    public class QuotesPlugin : DurandalPlugin
    {
        private static IHttpClient _webClient = null;

        public QuotesPlugin() : base("quotes")
        {
        }

        public override async Task OnLoad(IPluginServices services)
        {
            _webClient = services.HttpClientFactory.CreateHttpClient(new Uri("https://www.quotes.net"), services.Logger);
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
        }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode getQuotesNode = tree.CreateNode(GetQuotes);
            tree.AddStartState("find_quotes", getQuotesNode);
            return tree;
        }

        public override async Task<TriggerResult> Trigger(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Check for the presence of person entities
            if (services.EntityHistory.FindEntities<SchemaDotOrg.Person>().Count > 0)
            {
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                return new TriggerResult(BoostingOption.NoChange);
            }

            // Check for a person name slot
            if (DialogHelpers.TryGetSlot(queryWithContext.Understanding, "person") != null)
            {
                return new TriggerResult(BoostingOption.NoChange);
            }

            return new TriggerResult(BoostingOption.Suppress);
        }

        public async Task<PluginResult> GetQuotes(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Look for a person name slot
            SchemaDotOrg.Person person = null;

            SlotValue personSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "person");
            if (personSlot != null)
            {
                IList<ContextualEntity> personEntities = personSlot.GetEntities(services.EntityContext);
                foreach (ContextualEntity r in personEntities)
                {
                    if (r.Entity.IsA<SchemaDotOrg.Person>())
                    {
                        person = r.Entity.As<SchemaDotOrg.Person>();
                        break;
                    }
                }
            }

            // If person is still null, try and pull from context
            if (person == null)
            {
                IList<Hypothesis<SchemaDotOrg.Person>> people = services.EntityHistory.FindEntities<SchemaDotOrg.Person>();
                if (people.Count > 0)
                {
                    person = people[0].Value;
                }
            }

            // If person is still null, fail out
            if (person == null)
            {
                return new PluginResult(Result.Skip);
            }

            // Get quotes
            QuotesResult quotes = await QuoteAPI.GetQuotes(person, _webClient, services.Logger).ConfigureAwait(false);

            if (quotes.Quotes == null || quotes.Quotes.Count == 0)
            {
                return new PluginResult(Result.Success)
                {
                    ResponseText = "I couldn't find any quotes by that person"
                };
            }
            else
            {
                QuotesView html = new QuotesView()
                {
                    AuthorName = person.Name.Value,
                    Quotes = quotes.Quotes
                };

                IRandom rand = new FastRandom();
                string randomQuote = quotes.Quotes[rand.NextInt(0, quotes.Quotes.Count)];

                PluginResult quotesResult = new PluginResult(Result.Success)
                {
                    ResponseSsml = randomQuote,
                    ResponseText = "\"" + randomQuote + "\"",
                    ResponseHtml = html.Render()
                };

                // Touch the person entity so it's up to date
                services.EntityHistory.AddOrUpdateEntity(person);
                return quotesResult;
            }
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            PluginInformation returnVal = new PluginInformation()
            {
                InternalName = "Quotes",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Quotes",
                ShortDescription = "Finds quotes by famous people",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Find quotes");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Famous quotes by Albert Einstein");

            return returnVal;
        }
    }
}
