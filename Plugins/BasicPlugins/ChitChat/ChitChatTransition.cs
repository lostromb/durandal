using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.SideSpeech
{
    public class ChitChatTransition
    {
        public ChitChatTransition(string start, string intent, string target)
        {
            StartNode = start;
            Intent = intent;
            TargetNode = target;
        }

        public string StartNode;
        public string Intent;
        public string TargetNode;
    }
}
