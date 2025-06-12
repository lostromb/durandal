using Durandal.Common.Logger;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Collections;
using Durandal.Common.NLP.Language;

namespace Durandal.Common.NLP.Annotation
{
    public interface IAnnotatorProvider
    {
        Durandal.Common.Collections.IReadOnlySet<string> GetAllAnnotators();

        IAnnotator CreateAnnotator(string name, LanguageCode locale, ILogger logger);
    }
}
