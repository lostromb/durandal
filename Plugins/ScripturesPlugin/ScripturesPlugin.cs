
namespace Durandal.Plugins.Scriptures
{
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP;
    using Durandal.Common.Statistics;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using Durandal.CommonViews;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class ScripturesPlugin : DurandalPlugin
    {
        private IDictionary<string, Verse> _verses;
        private IList<NamedEntity<string>> _bookLookup;
        private IList<NamedEntity<string>> _bookLookupForSpeech;
        private IDictionary<string, string> _keyToBookTable;
        private IDictionary<string, string> _keyToCanonTable;
        
        public ScripturesPlugin()
            : base("scriptures")
        {
            _verses = new Dictionary<string, Verse>();
            _bookLookup = new List<NamedEntity<string>>();
            _bookLookupForSpeech = new List<NamedEntity<string>>();
            _keyToBookTable = new Dictionary<string, string>();
            _keyToCanonTable = new Dictionary<string, string>();
        }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            tree.AddStartState("look_up_reference", LookUpVerse);
            
            return tree;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            VirtualPath scriptureDatabaseFile = services.PluginDataDirectory + "/scriptures.tsv";
            if (await services.FileSystem.ExistsAsync(scriptureDatabaseFile).ConfigureAwait(false))
            {
                foreach (string scriptureEntry in await services.FileSystem.ReadLinesAsync(scriptureDatabaseFile).ConfigureAwait(false))
                {
                    // FIXME: This allocates a lot of memory
                    string[] parts = scriptureEntry.Split('\t');
                    if (parts.Length != 6)
                        continue;

                    Verse newVerse = new Verse();
                    newVerse.Url = parts[0];
                    newVerse.CanonCode = parts[1];
                    newVerse.BookCode = parts[2];
                    newVerse.ChapterNum = int.Parse(parts[3]);
                    newVerse.VerseNum = int.Parse(parts[4]);
                    newVerse.Content = parts[5];

                    string key = newVerse.BookCode + ":" + newVerse.ChapterNum + ":" + newVerse.VerseNum;
                    _verses[key] = newVerse;
                }
            }

            services.Logger.Log("Loaded " + _verses.Count + " scripture references");

            IDictionary<string, IList<LexicalString>> _mappings = new Dictionary<string, IList<LexicalString>>();
            IDictionary<string, IList<LexicalString>> _mappingsSpeech = new Dictionary<string, IList<LexicalString>>();
            VirtualPath entityMappingFile = services.PluginDataDirectory + "/map_book_to_key.dat";
            if (await services.FileSystem.ExistsAsync(entityMappingFile).ConfigureAwait(false))
            {
                foreach (string mappingEntry in await services.FileSystem.ReadLinesAsync(entityMappingFile).ConfigureAwait(false))
                {
                    string[] parts = mappingEntry.Split('\t');
                    if (parts.Length != 3)
                        continue;

                    // FIXME this code attempts to handle lexical matching by isolating spoken and written-only forms, but it doesn't do so in a way that actually utilizes phonetics. Fix by actually incorporating the IPA lexical form into the table
                    LexicalString from = new LexicalString(parts[0]);
                    string to = parts[1];
                    bool textOnly = parts[2].Equals("1");

                    if (!_mappings.ContainsKey(to))
                    {
                        _mappings[to] = new List<LexicalString>();
                        _mappingsSpeech[to] = new List<LexicalString>();
                    }

                    _mappings[to].Add(from);
                    if (!textOnly)
                    {
                        _mappingsSpeech[to].Add(from);
                    }
                }
            }
            foreach (var entry in _mappings)
            {
                _bookLookup.Add(new NamedEntity<string>(entry.Key, entry.Value));
            }
            foreach (var entry in _mappingsSpeech)
            {
                _bookLookupForSpeech.Add(new NamedEntity<string>(entry.Key, entry.Value));
            }

            // Load the other mapping tables
            VirtualPath keyToBookTable = services.PluginDataDirectory + "/map_key_to_book.dat";
            if (await services.FileSystem.ExistsAsync(keyToBookTable).ConfigureAwait(false))
            {
                foreach (string mappingEntry in await services.FileSystem.ReadLinesAsync(keyToBookTable).ConfigureAwait(false))
                {
                    string[] parts = mappingEntry.Split('\t');
                    if (parts.Length != 2)
                        continue;

                    _keyToBookTable.Add(parts[0], parts[1]);
                }
            }

            VirtualPath keyToCanonTable = services.PluginDataDirectory + "/map_key_to_canon.dat";
            if (await services.FileSystem.ExistsAsync(keyToCanonTable).ConfigureAwait(false))
            {
                foreach (string mappingEntry in await services.FileSystem.ReadLinesAsync(keyToCanonTable).ConfigureAwait(false))
                {
                    string[] parts = mappingEntry.Split('\t');
                    if (parts.Length != 2)
                        continue;

                    _keyToCanonTable.Add(parts[0], parts[1]);
                }
            }
        }

