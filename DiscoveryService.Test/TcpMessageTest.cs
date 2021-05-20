using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Messages;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace DiscoveryService.Test
{
    /// <summary>
    /// TcpMessageTest
    /// </summary>
    [TestFixture]
    public class TcpMessageTest
    {
        private String ExpectedMessage(UInt32 messageId, UInt32 versionOfProtocol, UInt32 kadPort, List<String> groupsIds)
        {
            var writer = new StringWriter();

            writer.WriteLine("TCP message:");
            writer.WriteLine($"MessageId = {messageId};\n" +
                             $"Protocol version = {versionOfProtocol};\r\n" +
                             $"TCP port of the Kademilia service = {kadPort};");

            writer.WriteLine($"GroupIds:");
            for (Int32 id = 0; id < groupsIds.Count; id++)
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
        public void Write_NullWireWriter_WriterNullException()
        {
            var tcp = new TcpMessage();

            var exception = Assert.Throws<ArgumentNullException>(code: () => tcp.Write(writer: null));

            Assert.That(actual: exception.ParamName, expression: Is.EqualTo("WriterNullException"));
        }

        [Test]
        public void Ctor_NormalInput_StringEqual()
        {
            var expected = ExpectedMessage(messageId: 1111, versionOfProtocol: 1, kadPort: 17500, groupsIds: new List<String> { "a", "b" });

            var message = new TcpMessage(messageId: 1111, kadPort: 17500, new List<String> { "a", "b" });

            var actual = message.ToString();

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Ctor_NullGroupIds_StringEqual()
        {
            var expected = ExpectedMessage(messageId: 1111, versionOfProtocol: 1, kadPort: 17500, groupsIds: null);

            var message = new TcpMessage(messageId: 1111, kadPort: 17500, groupsIds: null);

            var actual = message.ToString();

            Assert.AreEqual(expected, actual);
        }
    }
}
