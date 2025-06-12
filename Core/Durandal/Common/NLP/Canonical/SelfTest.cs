using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Durandal.Common.NLP.Canonical
{
    public static class SelfTest
    {
        public static void Main(string[] args)
        {
            /*Grammar grammar = new Grammar(@"..\..\ordinals_new.xml");
            bool pass = true;
            pass &= CountMatches(grammar, "1", 1);
            pass &= CountMatches(grammar, "5", 1);
            pass &= CountMatches(grammar, "number 5", 1);
            pass &= CountMatches(grammar, "number six", 1);
            pass &= CountMatches(grammar, "the sixth one", 1);
            pass &= CountMatches(grammar, "first", 1);
            pass &= CountMatches(grammar, "last", 1);
            pass &= CountMatches(grammar, "the first one", 1);
            pass &= CountMatches(grammar, "the bottom entry", 1);

            pass &= CountMatches(grammar, "blast", 0);
            pass &= CountMatches(grammar, "lastly", 0);
            pass &= CountMatches(grammar, "1st avenue", 0);
            pass &= CountMatches(grammar, "first avenue", 0);*/
        }

        public static bool CountMatches(Grammar grammar, string input, int expectedCount)
        {
            IList<GrammarMatch> matches = grammar.Matches(input, NullLogger.Singleton);
            if (matches.Count != expectedCount)
            {
                Debug.WriteLine("Failed on " + input);
                return false;
            }

            return true;
        }
    }
}
