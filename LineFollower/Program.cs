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
            var app = new LineFollower();

            while (true)
            {
                app.Run();
            }
        }
    }
}
