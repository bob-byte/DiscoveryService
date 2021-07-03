using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages
{
    public class MessageEventArgs : EventArgs
    {
        /// <summary>
        ///   The LightUpon.Cloud app message.
        /// </summary>
        /// <value>
        ///   The received message.
        /// </value>
        public Message Message { get; set; }
    }
}
