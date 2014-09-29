using System;
using Microsoft.SPOT;
using System.Threading;
using System.IO.Ports;

namespace LineFollower
{
    class LineFollower
    {

        #region Fields

        private SerialPort serial;

        private const int IR_0_LOWER_LIMIT = 250;
        private const int IR_0_UPPER_LIMIT = 550;
        private const int IR_1_LOWER_LIMIT = 900;
        private const int IR_1_UPPER_LIMIT = 1400;

        ulong current_IR_time;
        ulong diff_IR_time;

        byte incount = 0;
        ulong last_IR_time = 0;

        byte OirDataHigh, OirDataLow;
        byte CirDataHigh, CirDataLow;

        byte even = 0;

        long repeatTimer1 = DateTime.Now.Ticks;
        long repeatTimer2 = DateTime.Now.Ticks;  

        byte incomingByte = 0;
        int incomingCnt = 0;

        Boolean msgStart = false;
        Boolean msgEnd   = false;

        int msgHeader = 0;
        int msgId     = 0;
        int msgValue  = 0;

        #endregion

        #region Constants

        private const int DC_CMD_IR_RX1 = 0x60;
        private const int DC_CMD_IR_RX2 = 0x61;
        private const int DC_CMD_IR_RX3 = 0x62;
        private const int DC_CMD_IR_TX1 = 0x70;
        private const int DC_CMD_IR_TX2 = 0x71;
        private const int DC_CMD_IR_TX3 = 0x72;
        private const int SW_ON = 0xff;

        private const int DC_SEND_HEADER = 0x56;
        private const int DC_RECV_HEADER = 0x76;
        private const int DC_CMD_DIRA = 0x73;
        private const int DC_CMD_DIRB = 0x74;
        private const int DC_CMD_DIRC = 0x75;
        private const int DC_CMD_DIRD = 0x76;
        private const int DC_CMD_PWMA = 0x80;
        private const int DC_CMD_PWMB = 0x81;
        private const int DC_CMD_PWMC = 0x82;
        private const int DC_CMD_PWMD = 0x83;
        private const int FW = 0xff;
        private const int BW  = 0x00;
        private const int PIN_LED1 = 8;     //LED control
        private const int PIN_LED2 = 11;    //LED control
        private const int PIN_LED3 = 12;    //LED control
        private const int PIN_LED4 = 13;    //LED control

        private const int SW1 = 0x2034;
        private const int SW2 = 0x1034;
        private const int SW3 = 0x0834;
        private const int SW4 = 0x0434;
        private const int SW5 = 0x0234;
        private const int SW6 = 0x0134;
        private const int SW7 = 0x2032;
        private const int SW8 = 0x1032;
        private const int SW51 = 0x2234;
        private const int SW61 = 0x2134;
        private const int SW53 = 0x0A34;
        private const int SW63 = 0x0934;
        private const int CONT = 0x34;
        private const int ONES = 0x32;

        #endregion

        public LineFollower()
        {
            serial = new SerialPort("COM1", 10420);
            serial.Open();

            Thread.Sleep(500);        //delay 500ms

            line_following_setup();   //initialize the status of line following robot
            all_stop();               //all motors stop
        }

