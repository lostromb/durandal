

namespace Durandal.Common.Time.Timex.Actions.Interpreter
{
    using System;
    using System.Collections.Generic;

    using Durandal.Common.Time.Timex.Resources;

    /// <summary>
    /// Represents a set of compiled statements that are executed by interpreting a tag script.
    /// </summary>
    public class InterpretedScript
    {
        /// <summary>
        /// The static dictionary of normalization resources available in the grammar
        /// </summary>
        private IDictionary<string, NormalizationResource> _normalizationResources = new Dictionary<string, NormalizationResource>();
        
        /// <summary>
        /// The actual list of interpreter statements to execute
        /// </summary>
        private readonly IList<IStatement> _statements = new List<IStatement>(); 

        /// <summary>
        /// Creates a script from a list of imperative statements
        /// </summary>
        /// <param name="statements"></param>
        private InterpretedScript(IList<IStatement> statements)
        {
            _statements = statements;
        }

        /// <summary>
        /// This is the "compile" part of the script, where we supply the static normalization dictionary that is used by the functors
        /// </summary>
        /// <param name="normalizationResources"></param>
        public void SetNormalizationResources(IDictionary<string, NormalizationResource> normalizationResources)
        {
            _normalizationResources = normalizationResources;
        }
        
        public void Evaluate(IDictionary<string, string> timexDict, string inputValue)
        {
            foreach (var statement in _statements)
            {
                statement.Evaluate(_normalizationResources, timexDict, inputValue);
            }
        }

        /// <summary>
        /// Attempts to parse an executable script from a C# code snippet.
        /// If parsing fails, this still returns a value, but it will contain no executable statements.
        /// </summary>
        /// <param name="codeString"></param>
        /// <param name="ruleId">The rule that contains this script, to make debugging easier</param>
        /// <returns></returns>
        public static InterpretedScript Parse(string codeString, string ruleId)
        {
            IList<IStatement> parsedStatements = ScriptParser.ParseStatements(codeString, ruleId);
            return new InterpretedScript(parsedStatements);
        }
    }
}
