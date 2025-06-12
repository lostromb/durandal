
namespace Durandal.Plugins
{
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using Durandal.CommonViews;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class WikipediaPlugin : DurandalPlugin
    {
        public WikipediaPlugin()
            : base("wikipedia")
        {
        }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            tree.AddStartState("wiki_search", WikiSearch);
            tree.AddStartState("wikihow_search", WikihowSearch);
            return tree;
        }

        public override async Task<TriggerResult> Trigger(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "query")))
            {
                services.Logger.Log("Suppressing wikipedia answer because no query was extracted");
                return new TriggerResult(BoostingOption.Suppress);
            }

            return new TriggerResult(BoostingOption.NoChange);
        }

        public async Task<PluginResult> WikiSearch(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            string query = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "query");

            if (string.IsNullOrWhiteSpace(query))
            {
                services.Logger.Log("No query extracted");
                return new PluginResult(Result.Skip);
            }

            query = query.TrimEnd('?');

            return new PluginResult(Result.Success)
            {
                ResponseText = "I don't know that",
                ResponseHtml = new MessageView()
                {
                    Content = "I don't know that",
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            };

            //string pageTitle = GetWikiPageTitle(query, services.Logger);
            //if (string.IsNullOrWhiteSpace(pageTitle))
            //{
            //    return new PluginResult(Result.Skip)
            //    {
            //        ResponseSsml = "I could't find anything about " + query,
            //        ResponseText = "I could't find anything about " + query
            //    };
            //}

            //string summary = GetEntitySummary(pageTitle);
            //if (string.IsNullOrWhiteSpace(summary))
            //{
            //    return new PluginResult(Result.Skip)
            //    {
            //        ResponseSsml = "I could't find anything about " + query,
            //        ResponseText = "I could't find anything about " + query
            //    };
            //}

            //return new PluginResult(Result.Success)
            //{
            //    ResponseSsml = summary,
            //    ResponseText = summary
            //};
        }

        public async Task<PluginResult> WikihowSearch(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            string query = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "query");

            if (string.IsNullOrWhiteSpace(query))
            {
                services.Logger.Log("No query extracted");
                return new PluginResult(Result.Skip);
            }

            query = query.TrimEnd('?');

            string searchURL = "http://www.wikihow.com/Special:GoogSearch?cx=008953293426798287586%3Amr-gwotjmbs&cof=FORID%3A10&ie=UTF-8&q=" + WebUtility.UrlEncode(query) + "&siteurl=www.wikihow.com%2FMain-Page";
            services.Logger.Log("Searching WikiHow for " + query);
            return new PluginResult(Result.Success)
            {
                ResponseSsml = "This should help you out",
                ResponseText = "This should help you out",
                ResponseUrl = searchURL
            };
        }

        //private class BingEntityResponse
        //{
        //    public string _type;
        //}

        //private static BingEntityResponse GetEntities(string query)
        //{
        //    return null;
        //    // https://www.bingapis.com/api/v5/search?knowledge=1&vi=web-kirinprod&screenshot=1&q=harry+potter&appid=D41D8CD98F00B204E9800998ECF8427E3F0E9789&responseformat=json&responsesize=s&mkt=en-US&setlang=en-US&seemorefilter=EntityLookupAnswer
        //}

        //private static string GetWikiPageTitle(string topic, ILogger logger)
        //{
        //    logger.Log("Searching Wikipedia for " + topic);
        //    string url = "http://en.wikipedia.org/w/api.php?action=query&list=search&format=json&srsearch=" + HttpUtility.UrlEncode(topic);
        //    logger.Log("Search endpoint is " + url);
        //    string searchJson = DurandalUtils.Await(DurandalUtils.HttpGetAsync(url));

        //    JsonSerializer ser = JsonSerializer.Create(new JsonSerializerSettings());
        //    JsonTextReader reader = new JsonTextReader(new StringReader(searchJson));
        //    JObject returnVal = ser.Deserialize(reader) as JObject;

        //    if (returnVal == null || returnVal["query"] == null || returnVal["query"]["search"] == null)
        //    {
        //        logger.Log("No results came back from search endpoint!", LogLevel.Err);
        //        return null;
        //    }

        //    JArray resultsArray = returnVal["query"]["search"] as JArray;

        //    if (resultsArray == null || resultsArray.Count == 0)
        //    {
        //        logger.Log("Invalid results", LogLevel.Err);
        //        return null;
        //    }

        //    string topHit = null;

        //    foreach (JObject x in resultsArray.Children<JObject>())
        //    {
        //        if (x != null && x["title"] != null)
        //        {
        //            logger.Log("Got search hit: " + x["title"].Value<string>());

        //            if (string.IsNullOrEmpty(topHit))
        //            {
        //                topHit = x["title"].Value<string>();
        //            }
        //        }
        //    }

        //    return topHit;
        //}

        //private static string GetEntitySummary(string wikiPageTitle)
        //{
        //    string queryResponse = DurandalUtils.Await(DurandalUtils.HttpGetAsync("http://en.wikipedia.org/w/api.php?action=query&prop=revisions&rvprop=content&format=json&titles=" + HttpUtility.UrlEncode(wikiPageTitle)));

        //    JsonSerializer ser = JsonSerializer.Create(new JsonSerializerSettings());
        //    JsonTextReader reader = new JsonTextReader(new StringReader(queryResponse));
        //    JObject returnVal = ser.Deserialize(reader) as JObject;

        //    if (returnVal == null || returnVal["query"] == null || returnVal["query"]["pages"] == null)
        //        return null;

        //    JObject pageList = returnVal["query"]["pages"] as JObject;

        //    if (pageList == null || pageList.Count == 0 || pageList.First == null)
        //        return null;

        //    JProperty pageKey = pageList.First as JProperty;

        //    if (pageKey ==null)
        //        return null;

        //    JObject pageObject = pageKey.Value as JObject;

        //    if (pageObject == null || pageObject["revisions"] == null || pageObject["revisions"][0] == null || pageObject["revisions"][0]["*"] == null)
        //        return null;

        //    string page = pageObject["revisions"][0]["*"].Value<string>();

        //    // Now parse the page, remove all the wiki formatting, and return the first complete sentence.
        //    page = RemoveWikiML(page);
        //    page = StringUtils.RegexReplace(new Regex(@" ?\(.+?\) ?"), page, " ");
        //    string summary = StringUtils.RegexRip(new Regex(@"([\w\W]+?(?:[\s\.]\w\.[\w\W]*?)*?)\.(?:[^\w]|$)"), page, 1).Trim();
        //    return summary;
        //}

        //private static string RemoveWikiML(string page)
        //{
        //    page = RemoveWikiInfoBoxes(page);
        //    page = StringUtils.RegexRemove(new Regex(@"<!--.+?-->"), page);
        //    page = new Regex(@"\[\[([^|]+?)\]\]").Replace(page, (match) => { return match.Groups[1].Value; });
        //    page = new Regex(@"\[\[.+?\|(.+?)\]\]").Replace(page, (match) => { return match.Groups[1].Value; });
        //    page = StringUtils.RegexRemove(new Regex(@"<ref[\w\W]+?</ref>"), page);
        //    page = new Regex(@"(''+)([\w\W]+?)\1").Replace(page, (match) => { return match.Groups[2].Value; });
        //    page = new Regex(@"(==+)([\w\W]+?)\1").Replace(page, (match) => { return "\r\n" + match.Groups[1].Value + "\r\n"; });
        //    return page;
        //}

        //private static string RemoveWikiInfoBoxes(string page)
        //{
        //    int index = 0;
        //    int depth = 0;
        //    Regex bracketMatcher = new Regex(@"(\{\{)|(\}\})");
        //    StringBuilder returnVal = new StringBuilder();

        //    foreach (Match m in bracketMatcher.Matches(page))
        //    {
        //        if (m.Groups[1].Success)
        //        {
        //            if (depth == 0)
        //            {
        //                returnVal.Append(page.Substring(index, m.Index - index));
        //            }
        //            index = m.Index + m.Length;
        //            depth++;
        //        }
        //        else if (m.Groups[2].Success)
        //        {
        //            index = m.Index + m.Length;
        //            depth--;
        //        }
        //    }

        //    return returnVal.ToString();
        //}

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            PluginInformation returnVal = new PluginInformation()
            {
                InternalName = "Wikipedia",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Knowledge",
                ShortDescription = "TODO: Connect with Satori",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Who is Bill Gates?");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("What is glycolisis?");

            return returnVal;
        }
    }
}
