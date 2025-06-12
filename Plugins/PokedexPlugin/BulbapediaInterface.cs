using Durandal.Common.Utils;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PokedexAnswer
{
    public static class BulbapediaInterface
    {
        public static async Task<PokemonStatisticsView> GenerateHtmlPageForPokemon(string pageUrl, ILogger logger)
        {
            WebClient client = new WebClient();
            string entirePage = Encoding.UTF8.GetString(await client.DownloadDataTaskAsync(pageUrl));

            PokemonStatisticsView returnVal = new PokemonStatisticsView();
            // Parse tables
            IList<Table> tables = ParseTables(entirePage);

            // Find the first large "round"-style html table on the page. This is most likely the infobox table
            Table infoboxTable = null;
            foreach (Table table in tables)
            {
                if (table.TagValue.Contains("class=\"roundy\"") && table.Length > 15000)
                {
                    infoboxTable = table;
                    break;
                }
            }

            // Augment the table's properties a little bit
            string finalHtml = infoboxTable.Html;
            finalHtml = StringUtils.RegexReplace(new Regex("float:right;"), finalHtml, "margin:auto;", 1);
            finalHtml = StringUtils.RegexReplace(new Regex("width:[0-9]+%;"), finalHtml, "width:100%;", 1);
            finalHtml = StringUtils.RegexReplace(new Regex("max-width:[0-9]+px;"), finalHtml, "max-width:800px;", 1);
            finalHtml = StringUtils.RegexReplace(new Regex("min-width:[0-9]+px;"), finalHtml, "min-width:320px;", 1);

            // Change relative domain links to absolute ones
            finalHtml = StringUtils.RegexReplace(new Regex("href=\"/"), finalHtml, "href=\"http://bulbapedia.bulbagarden.net/");
            finalHtml = StringUtils.RegexReplace(new Regex("src=\"/"), finalHtml, "src=\"http://bulbapedia.bulbagarden.net/");

            // And render the augmented page
            returnVal.ContentPage = finalHtml;

            return returnVal;
        }

        /// <summary>
        /// Determines the content and boundaries of every HTML table within a webpage
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private static IList<Table> ParseTables(string html)
        {
            List<Table> returnVal = new List<Table>();
            IList<TableMarker> tableMarkers = ExtractTableMarkers(html);
            int matchedMarkers = 0;
            while (matchedMarkers < tableMarkers.Count)
            {
                // Match up table markers to build tables
                TableMarker startMarker = null;
                foreach (TableMarker m in tableMarkers)
                {
                    if (!m.IsMatched)
                    {
                        if (m.IsStartTag)
                        {
                            startMarker = m;
                        }
                        else
                        {
                            TableMarker endMarker = m;
                            if (startMarker != null)
                            {
                                Table newTable = new Table();
                                newTable.StartIndex = startMarker.StartIndex;
                                newTable.TagValue = startMarker.TagValue;
                                newTable.Html = html.Substring(startMarker.StartIndex, endMarker.StartIndex + endMarker.TagValue.Length - startMarker.StartIndex);
                                newTable.Length = newTable.Html.Length;
                                returnVal.Add(newTable);
                                startMarker.IsMatched = true;
                                endMarker.IsMatched = true;
                                matchedMarkers += 2;
                                break;
                            }
                        }
                    }
                }
            }

            // Sort tables by start index
            returnVal.Sort((a, b) =>
                {
                    return a.StartIndex - b.StartIndex;
                });
            
            return returnVal;
        }

        private static IList<TableMarker> ExtractTableMarkers(string html)
        {
            IList<TableMarker> returnVal = new List<TableMarker>();

            Regex tableStartParser = new Regex("<(/)?table.*?>");
            foreach (Match match in tableStartParser.Matches(html))
            {
                returnVal.Add(new TableMarker() {
                        StartIndex = match.Index,
                        TagValue = match.Value,
                        IsStartTag = !match.Groups[1].Success,
                        IsMatched = false
                    });
            }

            return returnVal;
        }

        private class Table
        {
            public int StartIndex;
            public int Length;
            public string TagValue;
            public string Html;
        }

        private class TableMarker
        {
            public string TagValue;
            public int StartIndex;
            public bool IsStartTag;
            public bool IsMatched;
        }
    }
}
