using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Speech.Triggers.Sphinx
{
    public static class SphinxHelpers
    {
        /// <summary>
        /// Accepts a keyword spotting configuration and outputs a single string representing a Sphinx-formatted keyword spotting config file
        /// </summary>
        /// <param name="config"></param>
        /// <param name="traceLogger">A logger for errors</param>
        /// <returns></returns>
        public static string CreateKeywordFile(KeywordSpottingConfiguration config, ILogger traceLogger)
        {
            StringBuilder builder = new StringBuilder();
            if (!string.IsNullOrEmpty(config.PrimaryKeyword) && config.PrimaryKeyword.Length < 100)
            {
                builder.Append(string.Format("{0}/{1}/\n", config.PrimaryKeyword.ToUpperInvariant(), LinToLog(config.PrimaryKeywordSensitivity).ToString().ToLowerInvariant()));
            }
            else
            {
                traceLogger.Log("Primary keyphrase must be non-empty and shorter than 100 characters; ignoring", LogLevel.Wrn);
            }

            if (config.SecondaryKeywords != null)
            {
                foreach (string secondaryKeyword in config.SecondaryKeywords)
                {
                    if (!string.IsNullOrEmpty(secondaryKeyword) && secondaryKeyword.Length < 100)
                    {
                        builder.Append(string.Format("{0}/{1}/\n", secondaryKeyword.ToUpperInvariant(), LinToLog(config.SecondaryKeywordSensitivity).ToString().ToLowerInvariant()));
                    }
                    else
                    {
                        traceLogger.Log("Secondary keyphrase must be non-empty and shorter than 100 characters; ignoring", LogLevel.Wrn);
                    }
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Converts a Durandal config "sensitivity" (0 - 10) into a Sphinx log threshold
        /// </summary>
        /// <param name="configThreshold"></param>
        /// <returns></returns>
        public static double LinToLog(double configThreshold)
        {
            return Math.Pow(10, (configThreshold * -2.5));
        }

        /// <summary>
        /// Converts a Sphinx log threshold into a Durandal "sensitivity" value (0 - 10)
        /// </summary>
        /// <param name="sphinxThreshold"></param>
        /// <returns></returns>
        public static double LogToLin(double sphinxThreshold)
        {
            return Math.Log10(sphinxThreshold) / -2.5;
        }
    }
}
