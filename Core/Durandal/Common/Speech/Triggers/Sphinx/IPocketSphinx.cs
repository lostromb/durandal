using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx
{
    /// <summary>
    /// Represents a bridge into a single native instance of the PocketSphinx decoder.
    /// </summary>
    public interface IPocketSphinx : IKeywordSpotter, IVoiceActivityDetector, IDisposable
    {
        /// <summary>
        /// Creates the decoder and initializes it with the given model
        /// </summary>
        /// <param name="modelDir"></param>
        /// <param name="dictionaryFile"></param>
        /// <param name="verboseLogging"></param>
        bool Create(
            string modelDir,
            string dictionaryFile,
            bool verboseLogging);
    }
}
