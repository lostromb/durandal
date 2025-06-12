//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace PokedexAnswer
{
    public class PokemonStatisticsView
    {
        private StringWriter Output;
        public string ContentPage {get; set;}
        public PokemonStatisticsView()
        {
        }
        public string Render()
        {
            StringBuilder returnVal = new StringBuilder();
            Output = new StringWriter(returnVal);
            RenderViewLevel0();
            return returnVal.ToString();
        }
        private void RenderViewLevel0()
        {
    #line hidden
            Output.Write("<!DOCTYPE html>\r\n<html lang=\"en\" dir=\"ltr\" class=\"client-nojs\">\r\n<head>\r\n<meta charset=\"UTF-8\"/>\r\n<link rel=\"stylesheet\" href=\"http://bulbapedia.bulbagarden.net/w/load.php?debug=false&amp;lang=en&amp;modules=mediawiki.legacy.commonPrint%2Cshared%7Cmediawiki.sectionAnchor%7Cmediawiki.skinning.content.externallinks%7Cmediawiki.skinning.interface%7Cmediawiki.ui.button%7Cskins.monobook.styles&amp;only=styles&amp;skin=monobook&amp;*\"/>\r\n<!--[if IE 6]><link rel=\"stylesheet\" href=\"/w/skins/MonoBook/IE60Fixes.css?303\" media=\"screen\" /><![endif]-->\r\n<!--[if IE 7]><link rel=\"stylesheet\" href=\"/w/skins/MonoBook/IE70Fixes.css?303\" media=\"screen\" /><![endif]-->\r\n<link rel=\"stylesheet\" href=\"http://bulbapedia.bulbagarden.net/w/load.php?debug=false&amp;lang=en&amp;modules=site&amp;only=styles&amp;skin=monobook&amp;*\"/>\r\n</head>\r\n<body style=\"background:#FFF;\">\r\n\r\n@ContentPage\r\n\r\n</body></html>\r\n");
    #line default
        }
    }
}
