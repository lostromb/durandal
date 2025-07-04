﻿
//------------------------------------------------------------------------------
// This code was generated by a tool.
//
//   Tool : Bond Compiler 0.10.1.0
//   File : Durandal.Extensions.BondProtocol.API.SpeechRecognizedPhrase_types.cs
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
    using Durandal.Common.IO.Json;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;

    public class SpeechRecognizedPhrase
    {
        public string DisplayText { get; set; }
        
        public string IPASyllables { get; set; }

        public string LexicalForm { get; set; }
        
        public float SREngineConfidence { get; set; }
        
        public List<SpeechPhraseElement> PhraseElements { get; set; }
        
        public string Locale { get; set; }

        [JsonConverter(typeof(JsonTimeSpanTicksConverter))]
        public TimeSpan? AudioTimeOffset { get; set; }

        [JsonConverter(typeof(JsonTimeSpanTicksConverter))]
        public TimeSpan? AudioTimeLength { get; set; }
        
        public List<string> InverseTextNormalizationResults { get; set; }
        
        public List<Tag> ProfanityTags { get; set; }
        
        public List<string> MaskedInverseTextNormalizationResults { get; set; }

        public SpeechRecognizedPhrase()
        {
            DisplayText = string.Empty;
            IPASyllables = string.Empty;
            LexicalForm = string.Empty;
            PhraseElements = new List<SpeechPhraseElement>();
            Locale = string.Empty;
            InverseTextNormalizationResults = new List<string>();
            ProfanityTags = new List<Tag>();
            MaskedInverseTextNormalizationResults = new List<string>();
        }
    }
} // Durandal.API
