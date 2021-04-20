using DiscoveryServices.CodingData;
using DiscoveryServices.Extensions;
using DiscoveryServices.Extensions.IPExtensions;
using DiscoveryServices.Messages;
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
        private Peer currentPeer;
        private ILoggingService loggingService;

        public static String Id { get; set; }

        public Client(Peer peer, ILoggingService loggingService)
        {
            currentPeer = peer;
            this.loggingService = loggingService;
        }

        public Task ListenTcpMessage(Int32 timeout, CancellationTokenSource tokenSource)
        {
            var token = tokenSource.Token;

            var task = Task.Run(() =>
            {
                SslTcp sslTcp = new SslTcp();

                do
                {
                    try
                    {
                        sslTcp.StartListening(new IPEndPoint(currentPeer.IpAddress, currentPeer.RunningPort));
                        var bytes = sslTcp.Message(currentPeer.Certificate, timeout);

                        ParsingBroadcastData parsing = new ParsingBroadcastData();
                        var sslTcpMessage = parsing.GetEncodedData(bytes);
                    }
                    catch
                    {
                        tokenSource.Cancel();
                    }
                    //AddNewPeer();
                }
                while (!tokenSource.IsCancellationRequested);

                sslTcp.StopListening();

            }, token);

            return task;
        }


        private void AddNewPeer(String id, IPAddress ipAddress)
        {
            var isAddedToKnownPeers = currentPeer.KnownOtherPeers.ContainsKey(id);
            var messToYourself = currentPeer.Id.Equals(id);

            if ((!isAddedToKnownPeers) && (!messToYourself))
            {
                currentPeer.KnownOtherPeers.Add(id, ipAddress);
            }
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
        internal Task SendingBroadcast(Int32 period, CancellationTokenSource innerTokenSource, CancellationToken tokenOuter)
        {
            CancellationToken token = innerTokenSource.Token;
            Task taskClient = null;

            taskClient = Task.Run(() =>
            {
                while (taskClient.WhetherToContinueTask(token))
                {
                    Broadcast udpClient = new Broadcast();
                    try
                    {
                        Parsing<BroadcastMessage> parsing = new ParsingBroadcastData();
                        var bytes = parsing.GetDecodedData(new BroadcastMessage(currentPeer.Id, currentPeer.RunningPort));

                        udpClient.Send(IPAddress.Broadcast, SubnetMask.ClassC, currentPeer.RunningPort, bytes);
                    }
                    catch (Exception ex)
                    {
                        innerTokenSource.Cancel();
                        //loggingService.LogError(ex, ex.Message);
                    }

                    //if outer task is cancelled we stop current task
                    var cancelled = tokenOuter.WaitHandle.WaitOne(period);
                    if (cancelled)
                    {
                        break;
                    }
                }
            }, token);

            return taskClient;
        }
    }
}
