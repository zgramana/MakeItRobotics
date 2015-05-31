using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;
using GHIElectronics.NETMF.FEZ;

namespace MakeItRobotics
{
    public class Program
    {
        #region Constants

        const Int16 SW1 = 0x2034;
        const Int16 SW2 = 0x1034;
        const Int16 SW3 = 0x0834;
        const Int16 SW4 = 0x0434;
        const Int16 SW5 = 0x0234;
        const Int16 SW6 = 0x0134;
        const Int16 SW7 = 0x2032;
        const Int16 SW8 = 0x1032;
        //const Int16 SW51 = 0x2234;
        //const Int16 SW61 = 0x2134;
        //const Int16 SW53 = 0x0A34;
        //const Int16 SW63 = 0x0934;
        //const Int16 CONT = 0x0034;
        //const Int16 ONES = 0x0032;

        #endregion
        private static StreetSweeper street_sweeper;

        static int valueo;  //old remote control code
        static int valuen;  //new remote control code
        static bool sweep;  //flag to record the sweep status

        private static InterruptPort remotePin;

        public static void Main()
        {
            remotePin = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di7, false, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeBoth);

            street_sweeper = new StreetSweeper();
            street_sweeper.remote_setup(remotePin);

            Thread.Sleep(500);        //delay 500ms

            street_sweeper.all_stop();

            while (true)
            {
                Run();
            }
        }

        internal static void Run()
        {
            valuen = street_sweeper.remote_value_read();  //read code from remote control
            //if (valuen != 0)
            //    Debug.Print(valuen.ToString());
            if (valuen != valueo)  //if the remote control code is different than the previous code, then change status
            {
                valueo = valuen;  //refresh the previous code
                if (valueo == SW1)                               //SW1 action
                {
                    if (sweep == false)                            //not sweep status
                        street_sweeper.street_sweeper_inward(0);  //stop the sweeper plates
                    street_sweeper.go_forward(120);             //go forward    
                }
                else if (valueo == SW3)                          //SW3 action
                {
                    if (sweep == false)                            //not sweep status
                        street_sweeper.street_sweeper_inward(0);  //stop the sweeper plates    
                    street_sweeper.go_backward(120);            //go backward
                }
                else if (valueo == SW2)                          //SW2 action
                {
                    if (sweep == false)                            //not sweep status
                        street_sweeper.street_sweeper_inward(0);  //stop the sweeper plates  
                    street_sweeper.turn_front_left(120);        //turn left  
                }
                else if (valueo == SW4)                          //SW4 action
                {
                    if (sweep == false)                            //not sweep status
                        street_sweeper.street_sweeper_inward(0);  //stop the sweeper plates
                    street_sweeper.turn_front_right(120);       //turn right   
                }
                else if (valueo == SW5)                          //SW5 action
                {
                    street_sweeper.move_stop();                 //stop wheels
                    street_sweeper.street_sweeper_inward(80);   //rotate sweeper plates inward
                }
                else if (valueo == SW6)                          //SW6 action
                {
                    street_sweeper.move_stop();                 //stop wheels
                    street_sweeper.street_sweeper_outward(80);  //rotate sweeper plates outward
                }
                else if (valueo == SW7)                          //SW7 action
                {
                    street_sweeper.street_sweeper_inward(80);   //continuously rotate sweeper plates inward 
                    sweep = true;                                 //sweeper status enable
                }
                else if (valueo == SW8)                          //SW8 action
                {
                    street_sweeper.street_sweeper_inward(0);    //stop rotating sweeper plates
                    sweep = false;                                //clear sweeper status
                }
                else                                          //if no buttons are pushed
                {
                    Debug.Print("!!! Receieved unknown command: " + valueo);
                    street_sweeper.move_stop();                 //stop wheels
                    if (sweep == true)                             //if sweeper plates are rotating
                        street_sweeper.street_sweeper_inward(80); //continue rotating sweeper plates inward      
                    else                                        //if sweeper plates are not rotating
                        street_sweeper.street_sweeper_inward(0);  //stop street sweeper          
                }
            }  
        }
    }
}
