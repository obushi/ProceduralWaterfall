using System;
using UnityEngine;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using SCIP_library;
using System.Threading;
using System.Text;

namespace URG
{
    [Serializable]
    public class EthernetURG : URGDevice
    {
        [SerializeField]
        readonly IPAddress ipAddress;

        [SerializeField]
        readonly int port;

        readonly static public URGType DeviceType = URGType.Ethernet;

        TcpClient tcpClient;
        Thread listenThread = null;

        bool isConnected = false;
        public override bool IsConnected { get { return isConnected; } }
        
        public override int StartStep { get { return 460; } }
        public override int EndStep { get { return 620; } }
        public override int StepCount360 { get { return 1440; } }

        /// <summary>
        /// Initialize ethernet-type URG device.
        /// </summary>
        /// <param name="_ipAddress">IP Address of the URG device.</param>
        /// <param name="_port">Port number of the URG device.</param>
        public EthernetURG(string _ipAddress = "192.168.0.35", int _port = 10940)
        {
            ipAddress = IPAddress.Parse(_ipAddress);
            port = _port;

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
                tcpClient = new TcpClient();
                tcpClient.Connect(ipAddress, port);
                listenThread = new Thread(new ParameterizedThreadStart(HandleClient));
                isConnected = true;
                listenThread.IsBackground = true;
                listenThread.Start(tcpClient);
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


            if (tcpClient != null)
            {
                if (tcpClient.Connected)
                {
                    if (tcpClient.GetStream() != null)
                    {
                        tcpClient.GetStream().Close();
                    }
                }
                tcpClient.Close();
            }
        }

        void HandleClient(object obj)
        {
            try
            {
                using (TcpClient client = (TcpClient)obj)
                using (NetworkStream stream = client.GetStream())
                {
                    while (isConnected)
                    {
                        try
                        {
                            long timeStamp = 0;
                            string receivedData = ReadLine(stream);
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
        protected static string ReadLine(NetworkStream stream)
        {
            if (stream.CanRead)
            {
                StringBuilder sb = new StringBuilder();
                bool is_NL2 = false;
                bool is_NL = false;
                do
                {
                    char buf = (char)stream.ReadByte();
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

        protected static bool TCPWrite(NetworkStream stream, string data)
        {
            if (stream.CanWrite)
            {
                byte[] buffer = Encoding.ASCII.GetBytes(data);
                stream.Write(buffer, 0, buffer.Length);
                return true;
            }
            else
            {
                return false;
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
                    TCPWrite(tcpClient.GetStream(), data);
                }
            }
            catch (Exception e) {
                Debug.LogException(e);
            }
        }
    }
}