        private MessageView RenderVerse(Verse result, ClientContext clientContext)
        {
            string canon = result.CanonCode;
            if (_keyToCanonTable.ContainsKey(canon))
            {
                canon = _keyToCanonTable[canon];
            }

            string book = result.BookCode;
            if (_keyToBookTable.ContainsKey(book))
            {
                book = _keyToBookTable[book];
            }
            
            MessageView returnVal = new MessageView()
            {
                Content = result.Content,
                Subscript = string.Format("{0}, {1} {2}:{3}", canon, book, result.ChapterNum, result.VerseNum),
                TextAlign = "left",
                ClientContextData = clientContext.ExtraClientContext
            };
            return returnVal;
        }

        public async Task<PluginResult> LookUpVerse(QueryWithContext queryWithContext, IPluginServices services)
        {
            LexicalString bookName = DialogHelpers.TryGetLexicalSlotValue(queryWithContext.Understanding, "book");

            if (bookName == null ||
                string.IsNullOrEmpty(bookName.WrittenForm))
            {
                services.Logger.Log("No book name tagged");
                return new PluginResult(Result.Skip);
            }

            // Resolve the book
            IList<Hypothesis<string>> resolvedBooks = await services.EntityResolver.ResolveEntity<string>(
                bookName,
                queryWithContext.Source == InputMethod.Spoken ? _bookLookupForSpeech : _bookLookup,
                queryWithContext.ClientContext.Locale,
                services.Logger).ConfigureAwait(false);

            if (resolvedBooks.Count == 0 || resolvedBooks[0].Conf < 0.8f)
            {
                services.Logger.Log("Book resolution was too ambiguous");
                return new PluginResult(Result.Skip);
            }

            string resolvedBookName = resolvedBooks[0].Value;

            bool bookHasNoChapters = resolvedBookName.Equals("w-of-m") ||
                resolvedBookName.Equals("a-of-f") ||
                resolvedBookName.Equals("jarom") ||
                resolvedBookName.Equals("omni") ||
                resolvedBookName.Equals("enos");

            // Iterate through all tag hypotheses to find the proper chapter / verse reference
            SlotValue chapter = null;
            SlotValue verse = null;
            foreach (var tagResult in queryWithContext.Understanding.TagHyps)
            {
                chapter = DialogHelpers.TryGetSlot(tagResult, "chapter");
                verse = DialogHelpers.TryGetSlot(tagResult, "verse");

                if ((bookHasNoChapters || chapter != null) && verse != null)
                {
                    break;
                }
            }

            Verse result = null;

            if (chapter == null && bookHasNoChapters)
            {
                // Special handling for books in which the chapter name can be omitted, because there is only one of them
                if (verse == null)
                {
                    services.Logger.Log("No verse string tagged");
                    return new PluginResult(Result.Skip);
                }

                decimal? verseNum = verse.GetNumber();

                if (verseNum == null)
                {
                    services.Logger.Log("Verse has no resolved number attribute");
                    return new PluginResult(Result.Skip);
                }

                // Now get the actual verse
                string key = resolvedBookName + ":1:" + verseNum;
                if (!_verses.ContainsKey(key))
                {
                    services.Logger.Log("The specified verse " + key + " does not exist");
                    return new PluginResult(Result.Skip);
                }

                result = _verses[key];
            }
            else
            {
                if (verse == null || chapter == null)
                {
                    services.Logger.Log("No verse or chapter tagged");
                    return new PluginResult(Result.Skip);
                }

                decimal? chapterNum = chapter.GetNumber();
                decimal? verseNum = verse.GetNumber();

                if (chapterNum == null || verseNum == null)
                {
                    services.Logger.Log("No numerical resolution for either the verse or chapter");
                    return new PluginResult(Result.Skip);
                }

                // Now get the actual verse
                string key = resolvedBookName + ":" + chapterNum + ":" + verseNum;
                if (!_verses.ContainsKey(key))
                {
                    services.Logger.Log("The specified verse " + key + " does not exist");
                    return new PluginResult(Result.Skip);
                }

                result = _verses[key];
            }

            MessageView html = RenderVerse(result, queryWithContext.ClientContext);

            return new PluginResult(Result.Success)
            {
                ResponseText = result.Content,
                ResponseSsml = result.Content,
                ResponseHtml = html.Render()
            };
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            using (MemoryStream pngStream = new MemoryStream())
            {
                if (pluginDataDirectory != null && pluginDataManager != null)
                {
                    VirtualPath iconFile = pluginDataDirectory + "\\icon.png";
                    if (pluginDataManager.Exists(iconFile))
                    {
                        using (Stream iconStream = pluginDataManager.OpenStream(iconFile, FileOpenMode.Open, FileAccessMode.Read))
                        {
                            iconStream.CopyTo(pngStream);
                        }
                    }
                }

                PluginInformation returnVal = new PluginInformation()
                {
                    InternalName = "Scriptures",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Scripture",
                    ShortDescription = "Looks up single verses of scripture and reads them",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Recite Psalms 23:4");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Quote 3rd Nephi chapter 9 verse 21");

                return returnVal;
            }
        }
    }
}
