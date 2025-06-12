using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Logger;
using Durandal.Common.Utils.NativePlatform;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Extensions.NativeAudio.Codecs
{
    /// <summary>
    /// Accelerates <see cref="OpusCodecFactory" /> using native C binaries
    /// </summary>
    public class NativeOpusAccelerator : IAccelerator
    {
        /// <inheritdoc />
        public bool Apply(ILogger logger)
        {
            try
            {
                if (NativeOpus.Initialize(logger))
                {
                    logger.Log("Accelerating Opus using native code adapter", LogLevel.Std);
                    OpusCodecFactory.SetGlobalFactory(new NativeOpusCodecFactory());
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
            OpusCodecFactory.SetGlobalFactory(null);
        }
    }
}
