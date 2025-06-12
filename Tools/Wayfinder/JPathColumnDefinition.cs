using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wayfinder
{
    public class JPathColumnDefinition : ColumnDefinition
    {
        private string _jpath;

        public JPathColumnDefinition(string name, string jpath) : base(name)
        {
            _jpath = jpath;
        }

        public override string GetValue(JToken obj)
        {
            return TryExtractJPathValue(obj, _jpath);
        }

        private static string TryExtractJPathValue(JToken obj, string path)
        {
            JToken response = obj.SelectToken(path);
            if (response == null)
                return string.Empty;
            string returnVal = response.Value<string>();
            return returnVal.Replace("\t", "\\t");
        }
    }
}
