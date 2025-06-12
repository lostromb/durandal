namespace Durandal.Extensions.NativeAudio
{
    using Durandal.Common.Audio;
    using Durandal.Common.Logger;
    using Durandal.Common.Utils.NativePlatform;
    using System;

    /// <summary>
    /// Accelerates <see cref="ResamplerFactory" /> using a native library layer.
    /// </summary>
    public class NativeSpeexResamplerAccelerator : IAccelerator
    {
        /// <inheritdoc />
        public bool Apply(ILogger logger)
        {
            try
            {
                if (NativeSpeexResampler.Initialize(logger))
                {
                    logger.Log("Accelerating IResampler using native code adapter", LogLevel.Std);
                    ResamplerFactory.SetGlobalFactory(NativeSpeexResampler.Create);
                    return true;
                }
            }
            catch (Exception e)
            {
                logger.Log(e);
            }

            return false;
        }

        /// <inheritdoc />
        public void Unapply(ILogger logger)
        {
            ResamplerFactory.SetGlobalFactory(null);
        }
    }
}
