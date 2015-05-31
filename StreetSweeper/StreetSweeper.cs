using GHIElectronics.NETMF.FEZ;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace MakeItRobotics
{
    class StreetSweeper
    {
        #region Constants

        const Byte DC_SEND_HEADER = 0x56;
        const Byte DC_RECV_HEADER = 0x76;
        const Byte FW = 0xff;
        const Byte BW = 0x00;
        const Byte DC_CMD_DIRA = 0x73;
        const Byte DC_CMD_DIRB = 0x74;
        const Byte DC_CMD_DIRC = 0x75;
        const Byte DC_CMD_DIRD = 0x76;
        const Byte DC_CMD_PWMA = 0x80;
        const Byte DC_CMD_PWMB = 0x81;
        const Byte DC_CMD_PWMC = 0x82;
        const Byte DC_CMD_PWMD = 0x83;

        const Byte IR_0_LOWER_LIMIT = 2;
        const Byte IR_0_UPPER_LIMIT = 6;
        const Byte IR_1_LOWER_LIMIT = 10;
        const Byte IR_1_UPPER_LIMIT = 15;

        #endregion

        #region Fields

        private static long lastTime;
        private static int count;
        private static int commandValueHigh;
        private static int commandValueLow;
        private static long microsecondsSinceLastEdge;

        private static long microsecondsSinceLastHighEdge;
        private static long microsecondsSinceLastLowEdge;

        private static long countEdgeLow;
        private static long countEdgeHigh;

        private static long lastEdgeLowTime;
        private static long lastEdgeHighTime;

        private static DateTime[] edgeLowBuffer;
        private static DateTime[] edgeHighBuffer;

        private static DateTime[] LowEdges;
        private static DateTime[] HighEdges;

        private static DateTime[] highEdgesBuffer;
        private static DateTime[] lowEdgesBuffer;
        private static DateTime lowEdge;
        private static DateTime highEdge;

        private static ManualResetEvent waitHandle;

        byte irDataHigh;
        byte irDataLow;
        byte irBits;
        bool irRxFlag;
        
        long current_IR_time;
        long diff_IR_time;
        //byte incount=0;  
        long last_IR_time=0;
        byte OirDataHigh, OirDataLow; 
        int CirDataHigh, CirDataLow; 
        byte even=0;
        long repeatTimer1;
        long repeatTimer2;
        private static long startTicks;

        //byte incomingByte = 0;
        //int incomingCnt = 0;
        //bool msgStart = false;
        //bool msgEnd   = false;
        //int msgHeader = 0;
        //int msgId     = 0;
        //int msgValue  = 0;

        #endregion

        private static SerialPort serial;
        private static int commandValue;

        static StreetSweeper()
        {
            countEdgeLow = -1;
            countEdgeHigh = -1;

            edgeLowBuffer = new DateTime[24];
            edgeHighBuffer = new DateTime[24];

            LowEdges = new DateTime[24];
            HighEdges = new DateTime[24];
            lowEdgesBuffer = new DateTime[24];
            highEdgesBuffer = new DateTime[24];

            waitHandle = new ManualResetEvent(false);


            serial = new SerialPort("COM1", 10420);
            serial.ReadTimeout = 0;
            serial.ErrorReceived += serial_ErrorReceived;
            serial.Open();
            startTicks = DateTime.Now.Ticks;
        }

        private static void serial_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Debug.Print("Serial Error: " + e.ToString());
        }

        internal void remote_setup(InterruptPort remotePin)
        {
            //remotePin = new InputPort((Cpu.Pin)FEZ_Pin.Digital.Di10, false, Port.ResistorMode.PullUp);
            //remotePin.EnableInterrupt();
            remotePin.OnInterrupt += remotePin_OnInterrupt; //interrupted;

            //pinMode(10, INPUT);
            //digitalWrite(10, HIGH);
            //PCICR |= (1 << PCIE0);  // Enable PCINT0
            //PCMSK0 |= (1 << PCINT2); // mask for bit4 of port B (pin 10)
            //MCUCR = (1 << ISC01) | (1 << ISC00);
        }

        void remotePin_OnInterrupt(uint data1, uint data2, DateTime time)
        {
            if (data2 == 0)
            {
                microsecondsSinceLastLowEdge = (time.Ticks - edgeLowBuffer[0].Ticks) / 1000;
                if (edgeLowBuffer[0] != DateTime.MinValue && microsecondsSinceLastLowEdge > 4000)
                {
                    Array.Clear(edgeLowBuffer, 0, 24);
                    Array.Clear(edgeHighBuffer, 0, 24);
                    Debug.Print("Incomplete command after 4ms. Delay: " + microsecondsSinceLastLowEdge + ", countEdgeLow: " + countEdgeLow);
                    countEdgeLow = -1;
                    countEdgeHigh = -1;
                }

                countEdgeLow++;

                if (countEdgeLow < 24)
                {
                    edgeLowBuffer[countEdgeLow] = time;
                }
                //countEdgeLow++;
                //microsecondsSinceLastLowEdge = (time.Ticks - lastEdgeLowTime) / 1000;
                //lastEdgeLowTime = time.Ticks;
            }
            else
            {
                microsecondsSinceLastLowEdge = (time.Ticks - edgeLowBuffer[0].Ticks) / 1000;
                if (edgeLowBuffer[0] != DateTime.MinValue && microsecondsSinceLastLowEdge > 4000)
                {
                    //Array.Clear(edgeLowBuffer, 0, 24);
                    Array.Clear(edgeHighBuffer, 0, 24);
                    //countEdgeLow = -1;
                    Debug.Print("Incomplete command after 4ms. Delay: " + microsecondsSinceLastLowEdge + ", countEdgeHigh: " + countEdgeHigh);
                    countEdgeHigh = -1;
                    return;
                }

                countEdgeHigh++;

                if (countEdgeHigh < 23)
                {
                    edgeHighBuffer[countEdgeHigh] = time;
                }
                else if (countEdgeHigh == 23)
                {
                    edgeHighBuffer[countEdgeHigh] = time;
                    
                    lastTime = ((DateTime.Now.Ticks / 10000) - repeatTimer1);

                    if (lastTime > 300)
                    {
                        lock (highEdgesBuffer)
                        {
                            edgeHighBuffer.CopyTo(HighEdges, 0);
                            edgeLowBuffer.CopyTo(LowEdges, 0);
                        }
                    }
                    else
                    {
                        Debug.Print("--> skipping since lastTime == " + lastTime);
                    }
                    Array.Clear(edgeHighBuffer, 0, 24);
                    Array.Clear(edgeLowBuffer, 0, 24);
                    countEdgeLow = -1;
                    countEdgeHigh = -1;
                    if (lastTime > 300)
                    {
                        irRxFlag = true;
                        repeatTimer1 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                        waitHandle.Set();
                        Debug.Print("Command completed. Cleared receive buffers");
                    }
                }
                //microsecondsSinceLastHighEdge = (time.Ticks - lastEdgeLowTime) / 1000;
                //lastEdgeHighTime = time.Ticks;
            }

            //Debug.Print("(" + data1.ToString() + "/" + data2.ToString() + ") <" + countEdgeLow + "/" + countEdgeHigh + ">");
        }

        private void process_command_buffer(ref int command)
        {
            command = 0;
            commandValueHigh = 0;
            commandValueLow = 0;

            lock (highEdgesBuffer)
            {
                Array.Copy(HighEdges, highEdgesBuffer, HighEdges.Length);
                Array.Copy(LowEdges, lowEdgesBuffer, LowEdges.Length);
            }

            var vals = new int[24];
            var times = new int[24];

            for (count = 0; count < 24; count++)
            {
                lowEdge = lowEdgesBuffer[count];
                highEdge = highEdgesBuffer[count];

                if (lowEdge == default(DateTime) || highEdge == default(DateTime))
                {
                    // We found an incomplete command buffer.
                    return;
                }

                times[count] = (int)(highEdge.Ticks - lowEdge.Ticks) / 1000;

                if (count >= 0 && count <= 5)
                {
                    commandValueLow <<= 1;

                    if ((highEdge.Ticks - lowEdge.Ticks) / 1000 <= 8)
                    {
                        vals[count] = 0;
                        commandValueLow &= 0xFE;
                    }
                    else
                    {
                        vals[count] = 1;
                        commandValueLow |= 1;
                    }
                }
                else if (count > 5 && count <= 11)
                {
                    commandValueHigh <<= 1;
                    if ((highEdge.Ticks - lowEdge.Ticks) / 1000 <= 8)
                    {
                        vals[count] = 0;
                        commandValueHigh &= 0xFE;
                    }
                    else
                    {
                        vals[count] = 1;
                        commandValueHigh |= 1;
                    }
                }
            }

            command = commandValueHigh * 256 + commandValueLow;
            
            Debug.Print("Command Value: " + command.ToString());
        }

        //private void interrupted(uint data1, uint data2, DateTime time)
        //{
        //    microsecondsSinceLastEdge = (time.Ticks - lastTime) / 1000;
        //    if (data2 == 0)
        //    {
        //        countEdgeLow++;
        //        microsecondsSinceLastLowEdge = (time.Ticks - lastEdgeLowTime) / 1000;
        //        lastEdgeLowTime = time.Ticks;
        //    }
        //    else
        //    {
        //        countEdgeHigh++;
        //        microsecondsSinceLastHighEdge = (time.Ticks - lastEdgeLowTime) / 1000;
        //        lastEdgeHighTime = time.Ticks;
        //    }

        //    if (microsecondsSinceLastEdge > 1500)
        //    {
        //        count = 0;
        //        countEdgeHigh = 0;
        //        countEdgeLow = 0;
        //    }
        //    Debug.Print(count.ToString() + ": " + microsecondsSinceLastEdge + "(" + data1.ToString() + "/" + data2.ToString() + ") [" + microsecondsSinceLastLowEdge.ToString() + "/" + microsecondsSinceLastHighEdge + "] <" + countEdgeLow + "/" +countEdgeHigh + ">");
        //    if (count == 0)
        //    {
        //        count = 1;
        //        lastTime = time.Ticks; //DateTime.Now.Ticks;                
        //        commandValueLow = 0;
        //        commandValueHigh = 0;

        //        countEdgeHigh = 0;
        //        countEdgeLow = 1;
        //        microsecondsSinceLastEdge = 0;
        //        microsecondsSinceLastHighEdge = 0;
        //        microsecondsSinceLastLowEdge = 0;
        //    }
        //    else if (count >= 24)
        //    {
        //        count++;
        //        lastTime = time.Ticks;
        //        return;
        //    }
        //    else if (count < 24 && count % 2 == 0)
        //    {
        //        count++;
        //        lastTime = time.Ticks;
        //    }
        //    else if (count > 0 && count <= 11)
        //    {
        //        microsecondsSinceLastEdge = (time.Ticks - lastTime) / 1000;
        //        commandValueLow <<= 1;
        //        if (microsecondsSinceLastEdge <= 8)
        //        {
        //            commandValueLow &= 0xFE;
        //        }
        //        else
        //        {
        //            commandValueLow |= 1;
        //        }
        //        count++;
        //    }
        //    else if (count > 11 && count <= 23)
        //    {
        //        microsecondsSinceLastEdge = (time.Ticks - lastTime) / 1000;
        //        commandValueHigh <<= 1;
        //        if (microsecondsSinceLastEdge <= 8)
        //        {
        //            commandValueHigh &= 0xFE;
        //        }
        //        else
        //        {
        //            commandValueHigh |= 1;
        //        }
        //        count++;
        //    }
        //    else
        //    {
        //        count++;
        //    }

        //    if (count == 24)
        //    {
        //        commandValue = commandValueHigh * 256 + commandValueLow;
        //        Debug.Print(count.ToString() + "Command Value: " + commandValue.ToString());
        //        irRxFlag = true;
        //        repeatTimer1 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        //        return;
        //    }
        //    else
        //    {
        //        lastTime = time.Ticks;
        //    }
        //}

        internal void all_stop()
        {
            Debug.Print("Stopping all motors");
            dc_write(DC_CMD_PWMA, 0);
            dc_write(DC_CMD_PWMB, 0);
            dc_write(DC_CMD_PWMC, 0);
            dc_write(DC_CMD_PWMD, 0);
        }

        private void dc_write(Byte type, Byte value)
        {
            var byteBuffer = new[] { (Byte)DC_SEND_HEADER, (Byte)type, (Byte)value };
            var length = byteBuffer.Length;
            var result = serial.Write(byteBuffer, 0, length);
            if (result != length)
            {
                throw new InvalidOperationException(String.Concat("Sent ", result, " bytes instead of 3."));
            }
            serial.Flush();
            Thread.Sleep(20);
        }

        internal void m3_action(Byte dir, Byte speed)
        {
          if (speed >= 255)
            speed = 1;
          else if(speed!=0)
            speed = (Byte)(256 - speed);
          dc_write(DC_CMD_DIRC, dir > 0 ? FW : BW);
          dc_write(DC_CMD_PWMC, speed);
        }

        internal void m4_action(Byte dir, Byte speed)
        {
          if (speed >= 255)
            speed = 1;
          else if(speed!=0)
            speed = (Byte)(256 - speed);
          dc_write(DC_CMD_DIRD, dir > 0 ? FW : BW);
          dc_write(DC_CMD_PWMD, speed);
        }

        internal int remote_value_read()
        {
            //return commandValue;
            repeatTimer2 = DateTime.Now.Ticks / 10000;
            if (repeatTimer2 - repeatTimer1 < 300)
            {
                if (irRxFlag == true)
                {
                    irRxFlag = false;

                    // TODO: copy the edge buffers and pass byref.

                    Debug.Print(">>> starting new command");
                    process_command_buffer(ref commandValue);
                    waitHandle.Reset();
                }
            }
            else
            {                
                commandValue = 0;
                waitHandle.WaitOne(); // wait for a new command to arrive.
            }
            return commandValue;
        }

        internal void street_sweeper_inward(Byte speed)
        {
            Debug.Print("Sweeping inward: " + speed);
            m3_action(BW, speed);
            m4_action(BW,speed);  
        }

        internal void street_sweeper_outward(Byte speed)
        {
            Debug.Print("Sweeping outward: " + speed);
            m3_action(FW, speed);
          m4_action(FW,speed); 
        }

        internal void go_forward(Byte speed)
        {
            Debug.Print("Going forward: " + speed);
            if (speed >= 255)
                speed = 1;
            else if (speed != 0)
                speed = (Byte)(256 - speed);
            dc_write(DC_CMD_DIRA, FW);
            dc_write(DC_CMD_DIRB, FW);
            dc_write(DC_CMD_PWMA, speed);
            dc_write(DC_CMD_PWMB, speed);
        }

        internal void go_backward(Byte speed)
        {
            Debug.Print("Going backward: " + speed);
            if (speed >= 255)
                speed = 1;
            else if (speed != 0)
                speed = (Byte)(256 - speed);
            dc_write(DC_CMD_DIRA, BW);
            dc_write(DC_CMD_DIRB, BW);
            dc_write(DC_CMD_PWMA, speed);
            dc_write(DC_CMD_PWMB, speed);
        }

        internal void turn_front_left(Byte speed)
        {
            Debug.Print("Left turn: " + speed);
            Byte duty = 0;
            Byte half = 0;
            if (speed == 0)
            {
                duty = 0;
                half = 0;
            }
            else if (speed >= 255)
            {
                duty = 1;
                half = 128;
            }
            else
            {
                duty = (Byte)(257 - speed);
                half = (Byte)(257 - speed * 3 / 4);
            }
            dc_write(DC_CMD_DIRA, FW);
            dc_write(DC_CMD_DIRB, FW);
            dc_write(DC_CMD_PWMA, half);
            dc_write(DC_CMD_PWMB, duty);
        }

        internal void turn_front_right(Byte speed)
        {
            Debug.Print("Right turn: " + speed);
            Byte duty = 0;
            Byte half = 0;
            if (speed == 0)
            {
                duty = 0;
                half = 0;
            }
            else if (speed >= 255)
            {
                duty = 1;
                half = 128;
            }
            else
            {
                duty = (Byte)(257 - speed);
                half = (Byte)(257 - speed * 0.6);
            } 
            dc_write(DC_CMD_DIRA, FW);
            dc_write(DC_CMD_DIRB, FW);
            dc_write(DC_CMD_PWMA, duty);
            dc_write(DC_CMD_PWMB, half);
        }

        internal void move_stop()
        {
            Debug.Print("Stopping");
            dc_write(DC_CMD_PWMA, 0);
            dc_write(DC_CMD_PWMB, 0);
        }

        internal long micros(long nowTicks)
        {
            return (nowTicks - startTicks) / 1000;
        }

        //internal void remote_scan(long nowTicks)
        //{
        //    current_IR_time = micros(nowTicks);
        //    diff_IR_time = current_IR_time - last_IR_time;
        //    Debug.Print("Diff time: " + diff_IR_time);
        //    last_IR_time = current_IR_time;
        //    if (diff_IR_time> 1500)
        //    {
        //        irBits = 0;
        //        irDataLow = 0;
        //        irDataHigh = 0;
        //    }
        //    if (irBits < 24)
        //    {
        //        if (irBits % 2 > 0)
        //        {
        //            if (diff_IR_time > IR_0_LOWER_LIMIT && diff_IR_time < IR_0_UPPER_LIMIT)
        //            {
        //                if (irBits < 12)
        //                {
        //                    irDataLow <<= 1;
        //                    irDataLow &= 0xFE;
        //                }
        //                else
        //                {
        //                    irDataHigh <<= 1;
        //                    irDataHigh &= 0xFE;
        //                }
        //            }
        //            else if (diff_IR_time > IR_1_LOWER_LIMIT && diff_IR_time < IR_1_UPPER_LIMIT)
        //            {
        //                if (irBits < 12)
        //                {
        //                    irDataLow <<= 1;
        //                    irDataLow |= 0x01;
        //                }
        //                else
        //                {
        //                    irDataHigh <<= 1;
        //                    irDataHigh |= 0x01;
        //                }
        //            }
        //        }
        //        irBits++;
        //        if (irBits == 24)
        //        {
        //            if (irDataLow == 50)  //--------->ONESµo¥Í
        //            {
        //                CirDataHigh = irDataHigh;
        //                CirDataLow = irDataLow;
        //                CirDataHigh = CirDataHigh * 256 + CirDataLow;
        //                repeatTimer1 = DateTime.Now.Ticks / 10000;
        //                irRxFlag = true;
        //            }
        //            else
        //            {
        //                if (even == 0)
        //                {
        //                    OirDataLow = irDataLow;
        //                    OirDataHigh = irDataHigh;
        //                    irBits = 0;
        //                    irDataLow = 0;
        //                    irDataHigh = 0;
        //                    even = 1;
        //                }
        //                else if (even == 1)
        //                {
        //                    even = 0;
        //                    if (OirDataLow == irDataLow && OirDataHigh == irDataHigh)//--------->CONTINUESµo¥Í
        //                    {
        //                        CirDataHigh = irDataHigh;
        //                        CirDataLow = irDataLow;
        //                        CirDataHigh = CirDataHigh * 256 + CirDataLow;
        //                        repeatTimer1 = DateTime.Now.Ticks / 10000;
        //                        irRxFlag = true;
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}
    }
}
