using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;
using GHIElectronics.NETMF.FEZ;

namespace IRRemote
{
    public class Program
    {
        private static InterruptPort remotePin;
        private static long lastTime;
        private static int count;
        private static int commandValueHigh;
        private static int commandValueLow;
        private static long microsecondsSinceLastEdge;

        public static void Main()
        {
            remotePin = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di7, false, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeBoth);
            
            remotePin.OnInterrupt += interrupted;

            lastTime = DateTime.Now.Ticks;

            while (true)
            {
                Debug.Print(
                Resources.GetString(Resources.StringResources.String1));
                Thread.Sleep(5000); 
            }
        }
    }
}
