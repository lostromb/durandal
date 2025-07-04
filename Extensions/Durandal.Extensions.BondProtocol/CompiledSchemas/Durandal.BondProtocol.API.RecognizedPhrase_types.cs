
//------------------------------------------------------------------------------
// This code was generated by a tool.
//
//   Tool : Bond Compiler 0.12.1.0
//   Input filename:  .\Durandal.BondProtocol.API.RecognizedPhrase.bond
//   Output filename: Durandal.BondProtocol.API.RecognizedPhrase_types.cs
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
    public partial class RecognizedPhrase
    {
        [global::Bond.Id(1), global::Bond.Required]
        public string Utterance { get; set; }

        [global::Bond.Id(2), global::Bond.Required]
        public List<RecoResult> Recognition { get; set; }

        [global::Bond.Id(3)]
        public Dictionary<string, float> Sentiments { get; set; }

        [global::Bond.Id(4)]
        public System.ArraySegment<byte> EntityContext { get; set; }

        public RecognizedPhrase()
            : this("Durandal.Extensions.BondProtocol.API.RecognizedPhrase", "RecognizedPhrase")
        {}

        protected RecognizedPhrase(string fullName, string name)
        {
            Utterance = "";
            Recognition = new List<RecoResult>();
            Sentiments = new Dictionary<string, float>();
            EntityContext = new System.ArraySegment<byte>();
        }
    }
} // Durandal.Extensions.BondProtocol.API
