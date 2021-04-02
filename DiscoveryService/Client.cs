using DiscoveryServices.Extensions.IPExtensions;
using DiscoveryServices.Protocols;
using LUC.Interfaces;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DiscoveryServices
{
    class Client
    {
        /// <summary>
        /// It sends a broadcast message, which contains info of current peer
        /// </summary>
        /// 
        /// <param name="ipAddressClass">
        /// Class of IP address where the package will be sent
        /// </param>
        /// <param name="subnetMask">
        /// It indicates how many peers will get this package
        /// </param>
        /// 
        /// <param name="currentPeer">
        /// Info of peer which this method will send in package
        /// </param>
        internal void SendBroadcastMessage(IPAddress ipAddressClass, IPAddress subnetMask, Peer currentPeer)
        {
            Broadcast udpClient = new Broadcast();
            try
            {
                var bytes = Parsing<Peer>.GetDecodedData(currentPeer);

                udpClient.Send(ipAddressClass, subnetMask, currentPeer.RunningPort, bytes);
            }
            catch
            {
                throw;
            }
        }
    }
}
