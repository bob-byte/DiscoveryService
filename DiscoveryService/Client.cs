using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Threading.Tasks;
using LUC.DiscoveryService.Extensions;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   Allows sending and receiving datagrams over multicast sockets.
    ///
    ///    Also listens on TCP/IP port for receiving side to establish connection.
    /// </summary>
    class Client : AbstractService, IDisposable
    {
        private const Int32 BackLog = Int32.MaxValue;
        private const Int32 TcpMaxSockets = 16;

        private static readonly IPAddress MulticastAddressIp4 = IPAddress.Parse("224.0.0.251");
        private readonly IPEndPoint MulticastEndpointIp4;

        private static readonly IPAddress MulticastAddressIp6 = IPAddress.Parse("FF02::FB");
        private readonly IPEndPoint MulticastEndpointIp6;

        private readonly List<UdpClient> udpReceivers;
        private readonly List<DiscoveryServiceSocket> tcpReceivers;

        private readonly ConcurrentDictionary<IPAddress, UdpClient> sendersUdp = new ConcurrentDictionary<IPAddress, UdpClient>();

        //public delegate void MessageHandler(Socket receiver, M messageArgs);

        /// <summary>
        /// It calls method OnUdpMessage, which run SendTcp,
        /// in order to connect back to the host, that sends muticast
        /// </summary>
        public event EventHandler<UdpMessageEventArgs> UdpMessageReceived;

        /// <summary>
        /// It calls method OnTcpMessage, which add new groups to ServiceDiscovery.GroupsSupported
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> TcpMessageReceived;

        /// <summary>
        ///   Creates a new instance of the <see cref="Client"/> class.
        /// </summary>
        /// <param name="profile">
        /// Info about current peer
        /// </param>
        /// <param name="useIpv4">
        /// Send and receive on IPv4.
        /// <value>
        ///   Defaults to <b>true</b> if the OS supports it.
        /// </value>
        /// </param>
        /// <param name="useIpv6">
        /// Send and receive on IPv6.
        /// <value>
        ///   Defaults to <b>true</b> if the OS supports it.
        /// </value>
        /// </param>
        /// <param name="runningIpAddresses">
        /// NetworkInterfaces wherefrom we should send to
        /// </param>
        public Client(Boolean useIpv4, Boolean useIpv6, ICollection<IPAddress> runningIpAddresses)
        {
            MulticastEndpointIp4 = new IPEndPoint(MulticastAddressIp4, (Int32)RunningUdpPort);
            MulticastEndpointIp6 = new IPEndPoint(MulticastAddressIp6, (Int32)RunningUdpPort);

            UseIpv4 = useIpv4;
            UseIpv6 = useIpv6;

            udpReceivers = new List<UdpClient>();
            tcpReceivers = new List<DiscoveryServiceSocket>();
            UdpClient udpReceiver4 = null;

            if (UseIpv4)
            {
                udpReceiver4 = new UdpClient(AddressFamily.InterNetwork);
                udpReceiver4.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
                udpReceiver4.Client.Bind(new IPEndPoint(IPAddress.Any, (Int32)RunningUdpPort));
                udpReceivers.Add(udpReceiver4);
            }

            UdpClient udpReceiver6 = null;
            if (UseIpv6)
            {
                udpReceiver6 = new UdpClient(AddressFamily.InterNetworkV6);
                udpReceiver6.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
                udpReceiver6.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, (Int32)RunningUdpPort));
                udpReceivers.Add(udpReceiver6);
            }

            RunningIpAddresses = runningIpAddresses;
            foreach (var address in runningIpAddresses)
            {
                if (sendersUdp.Keys.Contains(address))
                {
                    continue;
                }

                var localEndpoint = new IPEndPoint(address, (Int32)RunningUdpPort);
                var senderUdp = new UdpClient(address.AddressFamily);
                var senderTcp = new TcpClient(address.AddressFamily);

                DiscoveryServiceSocket receiverTcp = new DiscoveryServiceSocket(address.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp, log);
                try
                {
                    switch (address.AddressFamily)
                    {
                        case AddressFamily.InterNetwork:
                            {
                                udpReceiver4.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, optionValue: new MulticastOption(MulticastAddressIp4, address));
                                receiverTcp.Bind(localEndpoint);
                                receiverTcp.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, optionValue: true);

                                senderTcp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, optionValue: true);
                                senderUdp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);

                                senderUdp.Client.Bind(localEndpoint);

                                senderUdp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, optionValue: new MulticastOption(MulticastAddressIp4));
                                senderUdp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, optionValue: true);

                                break;
                            }
                        case AddressFamily.InterNetworkV6:
                            {
                                udpReceiver6.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, optionValue: new IPv6MulticastOption(MulticastAddressIp6, address.ScopeId));
                                receiverTcp.Bind(localEndpoint);
                                receiverTcp.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.ReuseAddress, optionValue: true);

                                senderTcp.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.ReuseAddress, optionValue: true);
                                senderUdp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);

                                senderUdp.Client.Bind(localEndpoint);
                                senderUdp.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, optionValue: new IPv6MulticastOption(MulticastAddressIp6));
                                senderUdp.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, optionValue: true);

                                break;
                            }
                        default:
                            {
                                throw new NotSupportedException($"Address family {address.AddressFamily}.");
                            }
                    }

                    if (!sendersUdp.TryAdd(address, senderUdp)) // Should not fail
                    {
                        senderUdp.Dispose();
                    }

                    tcpReceivers.Add(receiverTcp);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                {
                    // VPN NetworkInterfaces
                    senderUdp.Dispose();
                }
                catch (Exception e)
                {
                    log.LogError($"Cannot setup send socket for {address}: {e.Message}");
                    senderUdp.Dispose();
                }
            }

            foreach (var r in udpReceivers)
            {
                ListenUdp(r);
            }

            // TODO: add SSL support
            foreach (var tcpReceiver in tcpReceivers)
            {
                tcpReceiver.Listen(BackLog);
                ListenTcp(tcpReceiver);

                //break;
            }
        }

        public ICollection<IPAddress> RunningIpAddresses { get; }

        internal static List<IPAddress> IpAddressesOfInterfaces(IEnumerable<NetworkInterface> nics, Boolean useIpv4, Boolean useIpv6) =>
            nics.SelectMany(NetworkInterfaceLocalAddresses)
                .Where(a => (useIpv4 && a.AddressFamily == AddressFamily.InterNetwork)
                    || (useIpv6 && a.AddressFamily == AddressFamily.InterNetworkV6))
                .ToList();

        /// <summary>
        /// It sends udp messages from each address, which is initialized in this class
        /// </summary>
        /// <param name="message">
        /// Bytes of message to send
        /// </param>
        /// <returns>
        /// Task which allow to see any exception. Async void method doens't allow it
        /// </returns>
        public async Task SendUdpAsync(Byte[] message)
        {
            foreach (var sender in sendersUdp)
            {
                try
                {
                    var endpoint = sender.Key.AddressFamily == AddressFamily.InterNetwork ?
                        MulticastEndpointIp4 : MulticastEndpointIp6;
                    await sender.Value.SendAsync(message, message.Length, endpoint)
                        .ConfigureAwait(continueOnCapturedContext: false);

                    //break;
                }
                catch(SocketException e)
                {
                    log.LogError($"Failed to send UDP message, SocketException: {e.Message}");
                }
                catch (InvalidOperationException e)
                {
                    log.LogError($"Failed to send UDP message, InvalidOperationException: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Listens for UDP messages asynchronously
        /// </summary>
        /// <param name="receiver">
        /// Object which returns data of the messages
        /// </param>
        private void ListenUdp(UdpClient receiver)
        {
            // ReceiveAsync does not support cancellation.  So the receiver is disposed
            // to stop it. See https://github.com/dotnet/corefx/issues/9848
            _ = Task.Run(async () =>
            {
                try
                {
                    var task = receiver.ReceiveAsync();

                    _ = task.ContinueWith(x => ListenUdp(receiver), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    _ = task.ContinueWith((taskReceiving) =>
                    {
                        UdpMessageEventArgs eventArgs = new UdpMessageEventArgs
                        {
                            Buffer = taskReceiving.Result.Buffer,
                            RemoteEndPoint = taskReceiving.Result.RemoteEndPoint
                        };

                        UdpMessageReceived?.Invoke(receiver, eventArgs);
                        //await Task.Run(() => UdpMessageReceived.Invoke(receiver, eventArgs)).
                        //ConfigureAwait(continueOnCapturedContext: false);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    await task.ConfigureAwait(false);
                }
                catch(ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException e)
                {
                    log.LogError($"Failed to listen UDP message, SocketException: {e.Message}");
                    return;
                }
            });
        }

        /// <summary>
        /// Listens for TCP messages asynchronously
        /// </summary>
        /// <param name="receiver">
        /// Object which returns data of the messages
        /// </param>
        private void ListenTcp(DiscoveryServiceSocket receiver)
        {
            // ReceiveAsync does not support cancellation.  So the receiver is disposed
            // to stop it. See https://github.com/dotnet/corefx/issues/9848
            _ = Task.Run(async () =>
            {
                try
                {
                    var task = receiver.ReceiveAsync(Constants.ReceiveTimeout, TcpMaxSockets);

                    _ = task.ContinueWith(x => ListenTcp(receiver), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    //using tasks provides unblocking event calls
                    _ = task.ContinueWith(taskReceiving =>
                    {
                        TcpMessageReceived?.Invoke(receiver, taskReceiving.Result);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    await task.ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException e)
                {
                    //TODO don't return. Change absolutely TCP port (in TcpListener), but take into account maxValueTcpPort
                    log.LogError($"Failed to listen on TCP port.\n" +
                        $"{e}");
                    return;
                }
                catch(TimeoutException e)
                {
                    log.LogError($"Failed to listen on TCP port.\n" +
                        $"{e}");
                }
            });
        }

        /// <param name="nic">
        /// <see cref="NetworkInterface"/> wherefrom you want to get collection of local <see cref="IPAddress"/>
        /// </param>
        /// <returns>
        /// Collection of local <see cref="IPAddress"/> according to <paramref name="nic"/>
        /// </returns>
        private static IEnumerable<IPAddress> NetworkInterfaceLocalAddresses(NetworkInterface nic)
        {
            return nic
                .GetIPProperties()
                .UnicastAddresses
                .Select(x => x.Address)
                .Where(x => x.AddressFamily != AddressFamily.InterNetworkV6 || x.IsIPv6LinkLocal)
                ;
        }

        #region IDisposable Support

        private Boolean disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(Boolean disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    UdpMessageReceived = null;
                    TcpMessageReceived = null;

                    foreach (var receiver in udpReceivers)
                    {
                        try
                        {
                            receiver.Dispose();
                        }
                        catch
                        {
                            // eat it.
                        }
                    }
                    udpReceivers.Clear();

                    foreach (var receiver in tcpReceivers)
                    {
                        try
                        {
                            receiver.Dispose();
                        }
                        catch
                        {
                            // eat it.
                        }
                    }
                    tcpReceivers.Clear();

                    foreach (var address in sendersUdp.Keys)
                    {
                        if (sendersUdp.TryRemove(address, out var sender))
                        {
                            try
                            {
                                sender.Dispose();
                            }
                            catch
                            {
                                // eat it.
                            }
                        }
                    }
                    sendersUdp.Clear();

                    GC.Collect();//maybe it should be deleted
                }

                disposedValue = true;
            }
        }

        ~Client()
        {
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
