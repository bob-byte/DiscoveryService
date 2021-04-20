using LUC.Interfaces;
using LUC.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Security.Cryptography.X509Certificates;

namespace DiscoveryServices
{
    public class DiscoveryService
    {
        private const Int32 MillisecondsPerSecond = 1000;
        private const Int32 PeriodSendingsInMs = 60 * MillisecondsPerSecond;
        private const Int32 ReceiveTimeoutInMs = 5 * MillisecondsPerSecond;

        private const Int32 CountDataToListen = 256;

        private const Int32 MinValuePort = 17500;
        private const Int32 MaxValuePort = 17510;

        private static DiscoveryService instance;

        private static Peer currentPeer;
        private static Client client;
        private static Server server;

        //Value of this field should get externally so we use this attribute
        [Import(typeof(INotifyService))]
        private ILoggingService loggingService;

        private CancellationTokenSource tokenSourceInnerClient, tokenSourceOuterClient;
        private CancellationTokenSource tokenSourceInnerTcpClient, tokenSourceOuterTcpClient;
        private CancellationTokenSource tokenSourceInnerServer, tokenSourceOuterServer;

        private Boolean isDiscoveryServiceStarted = false;

        private DiscoveryService(List<String> groupsSupported)
        {
            Lock.InitWithLock(Lock.lockCurrentPeer, new Peer(groupsSupported, new X509Certificate(), MinValuePort, MaxValuePort), ref currentPeer);
            //X509Store keyStore = new X509Store(StoreName.My, )

            client = new Client(currentPeer, loggingService);
            server = new Server(currentPeer, loggingService);           

            loggingService = new LoggingService();
        }

        /// <summary>
        /// It creates instance to discover others peer in local network
        /// </summary>
        /// <param name="groupsSupported">
        /// Groups which current peer supports
        /// </param>
        /// <returns>
        /// Object to discover other peers. You can't get different instance, so it doesn't have sense to use this method more than 1 time
        /// </returns>
        public static DiscoveryService GetInstance(List<String> groupsSupported)
        {
            Lock.InitWithLock(Lock.lockService, new DiscoveryService(groupsSupported), ref instance);
            return instance;
        }

        public Dictionary<String, IPAddress> KnownPeers 
        {
            get
            {
                lock(Lock.lockChangeKnownPeers)
                {
                    return currentPeer.KnownOtherPeers;
                }
            }
        }
        
        /// <summary>
        /// It starts discovery others peer in local network
        /// </summary>
        /// 
        /// <param name="machineId">
        /// Id of current machine
        /// </param>
        public void Start(out String machineId)
        {
            if(isDiscoveryServiceStarted)
            {
                throw new Exception("Already started");
            }

            tokenSourceOuterClient = new CancellationTokenSource();
            var tokenClient = tokenSourceOuterClient.Token;
            tokenSourceInnerClient = new CancellationTokenSource();
            
            Task.Run(async () =>
            {
                while (!tokenClient.IsCancellationRequested)
                {
                    await client.SendingBroadcast(PeriodSendingsInMs, tokenSourceInnerClient, tokenClient);

                    //if we don't initialize this, token will have property IsCancellationRequested equal to true
                    tokenSourceInnerClient = new CancellationTokenSource();
                }
            }, tokenClient);

            tokenSourceOuterTcpClient = new CancellationTokenSource();
            var tokenTcpClient = tokenSourceInnerTcpClient.Token;
            tokenSourceInnerTcpClient = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while(!tokenTcpClient.IsCancellationRequested)
                {
                    await client.ListenTcpMessage(ReceiveTimeoutInMs, tokenSourceInnerTcpClient);

                    //if we don't initialize this, token will have property IsCancellationRequested equal to true
                    tokenSourceInnerTcpClient = new CancellationTokenSource();
                }
            }, tokenTcpClient);

            tokenSourceOuterServer = new CancellationTokenSource();
            var tokenServer = tokenSourceOuterClient.Token;
            tokenSourceInnerServer = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (!tokenServer.IsCancellationRequested)
                {
                    await server.ListeningBroadcast(tokenSourceInnerServer, CountDataToListen, ReceiveTimeoutInMs);

                    //if we don't initialize this, token will have property IsCancellationRequested equal to true
                    tokenSourceInnerServer = new CancellationTokenSource();
                }
            }, tokenServer);

            machineId = currentPeer.Id;
            isDiscoveryServiceStarted = true;
        }

        public void Stop()
        {
            if(isDiscoveryServiceStarted)
            {
                //Stop inner tasks of sending and listening
                tokenSourceInnerClient.Cancel();
                tokenSourceInnerTcpClient.Cancel();
                tokenSourceInnerServer.Cancel();

                //Stop outer tasks of sending and listening
                tokenSourceOuterClient.Cancel();
                tokenSourceOuterTcpClient.Cancel();
                tokenSourceOuterServer.Cancel();

                isDiscoveryServiceStarted = false;
            }
        }
    }
}
