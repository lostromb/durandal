using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Answers.SmartThingsAnswer.Devices
{
    [Flags]
    public enum DeviceCapability
    {
        None = 0x0,
        Switch = 0x1 << 1,
        SwitchLevel = 0x1 << 2,
        ColorControl = 0x1 << 3,
    }
}