        internal void Run()
        {
            int sensor_in;                  //variable to store the value of read_optical function feedback 
            int sensorValue1 = 0;           //variable to store optical1 status
            int sensorValue2 = 0;           //variable to store optical2 status
            int sensorCnt = 0;              //variable to count for trigger which optical

            long sensorTimer1 = DateTime.Now.Ticks;   //last triggered time
            long sensorTimer2 = DateTime.Now.Ticks;   //now time

            int action1 = 0;                //now action
            int action2 = 0;                //last action

            //************************************************************************
            //  Trigger Left/Right optical every 15 milliseconds
            //************************************************************************ 
            sensorTimer2 = DateTime.Now.Ticks;                 //read now time

            if (sensorTimer2 - sensorTimer1 > 15)    //if now time minus last triggered time is greater than 15ms, then trigger another optical
            {
                sensorTimer1 = sensorTimer2;           //last triggered time = now time
                /***********************************************************************
                    -> trigger optical1 -> greater than 15ms -> trigger optical2 -> greater than 15ms ->|
                    |-----------------------------------------------------------------------------------|
                ***********************************************************************/
                if (sensorCnt == 0)
                    trigger_optical1();
                else if (sensorCnt == 1)
                    trigger_optical2();

                sensorCnt++;

                if (sensorCnt == 2)
                    sensorCnt = 0;
            }
            //***********************************************************************
            //  Read Left/Right optical status
            //***********************************************************************
            sensor_in = read_optical();
            /************************************************************************
            read_optical()
            Description
                Reads the value from optical1(Right side) or optical2(Left side)
            Syntax
                read_optical()
            Parameters
                none
            Returns
                0x000  optical1 black
                0x0ff  optical1 white
                0x100  optical1 white
                0x1ff  optical1 black
                0x2XX  not ready; don't use this value      
            *************************************************************************/
            if ((sensor_in & 0xf00) == 0)
                sensorValue1 = sensor_in & 0xff;
            else if ((sensor_in & 0xf00) >> 8 == 1)
                sensorValue2 = sensor_in & 0xff;

            if (sensorValue1 == 0x00)
                action1 = action1 & 0xfe;
            if (sensorValue1 == 0xFF)
                action1 = action1 | 0x01;
            if (sensorValue2 == 0x00)
                action1 = action1 | 0x02;
            if (sensorValue2 == 0xFF)
                action1 = action1 & 0xfd;
            /************************************************************************
            action1
                        left        right
            0x00    black        black
            0x01    black        white
            0x02    white        black
            0x03    white        white
            *************************************************************************/
            /************************************************************************
                Make Robot Move
                if action1 is not equal to action2, then change motor status
                if action1 is equal to action2, then do nothing
            *************************************************************************/
            if (action1 != action2)
            {
                if (action1 == 3)
                    go_forward(50);
                if (action1 == 1)
                    line_following_turn_left(50);
                if (action1 == 2)
                    line_following_turn_right(50);
                if (action1 == 0)
                    go_forward(50);
            }
            action2 = action1;
        }

        private void dc_write(int type, int value)
        {
            serial.Write(new[] { (Byte)DC_SEND_HEADER, (Byte)type, (Byte)value }, 0, 3);
            Thread.Sleep(20);
        }

        private void all_stop()
        {
            dc_write(DC_CMD_PWMA, 0);
            dc_write(DC_CMD_PWMB, 0);
            dc_write(DC_CMD_PWMC, 0);
            dc_write(DC_CMD_PWMD, 0);
        }

        private void line_following_setup()
        {
            dc_write(DC_CMD_IR_TX1, SW_ON);
            dc_write(DC_CMD_IR_TX2, SW_ON);
        }

        private void line_following_turn_right(int speed)
        {
            int duty = 0;
            int half = 0;
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
                duty = 257 - speed;
                half = 257 - speed * 3 / 4;
            }
            dc_write(DC_CMD_DIRA, FW);
            dc_write(DC_CMD_DIRB, BW);
            dc_write(DC_CMD_PWMA, duty);
            dc_write(DC_CMD_PWMB, half);
        }

        private void line_following_turn_left(int speed)
        {
            int duty = 0;
            int half = 0;
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
                duty = 257 - speed;
                half = 257 - speed * 3 / 4;
            }
            dc_write(DC_CMD_DIRA, BW);
            dc_write(DC_CMD_DIRB, FW);
            dc_write(DC_CMD_PWMA, half);
            dc_write(DC_CMD_PWMB, duty);
        }

        private void go_forward(int speed)
        {
            if (speed >= 255)
                speed = 1;
            else if (speed != 0)
                speed = 256 - speed;
            dc_write(DC_CMD_DIRA, FW);
            dc_write(DC_CMD_DIRB, FW);
            dc_write(DC_CMD_PWMA, speed);
            dc_write(DC_CMD_PWMB, speed);
        }

        private int read_optical()
        {
            while (serial.IsOpen)
            {
                var buffer = new byte[1];
                var result = serial.Read(buffer, 0, 1);
                incomingByte = buffer[0];
                if (msgStart && incomingCnt == 2)
                {
                    msgValue = incomingByte;
                    incomingCnt = 3;
                    msgEnd = true;
                    break;
                }
                if (msgStart && incomingCnt == 1)
                {
                    msgId = incomingByte;
                    incomingCnt = 2;
                }
                if (incomingByte == DC_RECV_HEADER && !msgStart)
                {
                    msgHeader = incomingByte;
                    incomingCnt = 1;
                    msgStart = true;
                }
            }
            if (msgStart && msgEnd)
            {
                msgStart = false;
                msgEnd = false;
                incomingCnt = 0;
                if (msgId == DC_CMD_IR_RX1)
                    msgValue &= 0xff;
                if (msgId == DC_CMD_IR_RX2)
                    msgValue |= 0x100;
                return msgValue;
            }
            else
            {
                return 0x200;
            }
        }

        private void trigger_optical2()
        {
            dc_write(DC_CMD_IR_RX2, 0x00);
        }

        private void trigger_optical1()
        {
            dc_write(DC_CMD_IR_RX1, 0x00);
        }
    }
}
