
//------------------------------------------------------------------------------
// This code was generated by a tool.
//
//   Tool : Bond Compiler 0.10.0.0
//   File : Durandal.API.CachedWebData_types.cs
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
    using System;
    using System.Collections.Generic;

    public class CachedWebData
    {
        /// <summary>
        /// Data to be cached - can be HTML, CSS, an image, or whatever
        /// </summary>
        [JsonConverter(typeof(JsonByteArrayConverter))]
        public System.ArraySegment<byte> Data { get; set; }

        /// <summary>
        /// The mime type of the data
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// The trace ID of the activity that produced this item
        /// </summary>
        public Guid? TraceId { get; set; }

        public int LifetimeSeconds { get; set; }
        
        public CachedWebData()
            : this(BinaryHelpers.EMPTY_BYTE_ARRAY, string.Empty, null)
        {
        }

        public CachedWebData(byte[] data, string mimeType, Guid? traceId = null)
            : this(new ArraySegment<byte>(data), mimeType, traceId)
        {
        }

        public CachedWebData(ArraySegment<byte> data, string mimeType, Guid? traceId = null)
        {
            Data = data;
            MimeType = mimeType;
            TraceId = null;
            LifetimeSeconds = 0;
        }
    }
} // Durandal.API
