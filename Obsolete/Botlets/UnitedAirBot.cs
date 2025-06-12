using Durandal.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Answers.Botlets
{
    public class UnitedAirBot : Answer
    {
        public UnitedAirBot() : base("united_air") { }

        protected override ConversationTree BuildConversationTree(ConversationTree tree)
        {
            return tree;
        }


    }
}
