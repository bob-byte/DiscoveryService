using DiscoveryServices.Extensions.IPExtensions;
using LUC.Interfaces;
using LUC.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Text;

namespace DiscoveryServices
{
    public class DiscoveryService
    {
        private const Int32 PeriodSendingsInMs = /*60 * */1000;

        private static DiscoveryService instance;

        private static Peer currentPeer;
        private static Client client;
        private static Server server;

        private Task taskClient;
        private Task taskServer;

        [Import(typeof(INotifyService))]
        private ILoggingService loggingService;

        private CancellationTokenSource tokenSourceInnerClient, tokenSourceOuterClient;
        private CancellationTokenSource tokenSourceInnerServer, tokenSourceOuterServer;

        private DiscoveryService(List<String> groupsSupported)
        {
            Lock.InitWithLock(Lock.lockerCurrentPeer, new Peer(groupsSupported), ref currentPeer);

            client = new Client();
            server = new Server();
            

            loggingService = new LoggingService();
        }

        public static DiscoveryService GetInstance(List<String> groupsSupported)
        {
            Lock.InitWithLock(Lock.lockerService, new DiscoveryService(groupsSupported), ref instance);
            return instance;
        }

        public Dictionary<String, List<IPAddress>> KnownPeers { get; } = new Dictionary<String, List<IPAddress>>();
        
        public void Start(out String machineId)
        {
            tokenSourceOuterClient = new CancellationTokenSource();
            var tokenClient = tokenSourceOuterClient.Token;
            
            Task.Run(async () =>
            {
                while (!tokenClient.IsCancellationRequested)
                {
                    await Sending(tokenClient);
                }
            }, tokenClient);


            tokenSourceOuterServer = new CancellationTokenSource();
            var tokenServer = tokenSourceOuterClient.Token;
            Task.Run(async () =>
            {
                while (!tokenServer.IsCancellationRequested)
                {
                    await Listening();
                }
            }, tokenServer);

            machineId = currentPeer.Id;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="tokenOuter"></param>
        /// <returns></returns>
        private Task Sending(CancellationToken tokenOuter)
        {
            tokenSourceInnerClient = new CancellationTokenSource();
            CancellationToken token = tokenSourceInnerClient.Token;

            taskClient = Task.Run(() =>
            {
                while (taskClient.Status != TaskStatus.Canceled && taskClient.Status != TaskStatus.Faulted && !token.IsCancellationRequested)
                {
                    try
                    {
                        //ConfigureAwait(false) allows us to use thread pool in GUI
                        //var sendUsingClassA = Task.Run(() => client.SendPackages(IPAddressClass.ClassA, SubnetMask.ClassC, currentPeer))/*.ConfigureAwait(false)*/;
                        //var sendUsingClassB = Task.Run(() => client.SendPackages(IPAddressClass.ClassB, SubnetMask.ClassC, currentPeer))/*.ConfigureAwait(false)*/;
                        var sendUsingClassC = Task.Run(() => client.SendPackages(IPAddressClass.ClassC, SubnetMask.ClassC, currentPeer))/*.ConfigureAwait(false)*/;

                        Task.WaitAll(sendUsingClassC);
                    }
                    catch (Exception ex)
                    {
                        tokenSourceInnerClient.Cancel();

                        loggingService.LogError(ex, ex.Message);
                    }

                    //if outer task is cancelled we stop current task
                    var cancelled = tokenOuter.WaitHandle.WaitOne(PeriodSendingsInMs);
                    if (cancelled)
                    {
                        break;
                    }
                }
            }, token);

            return taskClient;
        }

        private Task Listening()
        {
            tokenSourceInnerServer = new CancellationTokenSource();
            var token = tokenSourceInnerServer.Token;

            taskServer = Task.Run(async () =>
            {
                while (taskServer.Status != TaskStatus.Canceled && taskServer.Status != TaskStatus.Faulted && !token.IsCancellationRequested)
                {
                    IPEndPoint newClient = null;
                    Peer peerWhereFromGetPackage = null;
                    try
                    {
                        Byte[] bytes = null;
                        await Task.Run(() => server.ListenBroadcast(IPAddress.Any, currentPeer.RunningPort, out newClient, out bytes, out peerWhereFromGetPackage)).ConfigureAwait(false);

                        String package = Encoding.ASCII.GetString(bytes);
                        //loggingService.LogInfo(package);
                    }
                    catch(Exception ex)
                    {
                        tokenSourceInnerServer.Cancel();
                        loggingService.LogError(ex, ex.Message);

                        currentPeer.RunningPort++;
                    }

                    var isAddedToKnownPeers = KnownPeers.ContainsKey(peerWhereFromGetPackage.Id);
                    var messToYourself = currentPeer.Id.Equals(peerWhereFromGetPackage.Id);
                    if ((!isAddedToKnownPeers) && (!messToYourself))
                    {
                        KnownPeers.Add(peerWhereFromGetPackage.Id, peerWhereFromGetPackage.IpAddresses);
                    }
                }
            }, token);

            return taskServer;
        }

        public void Stop()
        {
            tokenSourceInnerClient.Cancel();
            tokenSourceInnerServer.Cancel();

            tokenSourceOuterClient.Cancel();
            tokenSourceOuterServer.Cancel();
        }
    }
}
