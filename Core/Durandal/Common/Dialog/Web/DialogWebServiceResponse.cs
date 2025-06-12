using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog.Web
{
    public class DialogWebServiceResponse
    {
        public DialogResponse ClientResponse { get; set; }
        public AudioEncoder OutputAudioStream { get; set; }

        public DialogWebServiceResponse(DialogResponse response, AudioEncoder audioOutStream = null)
        {
            ClientResponse = response;
            OutputAudioStream = audioOutStream;
        }
    }
}
