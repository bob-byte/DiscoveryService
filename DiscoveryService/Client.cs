using LUC.Interfaces;
using LUC.Services.Implementation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using LUC.DiscoveryService.Extensions;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   Allows sending and receiving datagrams over multicast sockets.
    ///
    ///    Also listens on TCP/IP port for receiving side to establish connection.
    /// </summary>
    class Client : IDisposable
    {
        [Import(typeof(ILoggingService))]
        private static readonly ILoggingService log = new LoggingService();

        private static readonly IPAddress MulticastAddressIp4 = IPAddress.Parse("224.0.0.251");
        private readonly IPEndPoint MulticastEndpointIp4;

        private static readonly IPAddress MulticastAddressIp6 = IPAddress.Parse("FF02::FB");
        private readonly IPEndPoint MulticastEndpointIp6;

        private readonly List<UdpClient> udpReceivers;
        private readonly List<TcpListener> tcpReceivers;

        private readonly ConcurrentDictionary<IPAddress, UdpClient> sendersUdp = new ConcurrentDictionary<IPAddress, UdpClient>();

        /// <summary>
        /// It calls method OnUdpMessage, which run SendTcp
        /// </summary>
        public event EventHandler<UdpReceiveResult> UdpMessageReceived;

        /// <summary>
        /// It calls method OnTcpMessage, which add new groups to ServiceDiscovery.GroupsSupported
        /// </summary>
        public event EventHandler<MessageEventArgs> TcpMessageReceived;

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
        /// <param name="nics">
        /// NetworkInterfaces wherefrom we should send to
        /// </param>
        public Client(ServiceProfile profile, Boolean useIpv4, Boolean useIpv6, IEnumerable<NetworkInterface> nics)
        {
            MulticastEndpointIp4 = new IPEndPoint(MulticastAddressIp4, (Int32)profile.RunningUdpPort);
            MulticastEndpointIp6 = new IPEndPoint(MulticastAddressIp6, (Int32)profile.RunningUdpPort);

            udpReceivers = new List<UdpClient>();
            tcpReceivers = new List<TcpListener>();
            UdpClient udpReceiver4 = null;

            if (useIpv4)
            {
                udpReceiver4 = new UdpClient(AddressFamily.InterNetwork);
                udpReceiver4.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
                udpReceiver4.Client.Bind(new IPEndPoint(IPAddress.Any, (Int32)profile.RunningUdpPort));
                udpReceivers.Add(udpReceiver4);
            }

            UdpClient udpReceiver6 = null;
            if (useIpv6)
            {
                udpReceiver6 = new UdpClient(AddressFamily.InterNetworkV6);
                udpReceiver6.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
                udpReceiver6.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, (Int32)profile.RunningUdpPort));
                udpReceivers.Add(udpReceiver6);
            }

            // Get the IP addresses that we should send to.
            var addresses = nics
                .SelectMany(GetNetworkInterfaceLocalAddresses)
                .Where(a => (useIpv4 && a.AddressFamily == AddressFamily.InterNetwork)
                    || (useIpv6 && a.AddressFamily == AddressFamily.InterNetworkV6));

            foreach (var address in addresses)
            {
                if (sendersUdp.Keys.Contains(address))
                {
                    continue;
                }

                var localEndpoint = new IPEndPoint(address, (Int32)profile.RunningUdpPort);
                var senderUdp = new UdpClient(address.AddressFamily);
                var senderTcp = new TcpClient(address.AddressFamily);
                TcpListener receiverTcp = new TcpListener(address, (Int32)profile.RunningTcpPort);

                try
                {
                    switch (address.AddressFamily)
                    {
                        case AddressFamily.InterNetwork:
                            {
                                udpReceiver4.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, optionValue: new MulticastOption(MulticastAddressIp4, address));
                                receiverTcp.Server.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, optionValue: true);

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
                                receiverTcp.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.ReuseAddress, optionValue: true);

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
                    //log.LogError($"Cannot setup send socket for {address}: {e.Message}");
                    senderUdp.Dispose();
                }
            }

            Task.Run(() =>
            {
                foreach (var r in udpReceivers)
                {
                    ListenUdp(r);
                }
            });

            // TODO: add SSL support
            Task.Run(() =>
            {
                foreach (var tcpReceiver in tcpReceivers)
                {
                    tcpReceiver.Start();
                    ListenTcp(tcpReceiver);
                }
            });
        }

        /// <summary>
        /// It sends udp messages from each address, which is initialized in this class
        /// </summary>
        /// <param name="message">
        /// Bytes of message to send
        /// </param>
        /// <param name="periodInMs">
        /// How often to send messages
        /// </param>
        /// <param name="tokenOuter">
        /// Token which is initialized in <see cref="ServiceDiscovery"/> to know when we should stop sendings
        /// </param>
        /// <returns></returns>
        public async Task SendUdpAsync(Byte[] message)
        {
            foreach (var sender in sendersUdp)
            {
                try
                {
                    var endpoint = sender.Key.AddressFamily == AddressFamily.InterNetwork ?
                        MulticastEndpointIp4 : MulticastEndpointIp6;
                    await sender.Value.SendAsync(message, message.Length, endpoint).ConfigureAwait(false);
                }
                catch(SocketException)
                {
                    //log.LogError($"Sender {sender.Key} failure: {e.Message}");
                }
                catch(InvalidOperationException)
                {
                    //log.LogError($"Sender {sender.Key} failure: {e.Message}");
                }
            }
        }

        /// <summary>
        /// It listens udp messages in another task
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

                    _ = task.ContinueWith(x => UdpMessageReceived?.Invoke(this, x.Result), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    await task.ConfigureAwait(false);
                }
                catch
                {
                    return;
                }
            });
        }

        /// <summary>
        /// It listens tcp messages in another task
        /// </summary>
        /// <param name="receiver">
        /// Object which returns data of the messages
        /// </param>
        private void ListenTcp(TcpListener receiver)
        {
            // ReceiveAsync does not support cancellation.  So the receiver is disposed
            // to stop it. See https://github.com/dotnet/corefx/issues/9848
            _ = Task.Run(async () =>
            {
                try
                {
                    var task = receiver.ReceiveAsync();

                    _ = task.ContinueWith(x => ListenTcp(receiver), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    _ = task.ContinueWith(x => TcpMessageReceived?.Invoke(this, x.Result), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    await task.ConfigureAwait(false);
                }
                catch
                {
                    return;
                }
            });
        }

        /// <param name="nic">
        /// <see cref="NetworkInterface"/> wherefrom you want to get collection of local <see cref="IPAddress"/>
        /// </param>
        /// <returns>
        /// Collection of local <see cref="IPAddress"/> according to <paramref name="nic"/>
        /// </returns>
        private IEnumerable<IPAddress> GetNetworkInterfaceLocalAddresses(NetworkInterface nic)
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
                            receiver.Stop();
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
