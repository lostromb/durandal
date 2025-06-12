using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Answers.SmartThingsAnswer.Devices
{
    public class UserDeviceCollection
    {
        public Dictionary<string, SmartDevice> Devices { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }
}
