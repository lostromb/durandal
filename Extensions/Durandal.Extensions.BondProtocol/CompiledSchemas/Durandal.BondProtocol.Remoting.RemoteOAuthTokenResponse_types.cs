
//------------------------------------------------------------------------------
// This code was generated by a tool.
//
//   Tool : Bond Compiler 0.12.1.0
//   Input filename:  .\Durandal.BondProtocol.Remoting.RemoteOAuthTokenResponse.bond
//   Output filename: Durandal.BondProtocol.Remoting.RemoteOAuthTokenResponse_types.cs
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
    public partial class RemoteOAuthTokenResponse
        : RemoteProcedureResponse
    {
        [global::Bond.Id(4), global::Bond.Type(typeof(global::Bond.Tag.nullable<global::Durandal.Extensions.BondProtocol.API.OAuthToken>))]
        public global::Durandal.Extensions.BondProtocol.API.OAuthToken ReturnVal { get; set; }

        public RemoteOAuthTokenResponse()
            : this("Durandal.Extensions.BondProtocol.Remoting.RemoteOAuthTokenResponse", "RemoteOAuthTokenResponse")
        {}

        protected RemoteOAuthTokenResponse(string fullName, string name)
        {
            
        }
    }
} // Durandal.Extensions.BondProtocol.Remoting
