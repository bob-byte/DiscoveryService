using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace LUC.DiscoveryService.Test
{
    /// <summary>
    /// TcpMessageTest
    /// </summary>
    [TestFixture]
    public class TcpMessageTest
    {
        [Test]
        public void Write_NullWireWriter_WriterNullException()
        {
            var tcpMessage = new TcpMessage();

            Assert.That(code: () => tcpMessage.Write(writer: null), constraint: Throws.TypeOf(typeof(ArgumentNullException)));
        }

        [Test]
        public void Ctor_NormalInput_StringEqual()
        {
            var expected = ExpectedMessage(MessageOperation.Acknowledge, messageId: 1111, machineId: ID.RandomID, protocolVersion: 1, tcpPort: 17500, groupsIds: new List<String> { "a", "b" });

            var message = new TcpMessage(messageId: 1111, ID.RandomID, tcpPort: 17500, protocolVersion: 1, new List<String> { "a", "b" });

            var actual = message.ToString();

            Assert.AreEqual(expected, actual);
        }

        private String ExpectedMessage(MessageOperation messOperation, UInt32 messageId, ID machineId, UInt32 protocolVersion, UInt32 tcpPort, List<String> groupsIds)
        {
            var writer = new StringWriter();

            writer.WriteLine("TCP message:");
            writer.WriteLine($"MachineId = {machineId}\n" +
                             $"Protocol version = {protocolVersion};\r\n" +
                             $"Message operation: {messOperation};\n" +
                             $"TCP = {tcpPort};");

            writer.WriteLine($"GroupIds:");
            for (Int32 id = 0; id < groupsIds?.Count; id++)
            {
                if (id == groupsIds.Count - 1)
                {
                    writer.WriteLine($"{groupsIds[id]}");
                }
                else
                {
                    writer.WriteLine($"{groupsIds[id]};");
                }
            }

            var expected = writer.ToString();
            return expected;
        }

        [Test]
        public void Ctor_NullGroupIds_StringEqual()
        {
            var expected = ExpectedMessage(MessageOperation.Acknowledge, messageId: 1111, machineId: ID.RandomID, protocolVersion: 1, tcpPort: 17500, groupsIds: null);

            var message = new TcpMessage(messageId: 1111, machineId: ID.RandomID, tcpPort: 17500, protocolVersion: 1, groupsIds: null);

            var actual = message.ToString();

            Assert.AreEqual(expected, actual);
        }
    }
}
