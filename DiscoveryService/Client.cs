using DiscoveryServices.Protocols;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscoveryServices
{
    public class Client
    {
        private String ipNetwork;

        public String Id { get; private set; }
        public ProtocolVersion UsedProtocol { get; }
        public List<IPAddress> GroupsSupported { get; set; }

        //private Udp udpClient;

        public Client(String ipNetwork, List<IPAddress> groupsSupported, ProtocolVersion usedProtocol, String id)
        {
            this.ipNetwork = ipNetwork;
            GroupsSupported = groupsSupported;
            UsedProtocol = usedProtocol;
            Id = id;
        }

        public void SendPackages()
        {
            Broadcast udpClient = new Broadcast();
            Package package = new Package();

            var containedPackage = package.GetContainedOfPacket(UsedProtocol, GroupsSupported, 17500, Id);
            var bytes = Encoding.ASCII.GetBytes(containedPackage);

            udpClient.SendBroadcastToAll(bytes, ipNetwork);
        }

        //It is not used
        public void SendPacketsPeriodically(TimeSpan period, String ipNetwork)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    Broadcast udpClient = new Broadcast();
                    Package package = new Package();

                    var containedPackage = package.GetContainedOfPacket(UsedProtocol, GroupsSupported, 17500, Id);
                    var bytes = Encoding.ASCII.GetBytes(containedPackage);
                    
                    udpClient.SendBroadcastToAll(bytes, ipNetwork);
                    await Task.Delay(period);
                }
            });
        }

        

        //static Client()
        //{
        //    Id = $"{GetUniqueMachineId()} - {GetRandomSymbols()}";
        //    if(isConnected)
        //    {
        //        SendBroadcast();

        //    }
        //}

        //public void ConnectToServer(ServerType serverType)
        //{
        //    var serverHostName = Host.GetHostName(serverType);

        //    if(serverType != ServerType.InvalidType)
        //    {
        //        Broadcast.Connect(Server.GetHostName(serverType), Port);
        //    }
        //    else
        //    {
        //        throw new LocalNetworkException(NetworkC);
        //    }
        //}

        //public void RemoveConnectionToServer()
        //{
        //    if(Sender.IsClientAddedToServer(Id))
        //    {
        //        Tcp.SendTo(serverType, Id, TypeMessage.RemoveConnectionToServer);
        //    }
        //    else
        //    {
        //        throw new LocalNetworkException(ErrorCode.ErrorInvalidParameter);
        //    }
        //}

        //public FileInfo[] HttpGetNewFiles()
        //{

        //}

        //void Try()
        //{
        //    IPAddress iPAddress = new IPAddress()
        //}
    }
}
