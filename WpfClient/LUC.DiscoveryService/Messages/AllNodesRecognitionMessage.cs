using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common.Interfaces;

using System;
using System.IO;
using System.Text;

namespace LUC.DiscoveryServices.Messages
{
    /// <summary>
    /// UDP multicast message
    /// </summary>
    class AllNodesRecognitionMessage : DiscoveryMessage
    {
        /// <summary>
        /// Maximum bytes of a message.
        /// </summary>
        public const Int32 MAX_LENGTH = 110;

        /// <summary>
        /// Create a new instance of the <see cref="AllNodesRecognitionMessage"/> class. This constructor is often used to read message
        /// </summary>
        public AllNodesRecognitionMessage( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        /// <summary>
        /// Create a new instance of the <see cref="AllNodesRecognitionMessage"/> class. This constructor is often used to write message to a stream
        /// </summary>
        /// <param name="messageId">
        /// Unique message identifier. It is used to detect duplicate messages.
        /// </param>
        /// <param name="tcpPort">
        /// TCP port which is being run in machine with <see cref="MachineId"/>
        /// </param>
        /// <param name="machineId">
        /// Id of machine which is sending this messege
        /// </param>
        public AllNodesRecognitionMessage( UInt32 messageId, UInt16 protocolVersion, UInt16 tcpPort, String machineId )
            : base( messageId, machineId, protocolVersion, tcpPort )
        {
            DefaultInit();
        }

        protected override void DefaultInit( params Object[] args ) =>
            MessageOperation = MessageOperation.AllNodesRecognition;
    }
}
