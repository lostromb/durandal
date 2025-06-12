using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.File;
using Durandal.Common.Net.Http;
using Durandal.Common.Collections;
using Durandal.Common.NLP.Language;

namespace Durandal.Common.NLP.Annotation
{
    public class HardcodedAnnotatorProvider : IAnnotatorProvider
    {
        private static readonly Durandal.Common.Collections.IReadOnlySet<string> _knownTypes = new FastConcurrentHashSet<string>()
        {
            "timex", "ordinal", "number", "canonicalizer", "location", "localplace", "speller", "person"
        };

        private IHttpClientFactory _httpClientFactory;
        private readonly IFileSystem _fileSystem;
        private string _bingMapsApiKey;
        private string _bingLocalApiKey;
        private string _bingSpellerApiKey;

        public HardcodedAnnotatorProvider(
            IHttpClientFactory httpClientFactory,
            IFileSystem fileSystem,
            string bingMapsApiKey = "",
            string bingLocalApiKey = "",
            string bingSpellerApiKey = "")
        {
            _httpClientFactory = httpClientFactory;
            _fileSystem = fileSystem;
            _bingMapsApiKey = bingMapsApiKey;
            _bingLocalApiKey = bingLocalApiKey;
            _bingSpellerApiKey = bingSpellerApiKey;
        }

        public IAnnotator CreateAnnotator(string name, LanguageCode locale, ILogger logger)
        {
            IAnnotator returnVal = null;
            ILogger annoLogger = logger.Clone("Annotator-" + name);

            if (string.Equals(name, "timex", StringComparison.OrdinalIgnoreCase))
            {
                returnVal = new TimexAnnotator(_fileSystem, locale, annoLogger);
            }
            else if (string.Equals(name, "ordinal", StringComparison.OrdinalIgnoreCase))
            {
                returnVal = new OrdinalAnnotator(_fileSystem, locale, annoLogger);
            }
            else if (string.Equals(name, "canonicalizer", StringComparison.OrdinalIgnoreCase))
            {
                returnVal = new CanonicalizationAnnotator(_fileSystem, locale, annoLogger);
            }
            else if (string.Equals(name, "location", StringComparison.OrdinalIgnoreCase))
            {
                returnVal = new LocationEntityAnnotator(_bingMapsApiKey, _httpClientFactory, annoLogger);
            }
            else if (string.Equals(name, "localplace", StringComparison.OrdinalIgnoreCase))
            {
                returnVal = new LocalPlaceEntityAnnotator(_bingLocalApiKey, _httpClientFactory, annoLogger);
            }
            else if (string.Equals(name, "speller", StringComparison.OrdinalIgnoreCase))
            {
                returnVal = new SpellerAnnotator(_bingSpellerApiKey, _httpClientFactory, annoLogger);
            }
            else if (string.Equals(name, "person", StringComparison.OrdinalIgnoreCase))
            {
                returnVal = new PersonEntityAnnotator(_httpClientFactory, annoLogger);
            }
            else if (string.Equals(name, "number", StringComparison.OrdinalIgnoreCase))
            {
                returnVal = new NumberAnnotator(_fileSystem, locale, logger);
            }
            else
            {
                logger.Log("The annotator type \"" + name + "\" is unknown!", LogLevel.Wrn);
                return null;
            }
            
            bool success = returnVal.Initialize();

            if (success)
            {
                return returnVal;
            }
            else
            {
                return null;
            }
        }

        public Durandal.Common.Collections.IReadOnlySet<string> GetAllAnnotators()
        {
            return _knownTypes;
        }
    }
}
