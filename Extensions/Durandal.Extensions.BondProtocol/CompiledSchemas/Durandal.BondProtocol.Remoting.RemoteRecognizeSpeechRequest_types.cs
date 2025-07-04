
//------------------------------------------------------------------------------
// This code was generated by a tool.
//
//   Tool : Bond Compiler 0.12.1.0
//   Input filename:  .\Durandal.BondProtocol.Remoting.RemoteRecognizeSpeechRequest.bond
//   Output filename: Durandal.BondProtocol.Remoting.RemoteRecognizeSpeechRequest_types.cs
//
// Changes to this file may cause incorrect behavior and will be lost when
// the code is regenerated.
// <auto-generated />
//------------------------------------------------------------------------------


// suppress "Missing XML comment for publicly visible type or member"
#pragma warning disable 1591


#region ReSharper warnings
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable RedundantNameQualifier
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
// ReSharper disable UnusedParameter.Local
// ReSharper disable RedundantUsingDirective
#endregion

namespace Durandal.Extensions.BondProtocol.Remoting
{
    using System.Collections.Generic;

    [global::Bond.Schema]
    [System.CodeDom.Compiler.GeneratedCode("gbc", "0.12.1.0")]
    public partial class RemoteRecognizeSpeechRequest
        : RemoteProcedureRequest
    {
        [global::Bond.Id(3), global::Bond.Required]
        public string Locale { get; set; }

        [global::Bond.Id(4), global::Bond.Required]
        public global::Durandal.Extensions.BondProtocol.API.AudioData Audio { get; set; }

        public RemoteRecognizeSpeechRequest()
            : this("Durandal.Extensions.BondProtocol.Remoting.RemoteRecognizeSpeechRequest", "RemoteRecognizeSpeechRequest")
        {}

        protected RemoteRecognizeSpeechRequest(string fullName, string name)
        {
            Locale = "";
            Audio = new global::Durandal.Extensions.BondProtocol.API.AudioData();
        }
    }
} // Durandal.Extensions.BondProtocol.Remoting
