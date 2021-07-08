﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService
{
    class StateObjectForReceivingData
    {
        /// <summary>
        /// Size of receive buffer
        /// </summary>
        /// <value>
        /// Default value 256
        /// </value>
        public Int32 BufferSize { get; } = 256;

        public StateObjectForReceivingData()
        {
            Buffer = new Byte[BufferSize];
        }

        public StateObjectForReceivingData(Int32 bufferSize)
            : this()
        {
            BufferSize = bufferSize;
        }

        /// <summary>
        /// Client socket
        /// </summary>
        public Socket WorkSocket { get; set; } = null;

        /// <summary>
        /// Receive buffer
        /// </summary>
        public Byte[] Buffer { get; }

        /// <summary>
        /// Received data
        /// </summary>
        public List<Byte> ResultMessage { get; }
    }
}