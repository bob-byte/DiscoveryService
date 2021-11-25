﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages
{
    public class MessageEventArgs : EventArgs
    {
        private Message m_message;

        /// <summary>
        /// Bytes which <see cref="RemoteEndPoint"/> sent
        /// </summary>
        public Byte[] Buffer { get; set; }

        /// <summary>
        ///   Message sender endpoint. It is used to store source IP address.
        /// </summary>
        /// <value>
        ///   The endpoint from the message was received.
        /// </value>
        public EndPoint RemoteEndPoint { get; set; }

        internal Boolean IsReadMessage<T>()
            where T : Message, new()
        {
            return (m_message as T) != null;
        }

        /// <summary>
        ///   The LightUpon.Cloud app message.
        /// </summary>
        /// <param name="whetherReadMessage">
        /// Whether read message if it has not been read
        /// </param>
        /// <value>
        ///   The received message.
        /// </value>
        internal T Message<T>( Boolean whetherReadMessage = true )
            where T : Message, new()
        {
            if ( whetherReadMessage && !IsReadMessage<T>() )
            {
                m_message = new T();
                m_message.Read( Buffer );
            }

            return m_message as T;
        }

        /// <summary>
        /// Set read message
        /// </summary>
        /// <typeparam name="T">
        /// Type of any Discovery Service message
        /// </typeparam>
        /// <param name="message">
        /// Read message
        /// </param>
        internal void SetMessage<T>( T message )
            where T : Message, new()
        {
            m_message = message;
        }
    }
}
