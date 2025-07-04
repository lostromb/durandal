
//------------------------------------------------------------------------------
// This code was generated by a tool.
//
//   Tool : Bond Compiler 0.12.1.0
//   Input filename:  .\Durandal.BondProtocol.API.TriggerResult.bond
//   Output filename: Durandal.BondProtocol.API.TriggerResult_types.cs
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

namespace Durandal.Extensions.BondProtocol.API
{
    using System.Collections.Generic;

    [global::Bond.Schema]
    [System.CodeDom.Compiler.GeneratedCode("gbc", "0.12.1.0")]
    public partial class TriggerResult
    {
        [global::Bond.Id(1), global::Bond.Required]
        public BoostingOption BoostResult { get; set; }

        [global::Bond.Id(2), global::Bond.Required]
        public string ActionName { get; set; }

        [global::Bond.Id(3)]
        public string ActionNameSsml { get; set; }

        [global::Bond.Id(4)]
        public string ActionDescription { get; set; }

        [global::Bond.Id(5)]
        public List<LexicalString> ActionKnownAs { get; set; }

        public TriggerResult()
            : this("Durandal.Extensions.BondProtocol.API.TriggerResult", "TriggerResult")
        {}

        protected TriggerResult(string fullName, string name)
        {
            BoostResult = BoostingOption.NoChange;
            ActionName = "";
            ActionNameSsml = "";
            ActionDescription = "";
        }
    }
} // Durandal.Extensions.BondProtocol.API
