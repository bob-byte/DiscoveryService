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
        public void Writer_null()
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
            uint MessageId = 1111;
            uint VersionOfProtocol = 1; 
            uint TcpPort = 17500;
            String MachineId = "001";
            var writ = new StringWriter();
            writ.WriteLine("Multicast message:");
            writ.WriteLine($"MessageId = {MessageId};\n" +
                             $"MachineId = {MachineId};\n" +
                             $"Tcp port = {TcpPort};\n" +
                             $"Protocol version = {VersionOfProtocol}");

            MulticastMessage Multicast = new MulticastMessage(MessageId, MachineId, TcpPort);
            Assert.AreEqual(writ.ToString(), Multicast.ToString());
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
            uint MessageId = 1111;
            String MachineId = "001";
            uint TcpPort = 17500;
            uint VersionOfProtocol = 1;
            MulticastMessage mult= new MulticastMessage(MessageId, MachineId, TcpPort);
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
