using System;
using Microsoft.SPOT;
using System.Threading;
using System.IO.Ports;
using System.Text;

namespace LineFollower
{
    class LineFollower
    {

        #region Fields

        private SerialPort serial;

        long repeatTimer1 = DateTime.Now.Ticks;
        long repeatTimer2 = DateTime.Now.Ticks;

        Int16 sensor_in;                  //variable to store the value of read_optical function feedback 
        Int16 sensorValue1 = 0;           //variable to store optical1 status
        Int16 sensorValue2 = 0;           //variable to store optical2 status
        Int16 sensorCnt = 0;              //variable to count for trigger which optical

        long sensorTimer1 = DateTime.Now.Ticks;   //last triggered time
        long sensorTimer2 = DateTime.Now.Ticks;   //now time

        Int16 action1 = 0;                //now action
        Int16 action2 = 0;                //last action

        Int16 msgHeader = 0;
        Int16 msgId     = 0;
        Int16 msgValue  = 0;

        #endregion

        #region Constants

        const Int16 DC_CMD_IR_RX1 = 0x60;
        const Int16 DC_CMD_IR_RX2 = 0x61;
        const Int16 DC_CMD_IR_RX3 = 0x62;
        const Int16 DC_CMD_IR_TX1 = 0x70;
        const Int16 DC_CMD_IR_TX2 = 0x71;
        const Int16 DC_CMD_IR_TX3 = 0x72;
        const Int16 SW_ON = 0xff;

        const Int16 DC_SEND_HEADER = 0x56;
        const Int16 DC_RECV_HEADER = 0x76;
        const Int16 DC_CMD_DIRA = 0x73;
        const Int16 DC_CMD_DIRB = 0x74;
        const Int16 DC_CMD_DIRC = 0x75;
        const Int16 DC_CMD_DIRD = 0x76;
        const Int16 DC_CMD_PWMA = 0x80;
        const Int16 DC_CMD_PWMB = 0x81;
        const Int16 DC_CMD_PWMC = 0x82;
        const Int16 DC_CMD_PWMD = 0x83;
        const Int16 FW = 0xff;
        const Int16 BW  = 0x00;
        const Int16 PIN_LED1 = 8;     //LED control
        const Int16 PIN_LED2 = 11;    //LED control
        const Int16 PIN_LED3 = 12;    //LED control
        const Int16 PIN_LED4 = 13;    //LED control

        #endregion

        public LineFollower()
        {
            serial = new SerialPort("COM1", 10420);
            serial.ReadTimeout = 0;
            serial.ErrorReceived += serial_ErrorReceived;
            serial.Open();

            Thread.Sleep(500);        //delay 500ms

            line_following_setup();   //initialize the status of line following robot
            all_stop();               //all motors stop
            Thread.Sleep(1000);
        }

        void serial_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Debug.Print("Serial Error: " + e.ToString());
        }

        internal void Run()
        {
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
            Debug.Print("Optical value: " + sensor_in);
            /************************************************************************
            read_optical()
            Description
                Reads the value from optical1(Right side) or optical2(Left side)
            Syntax
                read_optical()
            Parameters
                none
            Returns
                0x000  optical1 black (0)
                0x0ff  optical1 white (255)
                0x100  optical2 white (256)
                0x1ff  optical2 black (511)
                0x2XX  not ready; don't use this value      
            *************************************************************************/
            if ((sensor_in & 0xf00) == 0)
                sensorValue1 = (Int16)(sensor_in & 0xff);
            else if ((sensor_in & 0xf00) >> 8 == 1)
                sensorValue2 = (Int16)(sensor_in & 0xff);

            if (sensorValue1 == 0x00)
                action1 = (Int16)(action1 & 0xfe);
            if (sensorValue1 == 0xFF)
                action1 = (Int16)(action1 | 0x01);
            if (sensorValue2 == 0x00)
                action1 = (Int16)(action1 | 0x02);
            if (sensorValue2 == 0xFF)
                action1 = (Int16)(action1 & 0xfd);
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

        private void line_following_turn_right(Int16 speed)
        {
            Debug.Print("Right turn: " + speed);
            Int16 duty = 0;
            Int16 half = 0;
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
                duty = (Int16)(257 - speed);
                half = (Int16)(257 - speed * 3 / 4);
            }
            dc_write(DC_CMD_DIRA, FW);
            dc_write(DC_CMD_DIRB, BW);
            dc_write(DC_CMD_PWMA, duty);
            dc_write(DC_CMD_PWMB, half);
        }

        private void line_following_turn_left(Int16 speed)
        {
            Debug.Print("Left turn: " + speed);
            Int16 duty = 0;
            Int16 half = 0;
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
                duty = (Int16)(257 - speed);
                half = (Int16)(257 - speed * 3 / 4);
            }
            dc_write(DC_CMD_DIRA, BW);
            dc_write(DC_CMD_DIRB, FW);
            dc_write(DC_CMD_PWMA, half);
            dc_write(DC_CMD_PWMB, duty);
        }

        private void go_forward(Int16 speed)
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

        private Int16 read_optical()
        {
            if (serial.IsOpen)
            {
                var buffer = new byte[serial.BytesToRead];
                var result = serial.Read(buffer, 0, serial.BytesToRead);

                msgHeader = buffer[0];
                msgId = buffer[1];
                msgValue = buffer[2];

                if (msgHeader == DC_RECV_HEADER)
                {
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
            else
            {
                return 0x200;
            }
        }

        private void trigger_optical2()
        {
            dc_write(DC_CMD_IR_RX2, 0);
        }

        private void trigger_optical1()
        {
            dc_write(DC_CMD_IR_RX1, 0);
        }
    }
}
