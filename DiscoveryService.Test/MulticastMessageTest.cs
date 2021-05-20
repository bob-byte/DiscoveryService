using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Messages;
using NUnit.Framework;
using System;
using System.IO;

namespace DiscoveryService.Test
{
    /// <summary>
    /// MulticastMessageTest
    /// </summary>
    [TestFixture]
    public class MulticastMessageTest
    {
        /// <summary>
        /// Returns the expected string
        /// </summary>
        private String ExpectedMessage(UInt32 messageId, UInt32 versionOfProtocol, UInt32 tcpPort, String machineId)
        {
            var writer = new StringWriter();
            writer.WriteLine("Multicast message:");
            writer.WriteLine($"MessageId = {messageId};\n" +
                             $"Protocol version = {versionOfProtocol};\r\n" +
                             $"TCP port = {tcpPort};\n" +
                             $"MachineId = {machineId}");
            var expected = writer.ToString();

            return expected;
        }

        /// <summary>
        /// Returns the actual string of the read stream
        /// </summary>
        private String ActualMessage(UInt32 messageId, UInt32 versionOfProtocol, UInt32 tcpPort, String machineId, MulticastMessage message)
        {
            MemoryStream stream = new MemoryStream();
            WireWriter writer = new WireWriter(stream);

            writer.Write(messageId);
            writer.Write(versionOfProtocol);
            writer.Write(tcpPort);
            writer.WriteString(machineId);
            stream.Position = 0;

            var actual = message.Read(new WireReader(stream)).ToString();

            return actual;
        }

        [Test]
        public void Write_NullWireWriter_WriterNullException()
        {
            MulticastMessage multicast = new MulticastMessage();
            WireWriter writer = null;

            var exception = Assert.Throws<ArgumentNullException>(code: () => multicast.Write(writer));

            Assert.That(actual: exception.ParamName, expression: Is.EqualTo("WriterNullException"));
        }

        [Test]
        public void Ctor_NormalInput_StringEqual()
        {
            var expected = ExpectedMessage(messageId: 1111, versionOfProtocol: 1, tcpPort: 17500, machineId: "001");

            MulticastMessage multicast = new MulticastMessage(messageId: 1111, tcpPort: 17500, machineId: "001");

            var actual = multicast.ToString();

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Read_NullWireReader_ReaderNullException()
        {
            MulticastMessage Multicast = new MulticastMessage();
            WireReader reader = null;

            var exception = Assert.Throws<ArgumentNullException>(code: () => Multicast.Read(reader));

            Assert.That(actual: exception.ParamName, expression: Is.EqualTo("ReaderNullException"));
        }

        [Test]
        public void Read_NormalInput_StringEqual()
        {
            MulticastMessage message = new MulticastMessage(messageId: 1111, tcpPort: 17500, machineId: "001");

            var expected = message.ToString();

            var actual = ActualMessage(messageId: 1111, versionOfProtocol: 1, tcpPort: 17500, machineId: "001", message);

            Assert.AreEqual(expected, actual);
        }
    }
}
