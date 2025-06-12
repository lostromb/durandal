using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// A very rudimentary command line argument parser.
    /// </summary>
    public static class CommandLineParser
    {
        private static readonly Regex ARG_NAME_PARSER = new Regex("^(\\/|-|--)(\\w+)$");

        /// <summary>
        /// Parses command line arguments.
        /// Accepts args like "/option Value /option2" and returns a dictionary.
        /// The keys of the dictionary are the command line parameters, and the values are lists containing all of the values (zero or more) that were specified for those parameters.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static IDictionary<string, List<string>> ParseArgs(string[] args)
        {
            Dictionary<string, List<string>> returnVal = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // TODO alter this method to handle "/arg=value" formulations

            string currentArg = null;
            foreach (string arg in args)
            {
                Match match = ARG_NAME_PARSER.Match(arg);
                if (match.Success)
                {
                    // It's an arg name. Trim it and make sure it's in the dictionary
                    currentArg = match.Groups[2].Value;
                    if (!returnVal.ContainsKey(currentArg))
                    {
                        returnVal.Add(currentArg, new List<string>());
                    }
                }
                else
                {
                    string sanitizedArg = arg.Trim('\"');
                    if (currentArg == null)
                    {
                        throw new Exception("Argument value specified without a name");
                    }
                    else
                    {
                        returnVal[currentArg].Add(sanitizedArg);
                    }
                }
            }

            return returnVal;
        }
    }
}
