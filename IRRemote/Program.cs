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

        private static void interrupted(uint data1, uint data2, DateTime time)
        {
            if (microsecondsSinceLastEdge > 1500)
            {
                count = 0;
            }
            Debug.Print(count.ToString() + ": " + microsecondsSinceLastEdge + "(" + data1.ToString() + "/" + data2.ToString() + ")");
            if (count == 0)
            {
                count = 1;
                lastTime = DateTime.Now.Ticks;
                commandValueLow = 0;
                commandValueHigh = 0;
                microsecondsSinceLastEdge = 0;
            } 
            else if (count < 24 && count % 2 == 0)
            {
                count++;
                lastTime = time.Ticks;
            } else if (count > 0 && count <= 11)
            {
                microsecondsSinceLastEdge = (time.Ticks - lastTime) / 1000;
                commandValueLow <<= 1;
                if (microsecondsSinceLastEdge <= 8)
                {
                    commandValueLow &= 0xFE;
                }
                else
                {
                    commandValueLow |= 1;
                }
                count++;
            }
            else if (count > 11 && count <= 23)
            {
                microsecondsSinceLastEdge = (time.Ticks - lastTime) / 1000;
                commandValueHigh <<= 1;
                if (microsecondsSinceLastEdge <= 8)
                {
                    commandValueHigh &= 0xFE;
                }
                else
                {
                    commandValueHigh |= 1;
                }
                count++;
            }

            if (count == 24)
            {
                Debug.Print(count.ToString() + "Command Value: " + (commandValueHigh *256 + commandValueLow).ToString());
                count = 0;
                return;
            }
            else
            {
                lastTime = time.Ticks;
            }
        }

    }
}
