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
    /// Сводное описание для UnitTest4
    /// </summary>
    [TestClass]
    public class TcpMessageTest
    {
        [TestMethod]
        public void Writer_null()
        {
            TcpMessage tcp = new TcpMessage();
            MemoryStream ms = new MemoryStream();
            WireWriter writer = null;
            var ex = Assert.ThrowsException<ArgumentNullException>(() => tcp.Write(writer));
            NUnit.Framework.Assert.That(ex.ParamName, NUnit.Framework.Is.EqualTo("writer"));
        }
        [TestMethod]
        public void Message()
        {
            uint MessageId = 1111;
            uint VersionOfProtocol = 2;
            List<string> GroupsIds=new List<string>{"a","b" };
            var writ = new StringWriter();
            writ.WriteLine("TCP message:");
            writ.WriteLine($"MessageId = {MessageId};\n" +
                           $"Protocol version = {VersionOfProtocol};");
            writ.WriteLine($"{nameof(GroupsIds)}:");

            for (Int32 id = 0; id < GroupsIds.Count; id++)
            {
                if (id == GroupsIds.Count - 1)
                {
                    writ.WriteLine($"{GroupsIds[id]}");
                }
                else
                {
                    writ.WriteLine($"{GroupsIds[id]};");
                }
            }

            var tcp = new TcpMessage(MessageId, VersionOfProtocol, GroupsIds);
            Assert.AreEqual(writ.ToString(), tcp.ToString());
        }
        [TestMethod]
        public void Message_GroupsIsNULL()
        {
           uint MessageId = 1111;
           uint VersionOfProtocol = 2;
           List <string>  GroupsIds = new List<string>();
            var writ = new StringWriter();
                writ.WriteLine( "TCP message:");
                writ.WriteLine($"MessageId = {MessageId};\n" +
                               $"Protocol version = {VersionOfProtocol};");
                writ.WriteLine($"{nameof(GroupsIds)}:");
            
            for (Int32 id = 0; id < GroupsIds.Count; id++)
            {
                if (id == GroupsIds.Count - 1)
                {
                    writ.WriteLine($"{GroupsIds[id]}");
                }
                else
                {
                    writ.WriteLine($"{GroupsIds[id]};");
                }
            }

            var tcp = new TcpMessage(MessageId, VersionOfProtocol,null);
            Assert.AreEqual(writ.ToString(), tcp.ToString());
        }
    }
}
