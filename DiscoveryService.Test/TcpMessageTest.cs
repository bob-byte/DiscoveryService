using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Messages;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace DiscoveryService.Test
{
    /// <summary>
    /// Сводное описание для UnitTest4
    /// </summary>
    [TestFixture]
    public class TcpMessageTest
    {
        [Test]
        public void Writer_null()
        {
            TcpMessage tcp = new TcpMessage();
            MemoryStream ms = new MemoryStream();
            WireWriter writer = null;

            var exception = Assert.Throws<ArgumentNullException>(() => tcp.Write(writer));

            Assert.That(exception.ParamName, Is.EqualTo(nameof(writer)));
        }

        [Test]
        public void Message()
        {
            UInt32 messageId = 1111;
            UInt32 versionOfProtocol = 1;
            UInt32 tcpPort = 17500;
            List<String> groupsIds = new List<String> { "a", "b" };
            var expected = ExpectedMessage(messageId, versionOfProtocol, tcpPort, groupsIds);
            var message = new TcpMessage(messageId, tcpPort, groupsIds);

            var actual = message.ToString();

            Assert.AreEqual(expected, actual);
        }

        private String ExpectedMessage(UInt32 messageId, UInt32 versionOfProtocol, UInt32 tcpPort, List<String> groupsIds)
        {
            var writer = new StringWriter();

            writer.WriteLine("TCP message:");
            writer.WriteLine($"MessageId = {messageId};\n" +
                             $"Tcp port = {tcpPort};\n" +
                             $"Protocol version = {versionOfProtocol};");
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
        public void Message_GroupsIsNULL()
        {
            UInt32 messageId = 1111;
            UInt32 versionOfProtocol = 1;
            UInt32 tcpPort = 17500;
            List<String> groupsIds = new List<String>();
            var expected = ExpectedMessage(messageId, versionOfProtocol, tcpPort, groupsIds);
            var message = new TcpMessage(messageId, tcpPort, groupsIds: null);

            var actual = message.ToString();

            Assert.AreEqual(expected, actual);
        }
    }
}
