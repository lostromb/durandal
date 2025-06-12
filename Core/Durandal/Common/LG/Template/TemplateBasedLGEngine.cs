namespace Durandal.Common.LG.Template
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using Durandal.API;
    using System.Text.RegularExpressions;
    using Durandal.Common.MathExt;
    using System.Threading.Tasks;
    using Durandal.Common.NLP.Language;

    /// <summary>
    /// This class represents a collection of LG patterns, and potentially custom code, that is used
    /// to enrich a PluginResult object with natural language. By default, these patterns are generated
    /// from a set of ini files in the /lg directory, however, they can be created programmatically as well.
    /// </summary>
    public class TemplateBasedLGEngine : ILGEngine
    {
        private IDictionary<string, IList<TemplateBasedLGPattern>> _patterns;
        private ILogger _logger;
        private IRandom _rand;

        /// <summary>
        /// A default client context used if none else is available
        /// </summary>
        private readonly ClientContext EmptyClientContext = new ClientContext();

        /// <summary>
        /// Creates a new template by parsing a .ini files
        /// </summary>
        /// <param name="sourceFiles">The LG template files to be parsed</param>
        /// <param name="fileSystem">A resource manager for locating the file</param>
        /// <param name="logger">A logger</param>
        public static async Task<TemplateBasedLGEngine> Create(IList<VirtualPath> sourceFiles, IFileSystem fileSystem, ILogger logger)
        {
            TemplateBasedLGEngine returnVal = new TemplateBasedLGEngine(logger);
            await returnVal.Initialize(sourceFiles, fileSystem).ConfigureAwait(false);
            return returnVal;
        }

        private TemplateBasedLGEngine(ILogger logger)
        {
            _logger = logger;
            _rand = new FastRandom();
            _patterns = new Dictionary<string, IList<TemplateBasedLGPattern>>();
        }

        private async Task Initialize(IList<VirtualPath> sourceFiles, IFileSystem fileSystem)
        {
            foreach (VirtualPath sourceFile in sourceFiles)
            {
                if (await fileSystem.ExistsAsync(sourceFile).ConfigureAwait(false))
                {
                    await ParseFile(sourceFile, fileSystem).ConfigureAwait(false);
                }
            }
        }

        private static readonly Regex LG_FILENAME_MATCHER = new Regex("(.+?)(?:\\.([a-z]{2}-[a-z]{2})\\.|\\.)ini");

        private async Task ParseFile(VirtualPath sourceFile, IFileSystem fileSystem)
        {
            LanguageCode defaultLocale = LanguageCode.NO_LANGUAGE;
            Match filenameMatch = LG_FILENAME_MATCHER.Match(sourceFile.Name);
            if (filenameMatch.Success && filenameMatch.Groups[2].Success)
            {
                defaultLocale = LanguageCode.TryParse(filenameMatch.Groups[2].Value);
            }

            using (StreamReader fileIn = new StreamReader(await fileSystem.OpenStreamAsync(sourceFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false)))
            {
                TemplateBasedLGPattern currentPattern = null;
                while (!fileIn.EndOfStream)
                {
                    string nextLine = fileIn.ReadLine();
                    if (string.IsNullOrWhiteSpace(nextLine))
                        continue;

                    nextLine = nextLine.Trim();
                    if (nextLine.StartsWith("#") || nextLine.StartsWith(";"))
                    {
                        // Ignore comments
                    }
                    else if (nextLine.StartsWith("[") && nextLine.EndsWith("]"))
                    {
                        nextLine = nextLine.Trim('[', ']');

                        // Ignore the [engine:template] tag
                        if (nextLine.StartsWith("Engine", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (currentPattern != null)
                        {
                            string fullName = (currentPattern.Name + ":" + currentPattern.Locale).ToLowerInvariant();
                            if (!_patterns.ContainsKey(fullName))
                            {
                                _patterns[fullName] = new List<TemplateBasedLGPattern>();
                            }
                            _patterns[fullName].Add(currentPattern);
                        }
                        currentPattern = new TemplateBasedLGPattern(_logger, EmptyClientContext);
                        // See if there's a locale in the name
                        int div = nextLine.IndexOf(':');
                        if (div >= 1)
                        {
                            // If so, create a locale-specific pattern
                            currentPattern.Name = nextLine.Substring(0, div);
                            currentPattern.Locale = LanguageCode.TryParse(nextLine.Substring(div + 1, nextLine.Length - div - 1));
                            if (currentPattern.Locale == null)
                            {
                                _logger.Log("Parsing error in LG template " + sourceFile.FullName + ". Invalid locale", LogLevel.Err);
                                _logger.Log(nextLine, LogLevel.Err);
                            }
                        }
                        else
                        {
                            // Otherwise, assume it's a locale-independent or default (specified by the filename) pattern
                            currentPattern.Name = nextLine;
                            currentPattern.Locale = defaultLocale;
                        }
                    }
                    else if (nextLine.Contains("="))
                    {
                        int div = nextLine.IndexOf('=');
                        string key = nextLine.Substring(0, div);
                        string value = nextLine.Substring(div + 1);
                        if (currentPattern == null)
                        {
                            _logger.Log("Parsing error in LG template " + sourceFile.FullName, LogLevel.Err);
                            _logger.Log(nextLine, LogLevel.Err);
                            _logger.Log("Need [PatternName:Locale] header before specifying any patterns", LogLevel.Err);
                        } 
                        else if (key.Equals("Text", StringComparison.OrdinalIgnoreCase))
                        {
                            currentPattern.SetTextTemplate(value.Replace("\\n", "\n"));
                        }
                        else if (key.Equals("ShortText", StringComparison.OrdinalIgnoreCase))
                        {
                            currentPattern.SetShortTextTemplate(value.Replace("\\n", "\n"));
                        }
                        else if (key.Equals("Spoken", StringComparison.OrdinalIgnoreCase))
                        {
                            currentPattern.SetSpokenTemplate(value);
                        }
                        else
                        {
                            // It's some field we don't know about. Assume it is custom LG data
                            currentPattern.SetExtraField(key, value);
                        }
                    }
                    else
                    {
                        _logger.Log("Badly formatted line in LG template " + sourceFile.FullName, LogLevel.Err);
                        _logger.Log(nextLine, LogLevel.Err);
                    }
                }

                // Write the last pattern
                if (currentPattern != null)
                {
                    string fullName = (currentPattern.Name + ":" + currentPattern.Locale).ToLowerInvariant();
                    if (!_patterns.ContainsKey(fullName))
                    {
                        _patterns[fullName] = new List<TemplateBasedLGPattern>();
                    }
                    _patterns[fullName].Add(currentPattern);
                }

                //fileIn.Close();
            }
        }

        /// <summary>
        /// Registers a new pattern that is backed by pure code, and is triggered by the given pattern name &amp; locale
        /// </summary>
        /// <param name="patternName">The name of the new pattern to register</param>
        /// <param name="method">The custom logic to execute</param>
        /// <param name="locale">The locale to define this pattern for. By default it is registered for all locales.</param>
        public void RegisterCustomCode(string patternName, LgCommon.RunLanguageGeneration method, LanguageCode locale)
        {
            string fullName = (patternName + ":" + locale.ToBcp47Alpha2String()).ToLowerInvariant();
            if (!_patterns.ContainsKey(fullName))
            {
                _patterns[fullName] = new List<TemplateBasedLGPattern>();
            }
            TemplateBasedLGPattern newCustomPattern = new TemplateBasedLGPattern(method, _logger, EmptyClientContext);
            _patterns[fullName].Add(newCustomPattern);
        }

        private ILGPattern GetRandomPatternFromList(IList<TemplateBasedLGPattern> patterns, int phraseNum)
        {
            return patterns[Math.Abs(phraseNum) % patterns.Count];
        }

        /// <summary>
        /// Retrieves a single pattern from the set of LG templates available.
        /// </summary>
        /// <param name="patternName">The name of the pattern to retrieve</param>
        /// <param name="clientContext">The current query's context</param>
        /// <param name="logger">A query-specific logger (optional)</param>
        /// <param name="debug">Turn on verbose debug output</param>
        /// <param name="phraseNum">Use a specific phrase instead of a random one. Used for nondeterministic unit tests.</param>
        /// <returns>The desired pattern. If none exists, a non-null empty pattern will be returned.</returns>
        public ILGPattern GetPattern(string patternName, ClientContext clientContext, ILogger logger = null, bool debug = false, int? phraseNum = null)
        {
            string key = (patternName + ":" + clientContext.Locale.ToBcp47Alpha2String()).ToLowerInvariant();
            ILogger queryLogger = logger ?? NullLogger.Singleton;
            queryLogger = queryLogger.Clone("LGPattern-" + patternName);
            
            if (_patterns.ContainsKey(key))
            {
                // A locale-specific pattern exists. Return it
                // (actually, return a clone so the caller cannot modify the actual patterns themselves)
                queryLogger.Log("Using locale-specific template " + key, LogLevel.Std);
                return GetRandomPatternFromList(_patterns[key], phraseNum.GetValueOrDefault(_rand.NextInt())).Clone(queryLogger, clientContext);
            }
            key = (patternName + ":" + LanguageCode.NO_LANGUAGE.ToBcp47Alpha2String()).ToLowerInvariant();
            if (_patterns.ContainsKey(key))
            {
                // A generic pattern exists. Return it
                queryLogger.Log("Using generic template " + key, LogLevel.Std);
                return GetRandomPatternFromList(_patterns[key], phraseNum.GetValueOrDefault(_rand.NextInt())).Clone(queryLogger, clientContext);
            }

            queryLogger.Log("LG pattern \"" + patternName + "\" doesn't exist", LogLevel.Wrn);
            return new NullLGPattern(patternName, clientContext.Locale);
        }

        /// <summary>
        /// Used for patterns that apply to UI elements or similar things where only text is needed
        /// </summary>
        /// <param name="patternName">The name of the pattern to retrieve</param>
        /// <param name="clientContext">The current query's context</param>
        /// <param name="logger">The current query's logger (optional)</param>
        /// <param name="debug"></param>
        /// <param name="phraseNum"></param>
        /// <returns>The "Text" field of the LG pattern, or empty string if the pattern is not found</returns>
        public async Task<string> GetText(string patternName, ClientContext clientContext, ILogger logger = null, bool debug = false, int? phraseNum = null)
        {
            ILGPattern p = GetPattern(patternName, clientContext, logger, debug, phraseNum);
            return (await p.Render().ConfigureAwait(false)).Text;
        }
    }
}
