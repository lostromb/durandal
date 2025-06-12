using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using static SDL2.SDL;

namespace Durandal.Extensions.SDL
{
    /// <summary>
    /// Manages global SDL state, mostly to ensure it only gets initialized once.
    /// </summary>
    internal class GlobalSDLState
    {
        private static int _initialized = 0;

        public static void Initialize()
        {
            if (!AtomicOperations.ExecuteOnce(ref _initialized))
            {
                return;
            }

            if (SDL_Init(SDL_INIT_AUDIO) != 0)
            {
                throw new Exception($"SDL2 initialization failed with error \"{SDL_GetError()}\"");
            }
        }
    }
}
