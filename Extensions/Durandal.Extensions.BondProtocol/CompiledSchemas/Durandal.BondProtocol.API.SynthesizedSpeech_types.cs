
//------------------------------------------------------------------------------
// This code was generated by a tool.
//
//   Tool : Bond Compiler 0.12.1.0
//   Input filename:  .\Durandal.BondProtocol.API.SynthesizedSpeech.bond
//   Output filename: Durandal.BondProtocol.API.SynthesizedSpeech_types.cs
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
    public partial class SynthesizedSpeech
    {
        [global::Bond.Id(1), global::Bond.Required]
        public AudioData Audio { get; set; }

        [global::Bond.Id(2), global::Bond.Type(typeof(global::Bond.Tag.nullable<string>))]
        public string Locale { get; set; }

        [global::Bond.Id(3), global::Bond.Type(typeof(global::Bond.Tag.nullable<string>))]
        public string Ssml { get; set; }

        [global::Bond.Id(4), global::Bond.Type(typeof(global::Bond.Tag.nullable<string>))]
        public string PlainText { get; set; }

        [global::Bond.Id(5), global::Bond.Type(typeof(global::Bond.Tag.nullable<List<SynthesizedWord>>))]
        public List<SynthesizedWord> Words { get; set; }

        public SynthesizedSpeech()
            : this("Durandal.Extensions.BondProtocol.API.SynthesizedSpeech", "SynthesizedSpeech")
        {}

        protected SynthesizedSpeech(string fullName, string name)
        {
            Audio = new AudioData();
        }
    }
} // Durandal.Extensions.BondProtocol.API
