using Durandal.Common.Client.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Reflection
{
    /// <summary>
    /// Schema for the ClearPrivateData JSON action.
    /// This triggers GDPR deletion by the client.
    /// </summary>
    public class ClearPrivateDataAction : IJsonClientAction
    {
        public static readonly string ActionName = "ClearPrivateData";

        public string Name
        {
            get
            {
                return ActionName;
            }
        }
    }
}
