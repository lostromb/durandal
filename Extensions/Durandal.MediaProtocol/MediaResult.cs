using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// The abstract response that is coupled with a single MediaCommand
    /// </summary>
    public abstract class MediaResult
    {
        public abstract string Result { get; }
    }
}
