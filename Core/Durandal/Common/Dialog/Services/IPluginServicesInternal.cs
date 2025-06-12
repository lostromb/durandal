using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Dialog.Services
{
    /// <summary>
    /// Plugin services used internally in execution framework, containing more data than what is exposed to plugins.
    /// </summary>
    public interface IPluginServicesInternal : IPluginServices
    {
        InMemoryDialogActionCache DialogActionCache { get; }
        InMemoryWebDataCache WebDataCache { get; }
    }
}
