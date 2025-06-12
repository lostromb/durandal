using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DurandalPiClient
{
    public class DummyGPIO : IButton
    {
        public event EventHandler ButtonPressed;
        public event EventHandler ButtonReleased;

        public void Dispose()
        {
        }
    }
}
