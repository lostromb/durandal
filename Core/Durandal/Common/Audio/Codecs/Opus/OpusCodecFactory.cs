using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Codecs.Opus
{
    /// <summary>
    /// Centralized location for creating opus encoder / decoder implementations.
    /// </summary>
    public static class OpusCodecFactory
    {
        private static IOpusCodecFactory _globalFactory = new ManagedOpusCodecFactory();

        /// <summary>
        /// Used by accelerators to inject a faster platform-specific resampler implementation.
        /// </summary>
        /// <param name="newFactoryImpl">The new factory implementation, or null to revert to the default factory implementation.</param>
        public static void SetGlobalFactory(IOpusCodecFactory newFactoryImpl)
        {
            _globalFactory = newFactoryImpl ?? new ManagedOpusCodecFactory();
        }

        public static IOpusCodecFactory Provider
        {
            get
            {
                return _globalFactory;
            }
        }
    }
}
