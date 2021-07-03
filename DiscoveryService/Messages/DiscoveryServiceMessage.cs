using System;
using System.IO;

namespace LUC.DiscoveryService.Messages
{
    /// <summary>
    /// <b>Abstract</b> class for messages
    /// </summary>
    public abstract class DiscoveryServiceMessage : Message
    {
        /// <summary>
        /// Maximum bytes of a message.
        /// </summary>
        /// <remarks>
        /// In reality the max length is dictated by the network MTU.
        /// </remarks>
        public const Int32 MaxLength = 10240;

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
        public DiscoveryServiceMessage(UInt32 messageId, String machineId, UInt32 protocolVersion)
        {
            MessageId = messageId;
            MachineId = machineId;
            ProtocolVersion = protocolVersion;

        }

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
        public UInt32 ProtocolVersion { get; set; }

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
