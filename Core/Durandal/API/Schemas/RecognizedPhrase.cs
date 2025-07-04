
//------------------------------------------------------------------------------
// This code was generated by a tool.
//
//   Tool : Bond Compiler 0.10.0.0
//   File : Durandal.API.RecognizedPhrase_types.cs
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
    using Durandal.Common.Utils;
    using Durandal.Common.IO.Json;
    using Newtonsoft.Json;
    using System.Collections.Generic;

    public class RecognizedPhrase
    {
        public string Utterance { get; set; }

        public List<RecoResult> Recognition { get; set; }

        public Dictionary<string, float> Sentiments { get; set; }

        [JsonConverter(typeof(JsonByteArrayConverter))]
        public System.ArraySegment<byte> EntityContext { get; set; }
        
        public RecognizedPhrase()
        {
            Utterance = "";
            Recognition = new List<RecoResult>();
            Sentiments = new Dictionary<string, float>();
            EntityContext = new System.ArraySegment<byte>();
        }
    }
} // Durandal.API
