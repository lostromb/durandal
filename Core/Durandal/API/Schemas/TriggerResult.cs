
//------------------------------------------------------------------------------
// This code was generated by a tool.
//
//   Tool : Bond Compiler 0.10.0.0
//   File : Durandal.API.TriggerResult_types.cs
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

namespace Durandal.API
{
    using System.Collections.Generic;

    public class TriggerResult
    {
        public BoostingOption BoostResult { get; set; }

        public string ActionName { get; set; }

        public string ActionNameSsml { get; set; }

        public string ActionDescription { get; set; }

        public List<LexicalString> ActionKnownAs { get; set; }
        
        public TriggerResult()
        {
            BoostResult = BoostingOption.NoChange;
            ActionName = "";
            ActionNameSsml = "";
            ActionDescription = "";
        }

        public TriggerResult(BoostingOption boost) : this()
        {
            BoostResult = boost;
        }
    }
} // Durandal.API
