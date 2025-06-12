using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Utils.NativePlatform
{
    /// <summary>
    /// Defines a class which can statically accelerate some part of this program, presumably by using
    /// a native code adapter layer. The acceleration can be enabled or disabled at will.
    /// The simplest way to use these is to call <see cref="AssemblyReflector.ApplyAccelerators(System.Reflection.Assembly, ILogger)"/>
    /// on an entire assembly at once at program startup.
    /// </summary>
    public interface IAccelerator
    {
        /// <summary>
        /// Applies this current accelerator to the running code.
        /// </summary>
        /// <param name="logger">A logger</param>
        /// <returns>True if the application had an effect</returns>
        bool Apply(ILogger logger);

        /// <summary>
        /// Unapplies the current accelerator from the running code.
        /// </summary>
        /// <param name="logger">A logger</param>
        void Unapply(ILogger logger);
    }
}
