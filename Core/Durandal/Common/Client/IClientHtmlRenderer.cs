using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Client
{
    public interface IClientHtmlRenderer
    {
        string RenderMessage(string text);
        string RenderErrorMessage(string text);
    }
}
