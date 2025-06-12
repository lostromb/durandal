namespace MediaControl.Winamp
{
    using Durandal.API;
    using Durandal.Common.Audio.Codecs.Opus.Ogg;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP;
    using Durandal.Common.Statistics;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.MediaProtocol;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class WinampLibrary
    {
        private static readonly VirtualPath LIBRARY_FILE_NAME = new VirtualPath("winamp_library.dat");
        private static readonly VirtualPath ARTIST_LOOKUP_CACHE = new VirtualPath("artist_lookup.dat");
        private static readonly VirtualPath ALBUM_LOOKUP_CACHE = new VirtualPath("album_lookup.dat");
        private const float MATCHING_THRESHOLD = 0.85f;
        private readonly ILogger _logger;
        private static readonly Dictionary<string, NLPTools> _nlTools = new Dictionary<string, NLPTools>();
        private readonly NLPTools.EditDistanceComparer _editDistance;
        private IFileSystem _cacheFileManager;
        private readonly Committer _libraryUpdater;
        private DirectoryInfo _mediaDirectory;
        private ReaderWriterLockSlim _libraryUpdateLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        private List<WinampLibraryEntry> _libraryEntries;
        private List<NamedEntity<object>> _entryTitles;
        private List<NamedEntity<object>> _entryArtists;
        private List<NamedEntity<object>> _entryAlbums;
            
        // TODO: Should start using a symbol expander, maybe do some normalization before storing the library entries. Like how does "2pac" work?
        // TODO: Use confidence level of the matcher to fail out of low-probability cases
        // TODO: Enqueing albums is still terrible - "i want to hear the ___ soundtrack"
        public WinampLibrary(ILogger logger, NLPTools.EditDistanceComparer editDistance)
        {
            _logger = logger;
            _libraryEntries = new List<WinampLibraryEntry>();
            _editDistance = editDistance;
            _libraryUpdater = new Committer(UpdateLibraryInternal);
            _nlTools["en-us"] = new NLPTools();
        }

        #region Initialization

        public void Initialize(DirectoryInfo mediaLibraryRoot, IFileSystem cacheFileSystem)
        {
            _mediaDirectory = mediaLibraryRoot;
            _cacheFileManager = cacheFileSystem;

            _libraryUpdateLock.EnterWriteLock();
            try
            {
                // Does the cache already exist?
                if (_cacheFileManager.Exists(LIBRARY_FILE_NAME))
                {
                    _logger.Log("Loading cached music library from " + LIBRARY_FILE_NAME.FullName);
                    _libraryEntries = ReadLibraryCache();
                    BuildIndexes();
                }
                else
                {
                    _logger.Log("Initializing music library from " + mediaLibraryRoot.FullName);
                    UpdateLibrary();
                    //DiscoverAllMusic(_mediaDirectory, ref _libraryEntries);
                    //WriteLibraryCache();
                }
            }
            finally
            {
                _libraryUpdateLock.ExitWriteLock();
            }

            _logger.Log("Loaded " + _libraryEntries.Count + " items from music library");
        }

        private List<WinampLibraryEntry> ReadLibraryCache()
        {
            List<WinampLibraryEntry> returnVal = new List<WinampLibraryEntry>();
            using (StreamReader cacheFileIn = new StreamReader(_cacheFileManager.OpenStream(LIBRARY_FILE_NAME, FileOpenMode.Open, FileAccessMode.Read)))
            {
                while (!cacheFileIn.EndOfStream)
                {
                    string line = cacheFileIn.ReadLine();
                    WinampLibraryEntry newEntry = new WinampLibraryEntry();
                    string[] parts = line.Split('\t');
                    if (parts.Length != 5)
                    {
                        _logger.Log("Bad input! " + line, LogLevel.Err);
                        continue;
                    }

                    newEntry.Artist = parts[0];
                    newEntry.AlbumArtist = parts[1];
                    newEntry.Album = parts[2];
                    newEntry.Title = parts[3];
                    newEntry.FilePath = parts[4];
                    returnVal.Add(newEntry);
                }

                cacheFileIn.Close();
            }

            return returnVal;
        }

        private void WriteLibraryCache()
        {
            using (StreamWriter libraryFileOut = new StreamWriter(_cacheFileManager.OpenStream(LIBRARY_FILE_NAME, FileOpenMode.Create, FileAccessMode.Write)))
            {
                foreach (WinampLibraryEntry entry in _libraryEntries)
                {
                    string title = entry.Title;
                    string artist = entry.Artist;
                    string album = entry.Album;
                    string path = entry.FilePath;
                    string albumArtist = entry.AlbumArtist;
                    libraryFileOut.WriteLine(artist + "\t" + albumArtist + "\t" + album + "\t" + title + "\t" + path);
                }

                libraryFileOut.Close();
            }
        }

        private void BuildIndexes()
        {
            _entryArtists = new List<NamedEntity<object>>();
            _entryAlbums = new List<NamedEntity<object>>();
            _entryTitles = new List<NamedEntity<object>>();
            foreach (WinampLibraryEntry entry in _libraryEntries)
            {
                List<LexicalString> artistNames = new List<LexicalString>();
                if (!string.IsNullOrEmpty(entry.AlbumArtist))
                {
                    artistNames.Add(new LexicalString(entry.AlbumArtist));
                    string sanitizedArtistName = SanitizeArtistName(entry.AlbumArtist);
                    if (!string.Equals(sanitizedArtistName, entry.AlbumArtist))
                    {
                        artistNames.Add(new LexicalString(sanitizedArtistName));
                    }
                }
                else if (!string.IsNullOrEmpty(entry.Artist))
                {
                    artistNames.Add(new LexicalString(entry.Artist));
                    string sanitizedArtistName = SanitizeArtistName(entry.Artist);
                    if (!string.Equals(sanitizedArtistName, entry.Artist))
                    {
                        artistNames.Add(new LexicalString(sanitizedArtistName));
                    }
                }

                if (artistNames.Count > 0)
                {
                    _entryArtists.Add(new NamedEntity<object>(entry, artistNames));
                }

                if (!string.IsNullOrEmpty(entry.Title))
                {
                    _entryTitles.Add(new NamedEntity<object>(entry, new LexicalString(entry.Title)));
                }

                if (!string.IsNullOrEmpty(entry.Album))
                {
                    _entryAlbums.Add(new NamedEntity<object>(entry, new LexicalString(entry.Album)));
                }
            }
        }

        private void DiscoverAllMusic(
            DirectoryInfo directory,
            ref List<WinampLibraryEntry> returnVal,
            Dictionary<string, WinampLibraryEntry> existingEntries = null)
        {
            if (directory == null || !directory.Exists)
                return;

            foreach (FileInfo file in directory.EnumerateFiles())
            {
                // Check if it's a music file
                if (IsAMusicFile(file.Name))
                {
                    // Create library entries, either from existing data or by parsing the file's tags
                    WinampLibraryEntry newEntry;
                    if (existingEntries != null && existingEntries.ContainsKey(file.FullName))
                    {
                        newEntry = existingEntries[file.FullName];
                    }
                    else
                    {
                        newEntry = CreateEntryFromMediaFile(file, _logger);
                    }

                    if (newEntry != null)
                    {
                        returnVal.Add(newEntry);
                        _logger.Log("Discovered music file " + file.Name, LogLevel.Vrb);
                    }
                    else
                    {
                        _logger.Log("Null/invalid tag in " + file.Name, LogLevel.Wrn);
                    }
                }
            }

            // Recurse into subdirectories
            foreach (DirectoryInfo subDir in directory.EnumerateDirectories())
            {
                DiscoverAllMusic(subDir, ref returnVal, existingEntries);
            }
        }

        public void UpdateLibrary()
        {
            _libraryUpdater.Commit();
        }

        private async Task UpdateLibraryInternal(IRealTimeProvider realTime)
        {
            _logger.Log("Starting library update...");

            // Build a new list of media entries based on the old one
            // Either a file is unchanged from before, for which we just copy its old entry over, or it's a new file and we parse its tags
            List<WinampLibraryEntry> newLibrary = new List<WinampLibraryEntry>();
            Dictionary<string, WinampLibraryEntry> oldLibrary = new Dictionary<string, WinampLibraryEntry>();
            foreach (WinampLibraryEntry entry in _libraryEntries)
            {
                oldLibrary.Add(entry.FilePath, entry);
            }

            DiscoverAllMusic(_mediaDirectory, ref newLibrary, oldLibrary);

            _logger.Log("Found " + newLibrary.Count + " media files; old size was " + oldLibrary.Count);

            // Enter critical region
            _libraryUpdateLock.EnterWriteLock();
            try
            {
                // Commit changes to the list of library entries and rebuild indexes and stuff
                _libraryEntries = newLibrary;
                _logger.Log("Rebuilding search index...");
                BuildIndexes();
                WriteLibraryCache();
            }
            finally
            {
                _libraryUpdateLock.ExitWriteLock();
            }

            _logger.Log("Committed library update");

            await DurandalTaskExtensions.NoOpTask;
        }

        #endregion
        
        #region Media file parsing

        private static bool IsAMusicFile(string fileName)
        {
            int lastDot = fileName.LastIndexOf('.');
            if (lastDot < 0)
                return false;
            string extension = fileName.Substring(lastDot + 1).ToLowerInvariant();
            return (extension.Equals("mp3") || extension.Equals("m4a") || extension.Equals("flac") || extension.Equals("opus"));
        }

        private static WinampLibraryEntry CreateEntryFromMediaFile(FileInfo fileName, ILogger logger)
        {
            WinampLibraryEntry returnVal = new WinampLibraryEntry();

            IDictionary<TagField, string> tags = ParseTagsFromMediaFile(fileName, logger);
            if (tags == null)
            {
                // Couldn't parse tags, so we can't continue
                return null;
            }

            // TODO: Use heuristics to derive album/artist/title when none are present
            // Perhaps use a MiscValues collection that is loosely matched?
            returnVal.Title = tags[TagField.TITLE];
            if (tags.ContainsKey(TagField.ARTIST))
            {
                returnVal.Artist = tags[TagField.ARTIST];
            }
            else if (tags.ContainsKey(TagField.ALBUM_ARTIST))
            {
                returnVal.Artist = tags[TagField.ALBUM_ARTIST];
            }

            if (tags.ContainsKey(TagField.ALBUM_ARTIST))
            {
                returnVal.AlbumArtist = tags[TagField.ALBUM_ARTIST];
            }
            else if (tags.ContainsKey(TagField.ARTIST))
            {
                returnVal.AlbumArtist = tags[TagField.ARTIST];
            }

            if (tags.ContainsKey(TagField.ALBUM))
            {
                returnVal.Album = tags[TagField.ALBUM];
            }
            returnVal.FilePath = fileName.FullName;
            return returnVal;
        }

        private static IDictionary<TagField, string> ParseTagsFromMediaFile(FileInfo inputFile, ILogger logger)
        {
            Dictionary<TagField, string> returnVal = new Dictionary<TagField, string>();

            try
            {
                // Is it a Taglib-compatible file?
                if (inputFile.Extension.Equals(".opus", StringComparison.OrdinalIgnoreCase))
                {
                    // Use Concentus for opus files
                    using (Stream fileStream = new FileStream(inputFile.FullName, FileMode.Open, FileAccess.Read))
                    {
                        OpusOggReadStream opusReader = new OpusOggReadStream(null, fileStream);
                        OpusTags tags = opusReader.Tags;
                        if (tags == null)
                        {
                            return null;
                        }

                        foreach (string key in tags.Fields.Keys)
                        {
                            if (string.Equals("Title", key, StringComparison.OrdinalIgnoreCase))
                            {
                                returnVal[TagField.TITLE] = tags.Fields[key];
                            }
                            else if (string.Equals("Artist", key, StringComparison.OrdinalIgnoreCase))
                            {
                                returnVal[TagField.ARTIST] = tags.Fields[key];
                            }
                            else if (string.Equals("ALBUMARTIST", key, StringComparison.OrdinalIgnoreCase))
                            {
                                returnVal[TagField.ALBUM_ARTIST] = tags.Fields[key];
                            }
                            else if (string.Equals("Album", key, StringComparison.OrdinalIgnoreCase))
                            {
                                returnVal[TagField.ALBUM] = tags.Fields[key];
                            }
                            else if (string.Equals("Track", key, StringComparison.OrdinalIgnoreCase)) // is this the right key?
                            {
                                returnVal[TagField.TRACK] = tags.Fields[key];
                            }
                        }

                        fileStream.Dispose();
                    }
                }
                else
                {
                    TagLib.File file = TagLib.File.Create(inputFile.FullName);
                    TagLib.Tag tag = file.Tag;
                    if (tag == null)
                    {
                        return null;
                    }

                    if (string.Equals(file.MimeType, "taglib/mp3") ||
                        string.Equals(file.MimeType, "taglib/m4a") ||
                        string.Equals(file.MimeType, "taglib/flac"))
                    {
                        if (!string.IsNullOrEmpty(tag.Title))
                        {
                            returnVal[TagField.TITLE] = tag.Title;
                        }
                        else
                        {
                            returnVal[TagField.TITLE] = inputFile.Name.Substring(0, inputFile.Name.Length - inputFile.Extension.Length);
                        }
                        
                        if (!string.IsNullOrEmpty(tag.FirstPerformer))
                        {
                            returnVal[TagField.ARTIST] = tag.FirstPerformer;
                        }
                        if (tag.Track > 0)
                        {
                            returnVal[TagField.TRACK] = tag.Track.ToString();
                        }
                        if (!string.IsNullOrEmpty(tag.Album))
                        {
                            returnVal[TagField.ALBUM] = tag.Album;
                        }
                        if (!string.IsNullOrEmpty(tag.FirstAlbumArtist))
                        {
                            returnVal[TagField.ALBUM_ARTIST] = tag.FirstAlbumArtist;
                        }
                    }
                }

                return returnVal;
            }
            catch (Exception e)
            {
                logger.Log("Error while loading tags from " + inputFile.FullName, LogLevel.Err);
                logger.Log(e, LogLevel.Err);
                return null;
            }
        }

        #endregion

        #region Library querying
        
        private static readonly Regex ARTIST_NAME_SANITIZER = new Regex("(^The )|(\\s*[\\(\\[].+?[\\)\\]]$)", RegexOptions.IgnoreCase);

        private static string SanitizeArtistName(string artist)
        {
            return StringUtils.RegexRemove(ARTIST_NAME_SANITIZER, artist);
        }

        private IList<WinampLibraryEntry> SearchByArtist(string artist)
        {
            string query = SanitizeArtistName(artist);
            _logger.Log("Querying by artist \"" + query + "\"");

            IList<Hypothesis<object>> hyps = new DefaultEntityResolver(new GenericEntityResolver(_nlTools, null)).ResolveEntity(new LexicalString(query), _entryArtists, "en-us", _logger).Await();

            IList<WinampLibraryEntry> results = new List<WinampLibraryEntry>();
            foreach (Hypothesis<object> hyp in hyps)
            {
                if (hyp.Conf > MATCHING_THRESHOLD)
                {
                    WinampLibraryEntry entry = hyp.Value as WinampLibraryEntry;
                    _logger.Log("Hyp " + entry.Title + " conf " + hyp.Conf, LogLevel.Vrb);
                    results.Add(entry);
                }
            }

            return results;
        }

        private IList<WinampLibraryEntry> SearchByAlbum(string album)
        {
            string query = album;
            _logger.Log("Querying by album \"" + query + "\"");


            IList<Hypothesis<object>> hyps = new DefaultEntityResolver(new GenericEntityResolver(_nlTools, null)).ResolveEntity(new LexicalString(query), _entryAlbums, "en-us", _logger).Await();

            IList<WinampLibraryEntry> results = new List<WinampLibraryEntry>();
            foreach (Hypothesis<object> hyp in hyps)
            {
                if (hyp.Conf > MATCHING_THRESHOLD)
                {
                    WinampLibraryEntry entry = hyp.Value as WinampLibraryEntry;
                    _logger.Log("Hyp " + entry.Title + " conf " + hyp.Conf, LogLevel.Vrb);
                    results.Add(entry);
                }
            }

            return results;
        }

        private IList<WinampLibraryEntry> SearchByTitle(string song)
        {
            string query = song;
            _logger.Log("Querying by title \"" + query + "\"");

            IList<Hypothesis<object>> hyps = new DefaultEntityResolver(new GenericEntityResolver(_nlTools, null)).ResolveEntity(new LexicalString(query), _entryTitles, "en-us", _logger).Await();

            IList<WinampLibraryEntry> results = new List<WinampLibraryEntry>();
            foreach (Hypothesis<object> hyp in hyps)
            {
                if (hyp.Conf > MATCHING_THRESHOLD)
                {
                    WinampLibraryEntry entry = hyp.Value as WinampLibraryEntry;
                    _logger.Log("Hyp " + entry.Title + " conf " + hyp.Conf, LogLevel.Vrb);
                    results.Add(entry);
                }
            }

            return results;
        }

        public IList<WinampLibraryEntry> Query(EnqueueMediaCommand command)
        {
            if (command == null)
            {
                return new List<WinampLibraryEntry>();
            }

            _libraryUpdateLock.EnterReadLock();
            try
            {
                HashSet<WinampLibraryEntry> resultSet = new HashSet<WinampLibraryEntry>();

                // Step 1 is run keyword search if we need to
                if (!string.IsNullOrEmpty(command.SearchTerm))
                {
                    Regex searchRegex = new Regex(command.SearchTerm, RegexOptions.IgnoreCase);
                    foreach (WinampLibraryEntry entry in _libraryEntries)
                    {
                        if (entry.ContainsSearchTerm(searchRegex))
                        {
                            resultSet.Add(entry);
                        }
                    }
                }

                // Then process all the remaining subsearches and intersect them with the result set as necessary

                if (!string.IsNullOrEmpty(command.Title))
                {
                    var r = SearchByTitle(command.Title);
                    if (resultSet.Count == 0)
                    {
                        resultSet.UnionWith(r);
                    }
                    else
                    {
                        resultSet.IntersectWith(r);
                    }
                }

                if (!string.IsNullOrEmpty(command.Artist))
                {
                    var r = SearchByArtist(command.Artist);
                    if (resultSet.Count == 0)
                    {
                        resultSet.UnionWith(r);
                    }
                    else
                    {
                        resultSet.IntersectWith(r);
                    }
                }

                if (!string.IsNullOrEmpty(command.Album))
                {
                    var r = SearchByAlbum(command.Album);
                    if (resultSet.Count == 0)
                    {
                        resultSet.UnionWith(r);
                    }
                    else
                    {
                        resultSet.IntersectWith(r);
                    }
                }

                if (!string.IsNullOrEmpty(command.Subquery))
                {
                    Regex subQuery = new Regex(command.Subquery, RegexOptions.IgnoreCase);
                    List<WinampLibraryEntry> toRemove = new List<WinampLibraryEntry>();
                    foreach (WinampLibraryEntry entry in resultSet)
                    {
                        if (!entry.ContainsSearchTerm(subQuery))
                        {
                            toRemove.Add(entry);
                        }
                    }

                    // todo: if the subquery filters out all results, do we still want to apply it?

                    resultSet.ExceptWith(toRemove);
                }

                // TODO should I like, sort the list in order of album + track number on the way out?

                return new List<WinampLibraryEntry>(resultSet);
            }
            finally
            {
                _libraryUpdateLock.ExitReadLock();
            }
        }

        #endregion
    }
}
