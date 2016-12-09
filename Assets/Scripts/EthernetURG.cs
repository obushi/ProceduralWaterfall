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
    /// <summary>
    /// UrgDevice is the abstract class every URG devices derive from.
    /// </summary>
    [Serializable]
    public abstract class URGDevice
    {
        /// <summary>
        /// Commands defined by SCIP 2.0.
        /// See also : https://www.hokuyo-aut.jp/02sensor/07scanner/download/pdf/URG_SCIP20.pdf
        /// </summary>
        protected enum SCIPCommands
        {
            VV, PP, II, // センサ情報要求コマンド(3 種類)  
            BM, QT,     // 計測開始・終了コマンド
            MD, GD,     // 距離要求コマンド(2 種類) 
            ME          // 距離・受光強度要求コマンド 
        }

        /// <summary>
        /// Connection type of URG sensor.
        /// </summary>
        public enum ConnectionType { Serial, Ethernet }

        /// <summary>
        /// List of the distance data obtained by the URG device.
        /// </summary>
        protected List<long> distances;
        public List<long> Distances { get { return distances; } }

        /// <summary>
        /// List of the intensity data obtaind by the URG device.
        /// </summary>
        protected List<long> intensities;
        public List<long> Intensities { get { return intensities; } }

        /// <summary>
        /// Establish connection with the URG device.
        /// </summary>
        public abstract void Open();

        /// <summary>
        /// End connection with the URG device.
        /// </summary>
        public abstract void Close();

        /// <summary>
        /// Read to "\n\n" from NetworkStream
        /// </summary>
        /// <returns>receive data</returns>
        protected static string ReadLine(NetworkStream stream)
        {
            if (stream.CanRead) {
                StringBuilder sb = new StringBuilder();
                bool is_NL2 = false;
                bool is_NL = false;
                do {
                    char buf = (char)stream.ReadByte();
                    if (buf == '\n') {
                        if (is_NL) {
                            is_NL2 = true;
                        } else {
                            is_NL = true;
                        }
                    } else {
                        is_NL = false;
                    }
                    sb.Append(buf);
                } while (!is_NL2);

                return sb.ToString();
            } else {
                return null;
            }
        }

        /// <summary>
        /// Write data to URG device
        /// <param name="stream">URG Device</param>
        /// <param name="data">String to write</param>
        /// </summary>
        protected static bool Write(NetworkStream stream, string data)
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
    }

    [Serializable]
    public class EthernetURG : URGDevice
    {
        [SerializeField]
        readonly IPAddress ipAddress;

        [SerializeField]
        readonly int port;

        TcpClient tcpClient;
        Thread listenThread = null;
        bool isConnected = false;
        public bool IsConnected { get { return isConnected; } }
        readonly static public ConnectionType DeviceType = ConnectionType.Ethernet;

        /// <summary>
        /// Initialize eathernet-type URG device.
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

                //// Enforce to use SCIP 2.0 for Classic URG
                //var stream = tcpClient.GetStream();
                //Write(stream, SCIP_Writer.SCIP2());
                //ReadLine(stream); // Ignore echo back
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

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

        public void Write(string data)
        {
            try {
                if (!isConnected) {
                    Open();
                }
                if (Enum.IsDefined(typeof(SCIPCommands), ParseCommand(data))) {
                    Write(tcpClient.GetStream(), data);
                }
            }
            catch (Exception e) {
                Debug.LogException(e);
            }
        }
    }
}