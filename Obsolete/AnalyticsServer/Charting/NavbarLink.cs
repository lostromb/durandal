using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DurandalServices.Instrumentation.Analytics.Charting
{
    public class NavbarLink
    {
        public string Url;
        public string Text;

        public NavbarLink(string url, string text)
        {
            Url = url;
            Text = text;
        }
    }
}
