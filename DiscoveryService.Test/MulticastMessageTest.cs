using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Messages;
using NUnit.Framework;
using System;
using System.IO;

namespace DiscoveryService.Test
{
    /// <summary>
    /// Сводное описание для UnitTest3
    /// </summary>
    [TestFixture]
    public class MulticastMessageTest
    {
        [Test]
        public void Writer_Null()
        {
            MulticastMessage multicast = new MulticastMessage();
            MemoryStream stream = new MemoryStream();
            WireWriter writer = null;

            var exception = Assert.Throws<ArgumentNullException>(code: () => multicast.Write(writer));

            Assert.That(actual: exception.ParamName, expression: Is.EqualTo(nameof(writer)));
        }

        [Test]
        public void Message()
        {
            UInt32 messageId = 1111;
            UInt32 versionOfProtocol = 1;
            UInt32 tcpPort = 17500;
            String machineId = "001";
            var writer = new StringWriter();
            writer.WriteLine("Multicast message:");
            writer.WriteLine($"MessageId = {messageId};\n" +
                             $"Protocol version = {versionOfProtocol};\r\n" +
                             $"TCP port = {tcpPort};\n" +
                             $"MachineId = {machineId}");
            var expected = writer.ToString();
            MulticastMessage multicast = new MulticastMessage(messageId, tcpPort, machineId);

            var actual = multicast.ToString();

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Reader_null()
        {
            MulticastMessage Multicast = new MulticastMessage();
            MemoryStream ms = new MemoryStream();
            WireReader reader = null;

            var exception = Assert.Throws<ArgumentNullException>(code: () => Multicast.Read(reader));

            Assert.That(actual: exception.ParamName, expression: Is.EqualTo(nameof(reader)));
        }

        [Test]
        public void Reader()
        {
            UInt32 messageId = 1111;
            String machineId = "001";
            UInt32 tcpPort = 17500;
            UInt32 versionOfProtocol = 1;
            MulticastMessage message = new MulticastMessage(messageId, tcpPort, machineId);
            MemoryStream stream = new MemoryStream();
            WireWriter writer = new WireWriter(stream);
            var expected = message.ToString();

            writer.Write(messageId);
            writer.Write(versionOfProtocol);
            writer.Write(tcpPort);
            writer.Write(machineId);
            stream.Position=0;
            var actual = message.Read(new WireReader(stream)).ToString();

            Assert.AreEqual(expected, actual);
        }
    }
}
