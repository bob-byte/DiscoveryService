using System;
using System.IO;

namespace LUC.DiscoveryService.Messages
{
    /// <summary>
    /// <b>Abstract</b> class for messages
    /// </summary>
    abstract class DiscoveryServiceMessage : Message
    {
        public DiscoveryServiceMessage()
        {
            ;//do nothing
        }

        /// <summary>
        ///   Create a new instance of the <see cref="DiscoveryServiceMessage"/> class.
        /// </summary>
        /// <param name="messageId">
        ///   Unique message identifier. It is used to detect duplicate messages.
        /// </param>
        /// <param name="protocolVersion">
        ///   Supported version of protocol
        /// </param>
        public DiscoveryServiceMessage(UInt32 messageId, String machineId, UInt16 protocolVersion)
        {
            MessageId = messageId;
            MachineId = machineId;
            ProtocolVersion = protocolVersion;
        }

        /// <summary>
        /// TCP port which is being run in machine with machineId.
        /// TCP port for inter-service communications.
        /// </summary>
        public UInt16 TcpPort { get; set; }

        /// <summary>
        ///   Unique message identifier. It is used to detect duplicate messages.
        /// </summary>
        public UInt32 MessageId { get; set; }

        /// <summary>
        /// Id of machine which is sending this messege
        /// </summary>
        public String MachineId { get; set; }

        /// <summary>
        ///   Supported version of protocol of the remote application.
        /// </summary>
        public UInt16 ProtocolVersion { get; set; }

        public override string ToString()
        {
            using (var writer = new StringWriter())
            {
                writer.Write($"MessageId = {MessageId};\n" +
                             $"MachineId = {MachineId}\n" +
                             $"Protocol version = {ProtocolVersion}");

                return writer.ToString();
            }
        }
    }
}
