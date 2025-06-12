using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.LG.Statistical
{
    /// <summary>
    /// Exposes statistical language generation in as simple and generic of a way as possible.
    /// This class is designed for any general-purpose use that requires a program to generate
    /// natural-sounding, grammatically correct outputs that vary based on some parameters.
    /// </summary>
    public class SimpleStatisticalLGEngine
    {
        private string _modelName = "LGModel";
        private bool _initialized = false;
        private HybridFileSystem _virtualFilesystem;
        private ILGScriptCompiler _lgScriptCompiler;
        private InMemoryFileSystem _templateFilesystem;
        private IFileSystem _cacheFilesystem;
        private VirtualPath _templateFileName;
        private ILogger _logger;
        private StatisticalLGEngine _core;
        private ICultureInfoFactory _cultureInfoFactory;

        /// <summary>
        /// Creates a simple statistical LG engine.
        /// </summary>
        /// <param name="modelFileStream">The stream from which to load a template file that will describe the model</param>
        /// <param name="logger">An optional logger</param>
        /// <param name="cultureInfoFactory">An optional provider for CultureInfo objects, which is used by a few slot transformers.
        /// Most of the time you will want to use WindowsCultureInfoFactory</param>
        /// <param name="scriptCompiler">An optional C# compiler that will be used to compile in-line LG scripts.</param>
        public SimpleStatisticalLGEngine(
            Stream modelFileStream,
            ILogger logger = null,
            ICultureInfoFactory cultureInfoFactory = null,
            ILGScriptCompiler scriptCompiler = null)
        {
            // FIXME: This whole class needs to have a proper async constructor and use async IO methods everywhere
            _logger = logger ?? NullLogger.Singleton;
            _cultureInfoFactory = cultureInfoFactory ?? new InvariantCultureInfoFactory();

            _virtualFilesystem = new HybridFileSystem(NullFileSystem.Singleton);
            _templateFilesystem = new InMemoryFileSystem();
            _virtualFilesystem.AddRoute(new VirtualPath(RuntimeDirectoryName.LG_DIR), _templateFilesystem);
            _lgScriptCompiler = scriptCompiler;
        
            // Load the model file into the fake virtual filesystem
            _templateFileName = new VirtualPath(RuntimeDirectoryName.LG_DIR + "\\" + _modelName + ".lg");
            using (Stream writeStream = _virtualFilesystem.OpenStream(_templateFileName, FileOpenMode.Create, FileAccessMode.Write))
            {
                modelFileStream.CopyTo(writeStream);
            }
        }

        /// <summary>
        /// Initializes and trains the statistical LG model without caching it.
        /// </summary>
        public async Task Initialize()
        {
            // Set the cache to be a black hole
            _cacheFilesystem = NullFileSystem.Singleton;
            _virtualFilesystem.AddRoute(new VirtualPath(RuntimeDirectoryName.CACHE_DIR), _cacheFilesystem);
            await InitializeInternal().ConfigureAwait(false);
        }

        /// <summary>
        /// Initializes and potentially trains a statistical LG model, reading and writing model data from the specified cache file.
        /// </summary>
        /// <param name="cacheSource">A representation of a file system where the cache data file will be located</param>
        /// <param name="cacheFile">The virtual path to the cache data file, for reading + writing</param>
        public async Task InitializeWithCache(IFileSystem cacheSource, VirtualPath cacheFile)
        {
            // Load the cache data into our virtual filesystem (if it exists)
            InMemoryFileSystem inMemoryCache;
            if (cacheSource.Exists(cacheFile))
            {
                _logger.Log("Loading cached LG files from " + cacheFile.FullName);
                using (Stream readStream = await cacheSource.OpenStreamAsync(cacheFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
                {
                    inMemoryCache = InMemoryFileSystem.Deserialize(readStream, false);
                }
            }
            else
            {
                inMemoryCache = new InMemoryFileSystem();
            }

            _cacheFilesystem = inMemoryCache;
            _virtualFilesystem.AddRoute(new VirtualPath(RuntimeDirectoryName.CACHE_DIR), _cacheFilesystem);

            await InitializeInternal().ConfigureAwait(false);

            // Write the cache back afterwards
            using (Stream writeStream = await cacheSource.OpenStreamAsync(cacheFile, FileOpenMode.Create, FileAccessMode.Write).ConfigureAwait(false))
            {
                inMemoryCache.Serialize(writeStream, true, false);
            }

            // And also delete any data in memory since we've already persisted it
            FileHelpers.DeleteAllFiles(inMemoryCache, VirtualPath.Root, _logger);
        }

        /// <summary>
        /// Renders the given phrase into text and SSML forms
        /// </summary>
        /// <param name="phraseName">The name of the phrase to render, as specified in the template</param>
        /// <param name="locale">The locale to render in. Locales are always lowercase ISO 639 alpha-2, for example "en-US"</param>
        /// <param name="substitutions">The set of substitutions to be inserted into the phrase. These are the parameters of the sentence</param>
        /// <param name="variants">Any extra variants that should be used to select candidate phrases. For example, you can use variants to alter which phrase to use based on user's modality or UI state.</param>
        /// <param name="debug">If true, log verbose data about what slot transformers are doing what (make sure your logger is also set to verbose to see it)</param>
        /// <returns>The rendered LG phrase in text and SSML forms</returns>
        public Task<RenderedLG> Render(string phraseName, LanguageCode locale, IDictionary<string, object> substitutions = null, IDictionary<string, string> variants = null, bool debug = false)
        {
            ClientContext fakeContext = new ClientContext()
            {
                Locale = locale,
                Capabilities = (ClientCapabilities.DisplayUnlimitedText | ClientCapabilities.CanSynthesizeSpeech)
            };

            ILGPattern pattern = _core.GetPattern(phraseName, fakeContext, variants, _logger, debug);
            if (substitutions != null)
            {
                foreach (var sub in substitutions)
                {
                    pattern = pattern.Sub(sub.Key, sub.Value);
                }
            }

            return pattern.Render();
        }

        private async Task InitializeInternal()
        {
            if (_initialized)
            {
                throw new InvalidOperationException("LG engine has already been initialized!");
            }

            _initialized = true;

            List<VirtualPath> templateFiles = new List<VirtualPath>();
            templateFiles.Add(_templateFileName);
            INLPToolsCollection nlTools = BuildNLTools();

            _core = await StatisticalLGEngine.Create(_virtualFilesystem, _logger, _modelName, _lgScriptCompiler, templateFiles, nlTools).ConfigureAwait(false);
        }

        private INLPToolsCollection BuildNLTools()
        {
            NLPToolsCollection returnVal = new NLPToolsCollection();
            returnVal.Add(LanguageCode.EN_US,
                new NLPTools()
                {
                    FeaturizationWordBreaker = new EnglishWordBreaker(),
                    WordBreaker = new EnglishWholeWordBreaker(),
                    LGFeatureExtractor = new EnglishLGFeatureExtractor(),
                    CultureInfoFactory = _cultureInfoFactory
                });

            return returnVal;
        }
    }
}
