﻿using GHIElectronics.NETMF.FEZ;
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
        long startTicks;

        //byte incomingByte = 0;
        //int incomingCnt = 0;
        //bool msgStart = false;
        //bool msgEnd   = false;
        //int msgHeader = 0;
        //int msgId     = 0;
        //int msgValue  = 0;

        #endregion

        private SerialPort serial;
        private static int commandValue;

        public StreetSweeper()
        {
            serial = new SerialPort("COM1", 10420);
            serial.ReadTimeout = 0;
            serial.ErrorReceived += serial_ErrorReceived;
            serial.Open();
            startTicks = DateTime.Now.Ticks;
        }

        private void serial_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Debug.Print("Serial Error: " + e.ToString());
        }

        internal void remote_setup(InterruptPort remotePin)
        {
            //remotePin = new InputPort((Cpu.Pin)FEZ_Pin.Digital.Di10, false, Port.ResistorMode.PullUp);
            //remotePin.EnableInterrupt();
            remotePin.OnInterrupt += interrupted;

            //pinMode(10, INPUT);
            //digitalWrite(10, HIGH);
            //PCICR |= (1 << PCIE0);  // Enable PCINT0
            //PCMSK0 |= (1 << PCINT2); // mask for bit4 of port B (pin 10)
            //MCUCR = (1 << ISC01) | (1 << ISC00);
        }

        //void remotePin_OnInterrupt(uint data1, uint data2, DateTime time)
        //{
        //    //Debug.Print("IR event: " + micros(time.Ticks));
        //    remote_scan(time.Ticks);  //analyze signal from RadioShack Make: it Robotics Remote Control
        //}
        private void interrupted(uint data1, uint data2, DateTime time)
        {
            microsecondsSinceLastEdge = (time.Ticks - lastTime) / 1000;
            if (microsecondsSinceLastEdge > 1500)
            {
                count = 0;
            }
            Debug.Print(count.ToString() + ": " + microsecondsSinceLastEdge + "(" + data1.ToString() + "/" + data2.ToString() + ")");
            if (count == 0)
            {
                count = 1;
                lastTime = time.Ticks; //DateTime.Now.Ticks;
                commandValueLow = 0;
                commandValueHigh = 0;
                microsecondsSinceLastEdge = 0;
            }
            else if (count >= 24)
            {
                count++;
                lastTime = time.Ticks;
                return;
            }
            else if (count < 24 && count % 2 == 0)
            {
                count++;
                lastTime = time.Ticks;
            }
            else if (count > 0 && count <= 11)
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
            else
            {
                count++;
            }

            if (count == 24)
            {
                commandValue = commandValueHigh * 256 + commandValueLow;
                Debug.Print(count.ToString() + "Command Value: " + commandValue.ToString());
                irRxFlag = true;
                repeatTimer1 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                return;
            }
            else
            {
                lastTime = time.Ticks;
            }
        }

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
            if ((repeatTimer2 - repeatTimer1) < 300)
            {
                if (irRxFlag == true)
                {
                    irRxFlag = false;
                }
            }
            else
            {
                commandValue = 0;
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
                half = (Byte)((Byte)(257 - speed) / 2);
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
                half = (Byte)((Byte)(257 - speed) / 2);
            } dc_write(DC_CMD_DIRA, FW);
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
