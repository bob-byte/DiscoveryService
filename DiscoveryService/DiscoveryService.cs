using DeviceId;
using DiscoveryServices.Extensions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DiscoveryServices
{
    public class DiscoveryService
    {
        private static DiscoveryService instance;
        private static Client client;
        private static Server server;

        private static Object locker = new Object();
        private Task taskClient;
        private Thread threadServer;

        String ipNetwork;

        public List<IPEndPoint> RecognizedPeers { get; private set; }
        public String Id { get; }

        private DiscoveryService(String ipNetwork, List<IPAddress> groupsSupported, ProtocolVersion protocolVersion)
        {
            this.ipNetwork = ipNetwork;

            DeviceIdBuilder deviceIdBuilder = new DeviceIdBuilder();
            Random random = new Random();
            Id = $"{deviceIdBuilder.GetDeviceId()}-{random.GenerateRandomSymbols(5)}";

            client = new Client(ipNetwork, groupsSupported, protocolVersion, Id);
            server = new Server();
        }

        public static DiscoveryService GetInstance(String ipNetwork, List<IPAddress> groupsSupported, ProtocolVersion protocolVersion)
        {
            if (instance == null)
            {
                lock (locker)
                {
                    if (instance == null)
                    {
                        instance = new DiscoveryService(ipNetwork, groupsSupported, protocolVersion);
                    }
                }
            }

            return instance;
        }
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public void Start(out String machineId)
        {
            CancellationToken token = cancellationTokenSource.Token;
            
            taskClient = new Task(async () => 
            {
                client.SendPackages();
                await Task.Delay(new TimeSpan(0, 1, 0));
            }, token);
            //threadClient = new Thread(new ThreadStart(() => client.SendPacketsPeriodically(RecognizedPeers, new TimeSpan(0, 1, 0), ipNetwork)));
            taskClient.Start();

            threadServer = new Thread(new ThreadStart(() =>
            {
                while(true)
                {
                    server.ListenBroadcast(out IPEndPoint newClient);
                    RecognizedPeers.Add(newClient);
                }
            }));
            threadServer.Start();

            machineId = Id;
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            //var statusTask = taskClient.Status;
            threadServer.Abort();
        }
    }
}
