using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Answers.SmartThingsAnswer.Devices
{
    public class SmartDevice
    {
        public SmartDevice(string name, string id, DeviceCapability capabilities, IEnumerable<string> knownAs)
        {
            Name = name;
            Id = id;
            KnownAs = new List<string>(knownAs);
            Capabilities = capabilities;
        }
        
        public string Name
        {
            get;
            private set;
        }

        public string Id
        {
            get;
            private set;
        }

        public IList<string> KnownAs
        {
            get;
            private set;
        }

        public DeviceCapability Capabilities
        {
            get;
            private set;
        }

        public void AddCapability(DeviceCapability cap)
        {
            Capabilities |= cap;
        }

        #region Capability.Switch

        /// <summary>
        /// Turns the device on, if it has the Switch capability
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<bool> On(SmartThingsContext context)
        {
            if (Capabilities.HasFlag(DeviceCapability.Switch))
            {
                CommandResult result = await context.SendCommandAsync("PUT", "/switches/" + Id, "{ command: on }");
                return result.Success;
            }

            return false;
        }

        /// <summary>
        /// Turns the device off, if it has the Switch capability
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<bool> Off(SmartThingsContext context)
        {
            if (Capabilities.HasFlag(DeviceCapability.Switch))
            {
                CommandResult result = await context.SendCommandAsync("PUT", "/switches/" + Id, "{ command: off }");
                return result.Success;
            }

            return false;
        }

        private bool _stateSwitch = false;

        /// <summary>
        /// The current on/off state of the device, if it has the Switch capability.
        /// The Set operator on this does not actually send a command to change the state.
        /// </summary>
        [JsonIgnore]
        public bool StateSwitch
        {
            get
            {
                if (Capabilities.HasFlag(DeviceCapability.Switch))
                {
                    return _stateSwitch;
                }

                return false;
            }

            set
            {
                _stateSwitch = value;
            }
        }

        #endregion

        #region Capability.SwitchLevel

        public async Task<bool> SetLevel(int percentValue, SmartThingsContext context)
        {
            if (Capabilities.HasFlag(DeviceCapability.SwitchLevel))
            {
                CommandResult result = await context.SendCommandAsync("PUT", "/dimmers/" + Id, "{ value: " + percentValue + " }");
                return result.Success;
            }

            return false;
        }

        private float _stateLevel = 0;

        /// <summary>
        /// The current level of the device, if it has the SwitchLevel capability.
        /// The Set operator on this does not actually send a command to change the state.
        /// </summary>
        [JsonIgnore]
        public float StateLevel
        {
            get
            {
                if (Capabilities.HasFlag(DeviceCapability.SwitchLevel))
                {
                    return _stateLevel;
                }

                return 0;
            }
            set
            {
                _stateLevel = value;
            }
        }

        #endregion
    }
}
