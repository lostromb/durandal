using Durandal.Common.Client;
using Durandal.CommonViews;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Client
{
    public class ClientSideHtmlRenderer : IClientHtmlRenderer
    {
        public string RenderErrorMessage(string text)
        {
            ErrorPage view = new ErrorPage()
            {
                ErrorDetails = text,
                RequestData = null // todo: this should specify theme data defined by the client
            };

            return view.Render();
        }

        public string RenderMessage(string text)
        {
            MessageView view = new MessageView()
            {
                Content = text,
                UseHtml5 = true
            };

            return view.Render();
        }
    }
}
