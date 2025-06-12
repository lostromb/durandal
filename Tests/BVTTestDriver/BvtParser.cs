

namespace BVTTestDriver
{
    using Durandal.Common.NLP.Train;
    using Durandal.Common.File;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Durandal.Common.NLP.Tagging;

    public static class BvtParser
    {
        public static IList<IList<TestUtterance>> ParseBvtFile(VirtualPath fileName, IFileSystem fileSystem)
        {
            IEnumerable<string> lines = fileSystem.ReadLines(fileName);
            IList<IList<TestUtterance>> returnVal = new List<IList<TestUtterance>>();
            IList<TestUtterance> currentConversation = null;
            foreach (string line in lines)
            {
                string[] parts = line.Split('\t');
                if (parts.Length != 3 || !parts[2].Contains('/'))
                {
                    Console.Error.WriteLine("Malformed line in " + fileName + ": " + line);
                    continue;
                }
                TestUtterance newUtterance = new TestUtterance();
                newUtterance.Id = int.Parse(parts[0]);
                newUtterance.Input = TaggedDataSplitter.StripTags(parts[1]);
                newUtterance.TaggedInput = parts[1];
                newUtterance.ExpectedDomain = parts[2].Substring(0, parts[2].IndexOf('/'));
                newUtterance.ExpectedIntent = parts[2].Substring(parts[2].IndexOf('/') + 1);

                if (newUtterance.Id == 0)
                {
                    // Start a new conversation
                    if (currentConversation != null)
                    {
                        returnVal.Add(currentConversation);
                    }
                    currentConversation = new List<TestUtterance>();
                }
                currentConversation.Add(newUtterance);
            }
            return returnVal;
        }
    }
}
