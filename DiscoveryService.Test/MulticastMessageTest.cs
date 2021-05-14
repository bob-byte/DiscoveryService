using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DiscoveryService.Test
{
    /// <summary>
    /// Сводное описание для UnitTest3
    /// </summary>
    [TestClass]
    public class MulticastMessageTest
    {
        [TestMethod]
        public void Writer_Null()
        {
            MulticastMessage Multicast = new MulticastMessage();
            MemoryStream ms = new MemoryStream();
            WireWriter writer = null;
            var ex = Assert.ThrowsException<ArgumentNullException>(() => Multicast.Write(writer));
            NUnit.Framework.Assert.That(ex.ParamName, NUnit.Framework.Is.EqualTo("writer"));
        }

        [TestMethod]
        public void Message()
        {
            UInt32 messageId = 1111;
            UInt32 versionOfProtocol = 1;
            UInt32 tcpPort = 17500;
            String machineId = "001";
            var writer = new StringWriter();
            MulticastMessage multicast;

            writer.WriteLine("Multicast message:");
            writer.WriteLine($"MessageId = {messageId};\n" +
                             $"Tcp port = {tcpPort};\n" +
                             $"Protocol version = {versionOfProtocol};\r\n" +
                             $"MachineId = {machineId}");
            var expected = writer.ToString();
            multicast = new MulticastMessage(messageId, tcpPort, machineId);
            var actual = multicast.ToString();

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void Reader_null()
        {
            MulticastMessage Multicast = new MulticastMessage();
            MemoryStream ms = new MemoryStream();
            WireReader reader = null;
            var ex = Assert.ThrowsException<ArgumentNullException>(() => Multicast.Read(reader));
            NUnit.Framework.Assert.That(ex.ParamName, NUnit.Framework.Is.EqualTo("reader"));
        }

        [TestMethod]
        public void Reader()
        {
            UInt32 MessageId = 1111;
            String MachineId = "001";
            UInt32 TcpPort = 17500;
            UInt32 VersionOfProtocol = 1;
            MulticastMessage mult= new MulticastMessage(MessageId, TcpPort, MachineId);
            MemoryStream ms = new MemoryStream();
            WireWriter writer = new WireWriter(ms);
            writer.Write(MessageId);
            writer.Write(VersionOfProtocol);
            writer.Write(MachineId);
            writer.Write(TcpPort);
            ms.Position=0;
            Assert.AreEqual(mult.ToString(), mult.Read(new WireReader(ms)).ToString());
        }
    }
}
