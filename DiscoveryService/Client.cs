using DiscoveryServices.Protocols;
using System.Net;

namespace DiscoveryServices
{
    class Client
    {
        public void SendPackages(IPAddress ipNetwork, IPAddress subnetMask, Peer peer)
        {
            Broadcast udpClient = new Broadcast();
            try
            {
                var bytes = Parsing<Peer>.GetDecodedData(peer);

                udpClient.Send(ipNetwork, subnetMask, peer.RunningPort, bytes);
            }
            catch
            {
                throw;
            }
        }
    }
}
