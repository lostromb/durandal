
//------------------------------------------------------------------------------
// This code was generated by a tool.
//
//   Tool : Bond Compiler 0.12.1.0
//   Input filename:  .\Durandal.BondProtocol.API.SpeechPhraseElement.bond
//   Output filename: Durandal.BondProtocol.API.SpeechPhraseElement_types.cs
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
    public partial class SpeechPhraseElement
    {
        [global::Bond.Id(1)]
        public float SREngineConfidence { get; set; }

        [global::Bond.Id(2)]
        public string LexicalForm { get; set; }

        [global::Bond.Id(3)]
        public string DisplayText { get; set; }

        [global::Bond.Id(4)]
        public string Pronunciation { get; set; }

        [global::Bond.Id(5)]
        public uint AudioTimeOffset { get; set; }

        [global::Bond.Id(6)]
        public uint AudioTimeLength { get; set; }

        public SpeechPhraseElement()
            : this("Durandal.Extensions.BondProtocol.API.SpeechPhraseElement", "SpeechPhraseElement")
        {}

        protected SpeechPhraseElement(string fullName, string name)
        {
            LexicalForm = "";
            DisplayText = "";
            Pronunciation = "";
        }
    }
} // Durandal.Extensions.BondProtocol.API
