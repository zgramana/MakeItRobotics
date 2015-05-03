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

        const Int16 DC_SEND_HEADER = 0x56;
        const Int16 DC_RECV_HEADER = 0x76;
        const Int16 FW = 0xff;
        const Int16 BW = 0x00;
        const Int16 DC_CMD_DIRA = 0x73;
        const Int16 DC_CMD_DIRB = 0x74;
        const Int16 DC_CMD_DIRC = 0x75;
        const Int16 DC_CMD_DIRD = 0x76;
        const Int16 DC_CMD_PWMA = 0x80;
        const Int16 DC_CMD_PWMB = 0x81;
        const Int16 DC_CMD_PWMC = 0x82;
        const Int16 DC_CMD_PWMD = 0x83;

        const Int16 IR_0_LOWER_LIMIT = 0;
        const Int16 IR_0_UPPER_LIMIT = 14;
        const Int16 IR_1_LOWER_LIMIT = 15;
        const Int16 IR_1_UPPER_LIMIT = 40;

        #endregion

        #region Fields

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
            remotePin.OnInterrupt += remotePin_OnInterrupt;

            //pinMode(10, INPUT);
            //digitalWrite(10, HIGH);
            //PCICR |= (1 << PCIE0);  // Enable PCINT0
            //PCMSK0 |= (1 << PCINT2); // mask for bit4 of port B (pin 10)
            //MCUCR = (1 << ISC01) | (1 << ISC00);
        }

        void remotePin_OnInterrupt(uint data1, uint data2, DateTime time)
        {
            //Debug.Print("IR event: " + micros(time.Ticks));
            remote_scan(time.Ticks);  //analyze signal from RadioShack Make: it Robotics Remote Control
        }

        internal void all_stop()
        {
            Debug.Print("Stopping all motors");
            dc_write(DC_CMD_PWMA, 0);
            dc_write(DC_CMD_PWMB, 0);
            dc_write(DC_CMD_PWMC, 0);
            dc_write(DC_CMD_PWMD, 0);
        }

        private void dc_write(Int16 type, Int16 value)
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

        internal void m3_action(short dir, short speed)
        {
          if (speed >= 255)
            speed = 1;
          else if(speed!=0)
            speed = (short)(256 - speed);
          dc_write(DC_CMD_DIRC, dir > 0 ? FW : BW);
          dc_write(DC_CMD_PWMC, speed);
        }

        internal void m4_action(short dir, short speed)
        {
          if (speed >= 255)
            speed = 1;
          else if(speed!=0)
            speed = (short)(256 - speed);
          dc_write(DC_CMD_DIRD, dir > 0 ? FW : BW);
          dc_write(DC_CMD_PWMD, speed);
        }

        internal int remote_value_read()
        {
            repeatTimer2 = DateTime.Now.Ticks / 10000;
            if ((repeatTimer2 - repeatTimer1) < 300)
            {
                if (irRxFlag == true)
                {
                    Debug.Print("IR Receive Flag True");
                    irRxFlag = false;
                    irBits = 0;
                }
            }
            else
            {
                CirDataHigh = 0;
            }
            return CirDataHigh;
        }

        internal void street_sweeper_inward(short speed)
        {
            Debug.Print("Sweeping inward: " + speed);
            m3_action(BW, speed);
          m4_action(BW,speed);  
        }

        internal void street_sweeper_outward(short speed)
        {
            Debug.Print("Sweeping outward: " + speed);
            m3_action(FW, speed);
          m4_action(FW,speed); 
        }

        internal void go_forward(Int16 speed)
        {
            Debug.Print("Going forward: " + speed);
            if (speed >= 255)
                speed = 1;
            else if (speed != 0)
                speed = (Int16)(256 - speed);
            dc_write(DC_CMD_DIRA, FW);
            dc_write(DC_CMD_DIRB, FW);
            dc_write(DC_CMD_PWMA, speed);
            dc_write(DC_CMD_PWMB, speed);
        }

        internal void go_backward(Int16 speed)
        {
            Debug.Print("Going backward: " + speed);
            if (speed >= 255)
                speed = 1;
            else if (speed != 0)
                speed = (short)(256 - speed);
            dc_write(DC_CMD_DIRA, BW);
            dc_write(DC_CMD_DIRB, BW);
            dc_write(DC_CMD_PWMA, speed);
            dc_write(DC_CMD_PWMB, speed);
        }

        internal void turn_front_left(Int16 speed)
        {
            Debug.Print("Left turn: " + speed);
            short duty = 0;
            short half = 0;
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
                duty = (short)(257 - speed);
                half = (short)((short)(257 - speed) / 2);
            }
            dc_write(DC_CMD_DIRA, FW);
            dc_write(DC_CMD_DIRB, FW);
            dc_write(DC_CMD_PWMA, half);
            dc_write(DC_CMD_PWMB, duty);
        }

        internal void turn_front_right(Int16 speed)
        {
            Debug.Print("Right turn: " + speed);
            short duty = 0;
            short half = 0;
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
                duty = (short)(257 - speed);
                half = (short)((short)(257 - speed) / 2);
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

        internal void remote_scan(long nowTicks)
        {
            current_IR_time = micros(nowTicks);
            diff_IR_time = current_IR_time - last_IR_time;
            Debug.Print("Diff time: " + diff_IR_time);
            last_IR_time = current_IR_time;
            if (diff_IR_time> 1500)
            {
                irBits = 0;
                irDataLow = 0;
                irDataHigh = 0;
            }
            if (irBits < 24)
            {
                if (irBits % 2 > 0)
                {
                    if (diff_IR_time > IR_0_LOWER_LIMIT && diff_IR_time < IR_0_UPPER_LIMIT)
                    {
                        if (irBits < 12)
                        {
                            irDataLow <<= 1;
                            irDataLow &= 0xFE;
                        }
                        else
                        {
                            irDataHigh <<= 1;
                            irDataHigh &= 0xFE;
                        }
                    }
                    else if (diff_IR_time > IR_1_LOWER_LIMIT && diff_IR_time < IR_1_UPPER_LIMIT)
                    {
                        if (irBits < 12)
                        {
                            irDataLow <<= 1;
                            irDataLow |= 0x01;
                        }
                        else
                        {
                            irDataHigh <<= 1;
                            irDataHigh |= 0x01;
                        }
                    }
                }
                irBits++;
                if (irBits == 24)
                {
                    if (irDataLow == 50)  //--------->ONESµo¥Í
                    {
                        CirDataHigh = irDataHigh;
                        CirDataLow = irDataLow;
                        CirDataHigh = CirDataHigh * 256 + CirDataLow;
                        repeatTimer1 = DateTime.Now.Ticks / 10000;
                        irRxFlag = true;
                    }
                    else
                    {
                        if (even == 0)
                        {
                            OirDataLow = irDataLow;
                            OirDataHigh = irDataHigh;
                            irBits = 0;
                            irDataLow = 0;
                            irDataHigh = 0;
                            even = 1;
                        }
                        else if (even == 1)
                        {
                            even = 0;
                            if (OirDataLow == irDataLow && OirDataHigh == irDataHigh)//--------->CONTINUESµo¥Í
                            {
                                CirDataHigh = irDataHigh;
                                CirDataLow = irDataLow;
                                CirDataHigh = CirDataHigh * 256 + CirDataLow;
                                repeatTimer1 = DateTime.Now.Ticks / 10000;
                                irRxFlag = true;
                            }
                        }
                    }
                }
            }
        }
    }
}
