using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Remoting
{
    public class ProcessContainerGuestInitializationParams : ContainerGuestInitializationParameters
    {
        /// <summary>
        /// The PID of the container host process, so that child processes can monitor its lifetime and self-close
        /// if their parent closes.
        /// </summary>
        public int ParentProcessId { get; set; }
    }
}
