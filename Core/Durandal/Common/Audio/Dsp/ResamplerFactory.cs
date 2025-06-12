using Durandal.Common.Audio.Codecs.Opus.Common;
using Durandal.Common.Logger;
using System;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Centralized location for creating <see cref="IResampler"/> implementations.
    /// </summary>
    public static class ResamplerFactory
    {
        public delegate IResampler ResamplerFactoryImpl(int numChannels, int inRate, int outRate, AudioProcessingQuality quality, ILogger logger);

        private static ResamplerFactoryImpl _globalFactory = DefaultFactoryImpl;

        /// <summary>
        /// Used by accelerators to inject a faster platform-specific resampler implementation.
        /// </summary>
        /// <param name="newFactoryImpl">The new factory implementation, or null to revert to the default factory implementation.</param>
        public static void SetGlobalFactory(ResamplerFactoryImpl newFactoryImpl)
        {
            _globalFactory = newFactoryImpl ?? DefaultFactoryImpl;
        }

        /// <summary>
        /// Creates a new audio resampler.
        /// </summary>
        /// <param name="numChannels">The number of interleaved channels the resampler should process.</param>
        /// <param name="inRate">The input frequency in hertz.</param>
        /// <param name="outRate">The output frequency in hertz.</param>
        /// <param name="quality">A quality hint for the resampler.</param>
        /// <param name="logger">A logger.</param>
        /// <returns>A newly created resampler.</returns>
        public static IResampler Create(int numChannels, int inRate, int outRate, AudioProcessingQuality quality, ILogger logger)
        {
            return _globalFactory(numChannels, inRate, outRate, quality, logger);
        }

        /// <inheritdoc/>
        internal static IResampler DefaultFactoryImpl(int numChannels, int inRate, int outRate, AudioProcessingQuality quality, ILogger logger)
        {
            return new SpeexResampler(numChannels, inRate, outRate, SpeexResampler.ConvertEnumQualityToInteger(quality));
        }
    }
}
