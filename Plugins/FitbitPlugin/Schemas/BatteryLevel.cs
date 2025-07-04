﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BatteryLevel
    {
        Unknown,
        High,
        Medium,
        Low,
        Empty
    }
}
