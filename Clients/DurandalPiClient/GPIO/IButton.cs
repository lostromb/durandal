using System;

namespace DurandalPiClient
{
    public interface IButton : IDisposable
    {
        event EventHandler ButtonPressed;
        event EventHandler ButtonReleased;
    }
}

