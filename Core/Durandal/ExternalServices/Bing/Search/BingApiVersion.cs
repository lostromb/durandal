using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Bing.Search
{
    public enum BingApiVersion
    {
        /// <summary>
        /// Search API v5 via Cognitive Services
        /// </summary>
        V5,

        /// <summary>
        /// Search API v7 via Cognitive Services
        /// </summary>
        V7,

        /// <summary>
        /// Search API v7 via internal bingapis
        /// </summary>
        V7Internal
    }
}
