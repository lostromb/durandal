using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Collections;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.NLP.Language;

namespace Durandal.Common.NLP.Annotation
{
    public class BasicAnnotatorProvider : IAnnotatorProvider
    {
        private readonly IDictionary<string, IAnnotator> _loadedAnnotators = new Dictionary<string, IAnnotator>();

        public BasicAnnotatorProvider(IEnumerable<IAnnotator> annotators = null)
        {
            if (annotators != null)
            {
                foreach (var annotator in annotators)
                {
                    _loadedAnnotators[annotator.Name] = annotator;
                }
            }
        }

        public IAnnotator CreateAnnotator(string name, LanguageCode locale, ILogger logger)
        {
            if (_loadedAnnotators.ContainsKey(name))
            {
                return _loadedAnnotators[name];
            }

            return null;
        }

        public Durandal.Common.Collections.IReadOnlySet<string> GetAllAnnotators()
        {
            return new ReadOnlySetWrapper<string>(new HashSet<string>(_loadedAnnotators.Keys));
        }
    }
}
