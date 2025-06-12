using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wayfinder
{
    public abstract class ColumnDefinition
    {
        private string _name;

        public ColumnDefinition(string name)
        {
            _name = name;
        }

        public string GetName()
        {
            return _name;
        }

        public abstract string GetValue(JToken obj);
    }
}
