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
        private const Int32 PeriodSendingsInMs = 60 * 1000;

        private static DiscoveryService instance;

        private static Peer currentPeer;
        private static Client client;
        private static Server server;

        private Task taskClient;
        private Task taskServer;

        //Value of this field should get externally so we use this attribute
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
            Lock.InitWithLock(Lock.lockerService, new DiscoveryService(groupsSupported), ref instance);
            return instance;
        }

        public Dictionary<String, List<IPAddress>> KnownPeers { get; } = new Dictionary<String, List<IPAddress>>();
        
        /// <summary>
        /// It starts discovery others peer in local network
        /// </summary>
        /// 
        /// <param name="machineId">
        /// Id of current machine
        /// </param>
        public void Start(out String machineId)
        {
            tokenSourceOuterClient = new CancellationTokenSource();
            var tokenClient = tokenSourceOuterClient.Token;
            
            Task.Run(async () =>
            {
                while (!tokenClient.IsCancellationRequested)
                {
                    await SendingBroadcast(tokenClient);
                }
            }, tokenClient);


            tokenSourceOuterServer = new CancellationTokenSource();
            var tokenServer = tokenSourceOuterClient.Token;
            Task.Run(async () =>
            {
                while (!tokenServer.IsCancellationRequested)
                {
                    await ListeningBroadcast();
                }
            }, tokenServer);

            machineId = currentPeer.Id;
        }


        /// <summary>
        /// It sends broadcast package asynchronously to 255 PC of the IP address class C
        /// </summary>
        /// 
        /// <param name="tokenOuter">
        /// Token of the outer task, which run this method
        /// </param>
        /// 
        /// <returns>
        /// Task which was canceled or has any exception
        /// </returns>
        private Task SendingBroadcast(CancellationToken tokenOuter)
        {
            tokenSourceInnerClient = new CancellationTokenSource();
            CancellationToken token = tokenSourceInnerClient.Token;

            taskClient = Task.Run(() =>
            {
                while (WhetherToContinueTask(taskClient, token))
                {
                    try
                    {
                        client.SendBroadcastMessage(IPAddressClass.ClassC, SubnetMask.ClassC, currentPeer);
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

        private Boolean WhetherToContinueTask(Task task, CancellationToken token)
        {
            var isThisTaskCanceled = task.Status == TaskStatus.Canceled;
            var hasThisTaskException = task.Status == TaskStatus.Faulted;
            
            return ((!isThisTaskCanceled) && (!hasThisTaskException) && !token.IsCancellationRequested);
        }

        /// <summary>
        /// It listens broadcast asynchronously in a different task until it will be canceled or has an exception. 
        /// Also it adds info about remote peer wherefrom we get package to KnowPeers property
        /// </summary>
        /// 
        /// <returns>
        /// It returns task which listened broadcast package
        /// </returns>
        private Task ListeningBroadcast()
        {
            tokenSourceInnerServer = new CancellationTokenSource();
            var token = tokenSourceInnerServer.Token;

            taskServer = Task.Run(() =>
            {
                while (WhetherToContinueTask(taskServer, token))
                {
                    IPEndPoint newClient = null;
                    Peer peerWhereFromGetPackage = null;

                    try
                    {
                        Byte[] bytes = null;
                        server.ListenBroadcast(IPAddress.Any, currentPeer.RunningPort, out newClient, out bytes, out peerWhereFromGetPackage);

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
            //Stop inner task of sending and listening appropriately
            tokenSourceInnerClient.Cancel();
            tokenSourceInnerServer.Cancel();

            //Stop outer task of sending and listening appropriately
            tokenSourceOuterClient.Cancel();
            tokenSourceOuterServer.Cancel();
        }
    }
}
