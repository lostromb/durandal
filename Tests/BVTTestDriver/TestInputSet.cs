using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BVTTestDriver
{
    public class TestInputSet
    {
        public readonly List<IList<TestUtterance>> Conversations;

        public TestInputSet()
        {
            Conversations = new List<IList<TestUtterance>>();
        }

        public int NumConversations
        {
            get
            {
                return Conversations.Count;
            }
        }

        public int NumUtterances
        {
            get
            {
                int count = 0;
                foreach (var conversation in Conversations)
                {
                    count += conversation.Count;
                }
                return count;
            }
        }

        public void AddData(IList<IList<TestUtterance>> newData)
        {
            Conversations.AddRange(newData);
        }

        public void AddBvtFile(VirtualPath fileName, IFileSystem fileSystem)
        {
            Console.WriteLine("Processing " + fileName.FullName);
            
            // Determine the domain name
            string domain = fileName.Name.Substring(0, fileName.Name.LastIndexOf('.'));

            // Read the whole bvt file into memory
            IList<IList<TestUtterance>> utterances = BvtParser.ParseBvtFile(fileName, fileSystem);

            foreach (var testConversation in utterances)
            {
                Conversations.Add(testConversation);
            }
        }
    }
}
