using Durandal.API;
using Durandal.API.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Answers.Botlets
{
    public static class BotletHelpers
    {
        public static T TryParseEntityFromSlot<T>(RecoResult queryResult, string slotName) where T : class
        {
            SlotValue resultSlot = DialogHelpers.TryGetSlot(queryResult, slotName);
            if (resultSlot == null)
            {
                return null;
            }
            try
            {
                T returnVal = JsonConvert.DeserializeObject<T>(resultSlot.Value);
                return returnVal;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static SlotValue BuildSlotFromEntity<T>(string slotName, T obj) where T : class
        {
            string json = JsonConvert.SerializeObject(obj);
            SlotValue returnVal = new SlotValue(slotName, json, SlotValueFormat.CrossDomainTag);
            return returnVal;
        }
    }
}
