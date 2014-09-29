using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.Threading;

namespace LineFollower
{
    public class Program
    {
        public static void Main()
        {
            var state = false;
            var led = new OutputPort((Cpu.Pin)GHIElectronics.NETMF.FEZ.FEZ_Pin.Digital.LED, state);

            var app = new LineFollower();
            app.Run();

            while (true)
            {
                state = !state;
                led.Write(state);
                Thread.Sleep(1000);
            }
        }
    }
}
