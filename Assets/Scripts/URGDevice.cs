using System;
using System.Collections.Generic;

namespace URG
{
    /// <summary>
    /// UrgDevice is the abstract class every URG device derives from.
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
            VV, PP, II,
            BM, QT,
            MD, GD,
            ME         
        }                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         

        /// <summary>
        /// Connection type of URG sensor.
        /// </summary>
        public enum ConnectionType { Serial, Ethernet }


        protected List<long> distances;
        /// <summary>
        /// List of the distance data captured by the URG device.
        /// </summary>
        public List<long> Distances { get { return distances; } }

        protected List<long> intensities;
        /// <summary>
        /// List of the intensity data captured by the URG device.
        /// </summary>
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
        /// Write data to the URG device.
        /// </summary>
        /// <param name="data"></param>
        public abstract void Write(string data);

        /// <summary>
        /// Connection status between sensor and host.
        /// </summary>
        public abstract bool IsConnected { get; }

        /// <summary>
        /// Smallest step number for capturing data from the URG device.
        /// </summary>
        public abstract int StartStep { get; }

        /// <summary>
        /// Largest step number for capturing data from the URG device.
        /// </summary>
        public abstract int EndStep { get; }

        /// <summary>
        /// Number of steps per degree * 360
        /// </summary>
        public abstract int StepsCount360 { get; }
    }
}