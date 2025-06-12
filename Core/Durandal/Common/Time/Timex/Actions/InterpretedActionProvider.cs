namespace Durandal.Common.Time.Timex.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    
    using Durandal.Common.Time.Timex.Actions.Interpreter;
    using Durandal.Common.Time.Timex.Resources;

    /// <summary>
    /// This class allows you to execute tag actions (or tag scripts) without relying on any C# codegen or reflection.
    /// This makes the script language more limited, but allows this code to be ported and run
    /// in almost any .Net runtime.
    /// </summary>
    public class InterpretedActionProvider : IActionProvider
    {
        private IDictionary<string, InterpretedScript> _scripts = new Dictionary<string, InterpretedScript>();

        private bool _compiled = false;

        public void AppendMethod(string ruleId, string tagKey, string codeString)
        {
            string key = ruleId + ":" + tagKey;
            if (_scripts.ContainsKey(key))
            {
                // Error: script already exists
                throw new TimexException("Script with key " + key + " already exists");
            }

            InterpretedScript script = InterpretedScript.Parse(codeString, key);
            if (script == null)
            {
                // Error: parse failure
                throw new TimexException("Could not parse tag script: " + codeString);
            }

            _scripts[key] = script;
        }

        public void Compile(IDictionary<string, NormalizationResource> normalizationResources)
        {
            if (normalizationResources == null)
            {
                throw new ArgumentNullException("normalizationResources");
            }
            
            foreach (var script in _scripts.Values)
            {
                script.SetNormalizationResources(normalizationResources);
            }

            _compiled = true;
        }

        public TagAction GetMethod(string ruleId, string tagKey)
        {
            if (!_compiled)
            {
                throw new InvalidOperationException("You must compile the tagscript assembly before you can use its methods!");
            }
            
            string key = ruleId + ":" + tagKey;

            InterpretedScript targetScript;
            if (_scripts.TryGetValue(key, out targetScript))
            {
                return targetScript.Evaluate;
            }

            throw new TimexException("No compiled script found for " + key);
        }
    }
}
