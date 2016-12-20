using System;
using UnityEngine;
using System.Collections.Generic;
using SCIP_library;
using System.Threading;
using System.Text;
using System.IO.Ports;

namespace URG
{
    [Serializable]
    public class SerialURG : URGDevice
    {
        [SerializeField]
        readonly string portName;

        [SerializeField]
        readonly int baudRate;

        readonly static public URGType DeviceType = URGType.Serial;

        SerialPort serialPort;
        Thread listenThread = null;

        bool isConnected = false;
        public override bool IsConnected { get { return isConnected; } }

        public override int StartStep { get { return 300; } }
        public override int EndStep { get { return 450; } }
        public override int StepCount360 { get { return 1024; } }

        /// <summary>
        /// Initialize serial-type URG device.
        /// </summary>
        /// <param name="_portName">Port name of the URG device.</param>
        /// <param name="_baudRate">Baud rate of the URG device.</param>
        public SerialURG(string _portName = "COM3", int _baudRate = 115200)
        {
            portName = _portName;
            baudRate = _baudRate;

            distances = new List<long>();
            intensities = new List<long>();
        }

        /// <summary>
        /// Establish connection with the URG device.
        /// </summary>
        public override void Open()
        {
            try
            {
                serialPort = new SerialPort(portName, baudRate);
                serialPort.NewLine = "\n\n";
                serialPort.Open();

                listenThread = new Thread(new ParameterizedThreadStart(HandleClient));
                isConnected = true;
                listenThread.IsBackground = true;
                listenThread.Start(serialPort);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// End connection with the URG device.
        /// </summary>
        public override void Close()
        {
            if (listenThread != null)
            {
                isConnected = false;
                listenThread.Join();
                listenThread = null;
            }


            if (serialPort != null)
            {
                serialPort.Close();
                serialPort.Dispose();
            }
        }

        void HandleClient(object obj)
        {
            try
            {
                using (SerialPort client = (SerialPort)obj)
                {
                    while (isConnected)
                    {
                        try
                        {
                            long timeStamp = 0;
                            string receivedData = ReadLine(client);
                            string parsedCommand = ParseCommand(receivedData);

                            SCIPCommands command = (SCIPCommands)Enum.Parse(typeof(SCIPCommands), parsedCommand);
                            switch (command)
                            {
                                case SCIPCommands.QT:
                                    distances.Clear();
                                    intensities.Clear();
                                    isConnected = false;
                                    break;
                                case SCIPCommands.MD:
                                    distances.Clear();
                                    SCIP_Reader.MD(receivedData, ref timeStamp, ref distances);
                                    break;
                                case SCIPCommands.GD:
                                    distances.Clear();
                                    SCIP_Reader.GD(receivedData, ref timeStamp, ref distances);
                                    break;
                                case SCIPCommands.ME:
                                    distances.Clear();
                                    intensities.Clear();
                                    SCIP_Reader.ME(receivedData, ref timeStamp, ref distances, ref intensities);
                                    break;
                                default:
                                    Debug.Log(receivedData);
                                    isConnected = false;
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        string ParseCommand(string receivedData)
        {
            string[] split_command = receivedData.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return split_command[0].Substring(0, 2);
        }

        /// <summary>
        /// Read to "\n\n" from NetworkStream
        /// </summary>
        /// <returns>receive data</returns>
        protected static string ReadLine(SerialPort serialport)
        {
            if (serialport.IsOpen)
            {
                StringBuilder sb = new StringBuilder();
                bool is_NL2 = false;
                bool is_NL = false;
                do
                {
                    char buf = (char)serialport.ReadByte();
                    if (buf == '\n')
                    {
                        if (is_NL)
                        {
                            is_NL2 = true;
                        }
                        else
                        {
                            is_NL = true;
                        }
                    }
                    else
                    {
                        is_NL = false;
                    }
                    sb.Append(buf);
                } while (!is_NL2);

                return sb.ToString();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Write data to the URG device.
        /// </summary>
        /// <param name="data"></param>
        public override void Write(string data)
        {
            try {
                if (!isConnected) {
                    Open();
                }
                if (Enum.IsDefined(typeof(SCIPCommands), ParseCommand(data))) {
                    var buffer = Encoding.ASCII.GetBytes(data);
                    serialPort.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception e) {
                Debug.LogException(e);
            }
        }
    }
}