using System;
using System.Net;

namespace LUC.DiscoveryService.Messages
{
    internal struct TcpReceiveResult
    {
        public TcpReceiveResult(Byte[] buffer, IPEndPoint remoteEndPoint)
        {
            Buffer = buffer;
            RemoteEndPoint = remoteEndPoint;
        }

        public Byte[] Buffer { get; }
        public IPEndPoint RemoteEndPoint { get; }
    }
}
