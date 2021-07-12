using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages
{
    public class MessageEventArgs : EventArgs
    {
        private Message message;

        public Byte[] Buffer { get; set; }

        public Boolean IsReadMessage { get; }

        /// <summary>
        ///   The LightUpon.Cloud app message.
        /// </summary>
        /// <param name="whetherReadMessage">
        /// Whether read message if it has not been read
        /// </param>
        /// <value>
        ///   The received message.
        /// </value>
        public T Message<T>(Boolean whetherReadMessage = true)
            where T: Message, new()
        {
            if (whetherReadMessage && message == null)
            {
                message = new T();
                message.Read(Buffer);
            }

            return message as T;
        }

        public void SetMessage<T>(T message)
            where T : Message, new()
        {
            this.message = message;
        }

        public BigInteger LocalContactId { get; set; }
    }
}
