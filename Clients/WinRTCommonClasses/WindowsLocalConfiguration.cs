using Durandal.Common.Config;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace DurandalWinRT
{
    public class WindowsLocalConfiguration : AbstractConfiguration
    {
        public WindowsLocalConfiguration(ILogger logger) : base(logger, DefaultRealTimeProvider.Singleton)
        {
            // Load what's already in local storage
            if (ApplicationData.Current != null &&
                ApplicationData.Current.LocalSettings != null)
            {
                IPropertySet appConfig = ApplicationData.Current.LocalSettings.Values;
                foreach (var item in appConfig)
                {
                    string valueField = (string)item.Value;
                    string[] parts = valueField.Split('\n');
                    RawConfigValue newValue = new RawConfigValue(item.Key, parts[0], (ConfigValueType)Enum.Parse(typeof(ConfigValueType), parts[1]));
                    Set(newValue);
                }
            }
        }

        protected override async Task CommitChanges(IRealTimeProvider realTime)
        {
            IPropertySet appConfig = ApplicationData.Current.LocalSettings.Values;
            int hLock = await _lock.EnterReadLockAsync();
            try
            {
                foreach (var item in _configValues)
                {
                    if (appConfig.ContainsKey(item.Key))
                    {
                        appConfig.Remove(item.Key);
                    }

                    appConfig.Add(item.Key, item.Value.RawValue + "\n" + item.Value.ValueType.ToString());
                }
            }
            finally
            {
                _lock.ExitReadLock(hLock);
            }
        }
    }
}
