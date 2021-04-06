using DiscoveryServices.CodingData;
using DiscoveryServices.Extensions;
using DiscoveryServices.Extensions.IPExtensions;
using DiscoveryServices.Messages;
using DiscoveryServices.Protocols;
using LUC.Interfaces;
using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscoveryServices
{
    class Server
    {
        private Peer currentPeer;
        private ILoggingService loggingService;
        Task taskServer = null;

        public static String Id { get; set; }

        public Server(Peer peer, ILoggingService loggingService)
        {
            currentPeer = peer;
            this.loggingService = loggingService;
        }

        /// <summary>
        /// It listens broadcast in a different task until it will be canceled or will have an exception. 
        /// Also it adds info about remote peer wherefrom we get package to KnowPeers property
        /// </summary>
        /// 
        /// <returns>
        /// It returns task which listened broadcast package
        /// </returns>
        internal Task ListeningBroadcast(CancellationTokenSource innerTokenSource, Int32 countDataToListen, Int32 timeout)
        {
            var innerToken = innerTokenSource.Token;

            taskServer = Task.Run(() =>
            {
                while (taskServer.WhetherToContinueTask(innerToken))
                {
                    IPEndPoint newClient = new IPEndPoint(currentPeer.IpAddress, currentPeer.RunningPort);

                    try
                    {
                        Byte[] bytes = new Byte[countDataToListen];
                        Broadcast udpClient = new Broadcast();
                        if (NetworkInterface.GetIsNetworkAvailable())
                        {
                            udpClient.Listen(bytes, newClient, timeout);
                            //Parsing<BroadcastMessage> parsingBroadcast = new ParsingBroadcastData();
                            //var receivedMessage = parsingBroadcast.GetEncodedData(bytes);

                            SendTcpSsl(newClient);
                        }
                        else
                        {
                            //any network interface is not available
                            throw new NetworkInformationException(SystemErrorCode.ErrorAdapHdwErr);
                        }

                        String package = Encoding.ASCII.GetString(bytes);
                        //loggingService.LogInfo(package);
                    }
                    catch (Exception ex)
                    {
                        innerTokenSource.Cancel();
                        //loggingService.LogError(ex, ex.Message);

                        currentPeer.RunningPort++;
                    }
                }
            }, innerToken);

            return taskServer;
        }

        private void SendTcpSsl(IPEndPoint endPoint)
        {
            var parsingSsl = new ParsingSslTcpData();
            Byte[] bytes = parsingSsl.GetDecodedData(new SslTcpMessage(currentPeer.GroupsSupported));
            SslTcp sslTcp = new SslTcp();
            sslTcp.Send(endPoint, currentPeer.Certificate, bytes);
        }
    }
}
