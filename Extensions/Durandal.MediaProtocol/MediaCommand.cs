using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// A single abstract command sent to a media service
    /// </summary>
    public abstract class MediaCommand
    {
        public abstract string Action { get; }
    }
}
