using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx
{
    public interface IKeywordSpotter
    {
        /// <summary>
        /// Reconfigures the keyword spotting configuration
        /// </summary>
        /// <param name="keywordFile"></param>
        /// <returns></returns>
        bool Reconfigure(KeywordSpottingConfiguration keywordFile);

        /// <summary>
        /// Starts listening for keywords
        /// </summary>
        /// <returns></returns>
        bool Start();

        /// <summary>
        /// Stops listening for keywords
        /// </summary>
        /// <returns></returns>
        bool Stop();
        
        /// <summary>
        /// Processes audio input and sends it to the keyword spotter.
        /// If a keyword is detected, a non-null hypothesis string will be returned
        /// </summary>
        /// <param name="samples"></param>
        /// <param name="numSamples"></param>
        string ProcessForKws(short[] samples, int numSamples);
    }
}
