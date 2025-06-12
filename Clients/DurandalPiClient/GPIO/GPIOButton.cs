#if RPI

using System;
using Raspberry.IO.GeneralPurpose;

namespace DurandalPiClient
{
    public class GPIOButton : IButton
    {
        private GpioConnection _connection;

        public GPIOButton(GpioConnection connection)
        {
			_connection = connection;
			_connection.PinStatusChanged += PinEventHandler;
        }

        public void PinEventHandler(object source, PinStatusEventArgs args)
        {
            if (args.Enabled && ButtonPressed != null)
            {
                ButtonPressed(this, new EventArgs());
            }
            if (!args.Enabled && ButtonReleased != null)
            {
                ButtonReleased(this, new EventArgs());
            }
        }

        public void Dispose()
        {
            _connection.Close();
        }

        public event EventHandler ButtonPressed;
        public event EventHandler ButtonReleased;

        public static GPIOButton Create(ConnectorPin pin)
        {
            try
            {
				InputPinConfiguration switchConfig = pin.Input();
                switchConfig.Resistor = PinResistor.PullUp;
				switchConfig.Reversed = true;
                GpioConnection connection = new GpioConnection(switchConfig);
                return new GPIOButton(connection);
            }
            catch (Exception e)
            {
                Console.WriteLine("GPIO error: " + e.Message);
                return null;
            }
        }
    }
}

#endif