using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Test
{
    [TestFixture]
    class DiscoveryServiceSocketTest
    {
        private DiscoveryServiceSocket dsSocket = new DiscoveryServiceSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp, Logging.log); 

        [Test]
        public void Connect_RemoteEndPointIsNull_GetException()
        {
            Assert.That(() => 
                    dsSocket.Connect(remoteEndPoint: null, TimeSpan.FromSeconds(1)), 
                    Throws.TypeOf(typeof(ArgumentNullException)));
        }

        [Test]
        public void ConnectAsync_RemoteEndPointIsNull_GetException()
        {
            Assert.That(async () =>
                    await dsSocket.ConnectAsync(remoteEndPoint: null, TimeSpan.FromSeconds(1)),
                    Throws.TypeOf(typeof(ArgumentNullException)));
        }
    }
}
