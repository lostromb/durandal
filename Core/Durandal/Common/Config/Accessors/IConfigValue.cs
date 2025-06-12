using Durandal.Common.Events;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Config.Accessors
{
    public interface IConfigValue<T> : IDisposable
    {
        /// <summary>
        /// Gets or sets the latest value in the configuration
        /// </summary>
        T Value { get; set; }

        /// <summary>
        /// An event which fires whenever this config value is changed, either from external file changes or
        /// programmatically setting the value
        /// </summary>
        AsyncEvent<ConfigValueChangedEventArgs<T>> ChangedEvent { get; }
    }
}
