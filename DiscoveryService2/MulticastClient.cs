using Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   Allows sending and receiving datagrams over multicast sockets.
    ///
    ///    Also listens on TCP/IP port for receiving side to establish connection.
    /// </summary>
    class MulticastClient : IDisposable
    {
        static readonly ILog log = LogManager.GetLogger(typeof(MulticastClient));

        /// <summary>
        ///   The port number assigned to Dropbox LanSync protocol.
        /// </summary>
        /// <value>
        ///   Port number 17500.
        /// </value>
        public static readonly int MulticastPort = 17500;

        static readonly IPAddress MulticastAddressIp4 = IPAddress.Parse("224.0.0.251");
        static readonly IPAddress MulticastAddressIp6 = IPAddress.Parse("FF02::FB");
        static readonly IPEndPoint EndpointIp6 = new IPEndPoint(MulticastAddressIp6, MulticastPort);
        static readonly IPEndPoint EndpointIp4 = new IPEndPoint(MulticastAddressIp4, MulticastPort);

        readonly List<UdpClient> udpReceivers;
        readonly List<TcpClient> tcpReceivers;
        readonly ConcurrentDictionary<IPAddress, UdpClient> senders = new ConcurrentDictionary<IPAddress, UdpClient>();

        public event EventHandler<UdpReceiveResult> UdpMessageReceived;

        public event EventHandler<TcpReceiveResult> TcpMessageReceived;

        public MulticastClient(bool useIPv4, bool useIpv6, IEnumerable<NetworkInterface> nics)
        {
            // Setup the udpReceivers.
            udpReceivers = new List<UdpClient>();
	    tcpReceivers = new List<TcpClient>();

            UdpClient udpReceiver4 = null;
            if (useIPv4)
            {
                udpReceiver4 = new UdpClient(AddressFamily.InterNetwork);
                udpReceiver4.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                udpReceiver4.Client.Bind(new IPEndPoint(IPAddress.Any, MulticastPort));
                udpReceivers.Add(udpReceiver4);
            }

            TcpClient tcpReceiver4 = null;
            if (useIPv4)
            {
                tcpReceiver4 = new UdpClient(AddressFamily.InterNetwork);
                tcpReceiver4.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                tcpReceiver4.Client.Bind(new IPEndPoint(IPAddress.Any, MulticastPort));
                udpReceivers.Add(tcpReceiver4);
            }

            UdpClient udpReceiver6 = null;
            if (useIpv6)
            {
                udpReceiver6 = new UdpClient(AddressFamily.InterNetworkV6);
                udpReceiver6.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                udpReceiver6.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, MulticastPort));
                udpReceivers.Add(udpReceiver6);
            }

            UdpClient tcpReceiver6 = null;
            if (useIpv6)
            {
                tcpReceiver6 = new UdpClient(AddressFamily.InterNetworkV6);
                tcpReceiver6.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                tcpReceiver6.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, MulticastPort));
                tcpReceivers.Add(tcpReceiver6);
            }

            // Get the IP addresses that we should send to.
            var addreses = nics
                .SelectMany(GetNetworkInterfaceLocalAddresses)
                .Where(a => (useIPv4 && a.AddressFamily == AddressFamily.InterNetwork)
                    || (useIpv6 && a.AddressFamily == AddressFamily.InterNetworkV6));
            foreach (var address in addreses)
            {
                if (senders.Keys.Contains(address))
                {
                    continue;
                }

                var localEndpoint = new IPEndPoint(address, MulticastPort);
                var sender = new UdpClient(address.AddressFamily);
                try
                {
                    switch (address.AddressFamily)
                    {
                        case AddressFamily.InterNetwork:
                            udpReceiver4.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(MulticastAddressIp4, address));
                            sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                            sender.Client.Bind(localEndpoint);
                            sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(MulticastAddressIp4));
                            sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                            break;
                        case AddressFamily.InterNetworkV6:
                            udpReceiver6.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(MulticastAddressIp6, address.ScopeId));
                            sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                            sender.Client.Bind(localEndpoint);
                            sender.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(MulticastAddressIp6));
                            sender.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, true);
                            break;
                        default:
                            throw new NotSupportedException($"Address family {address.AddressFamily}.");
                    }

                    log.Debug($"Will send via {localEndpoint}");
                    if (!senders.TryAdd(address, sender)) // Should not fail
                    {
                        sender.Dispose();
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                {
                    // VPN NetworkInterfaces
                    sender.Dispose();
                }
                catch (Exception e)
                {
                    log.Error($"Cannot setup send socket for {address}: {e.Message}");
                    sender.Dispose();
                }
            }

            // Start listening for messages.
            foreach (var r in udpReceivers)
            {
                Listen(r);
            }

	    // TODO: check if TCP listener starts, if no connection flags should be added
	    // TODO: add SSL support
            foreach (var r in tcpReceivers)
            {
                Listen(r);
            }
        }

        public async Task SendAsync(byte[] message)
        {
            foreach (var sender in senders)
            {
                try
                {
                    var endpoint = sender.Key.AddressFamily == AddressFamily.InterNetwork ? EndpointIp4 : EndpointIp6;
                    await sender.Value.SendAsync(
                        message, message.Length, 
                        endpoint)
                    .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    log.Error($"Sender {sender.Key} failure: {e.Message}");
                    // eat it.
                }
            }
        }

        void Listen(UdpClient receiver)
        {
            // ReceiveAsync does not support cancellation.  So the receiver is disposed
            // to stop it. See https://github.com/dotnet/corefx/issues/9848
            Task.Run(async () =>
            {
                try
                {
                    var task = receiver.ReceiveAsync();

                    _ = task.ContinueWith(x => Listen(receiver), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    _ = task.ContinueWith(x => UdpMessageReceived?.Invoke(this, x.Result), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    await task.ConfigureAwait(false);
                }
                catch
                {
                    return;
                }
            });
        }

        void Listen(TcpClient receiver)
        {
            // ReceiveAsync does not support cancellation.  So the receiver is disposed
            // to stop it. See https://github.com/dotnet/corefx/issues/9848
            Task.Run(async () =>
            {
                try
                {
                    var task = receiver.ReceiveAsync();

                    _ = task.ContinueWith(x => Listen(receiver), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    _ = task.ContinueWith(x => TcpMessageReceived?.Invoke(this, x.Result), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    await task.ConfigureAwait(false);
                }
                catch
                {
                    return;
                }
            });
        }

        IEnumerable<IPAddress> GetNetworkInterfaceLocalAddresses(NetworkInterface nic)
        {
            return nic
                .GetIPProperties()
                .UnicastAddresses
                .Select(x => x.Address)
                .Where(x => x.AddressFamily != AddressFamily.InterNetworkV6 || x.IsIPv6LinkLocal)
                ;
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    UdpMessageReceived = null;

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

                    foreach (var address in senders.Keys)
                    {
                        if (senders.TryRemove(address, out var sender))
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
                    senders.Clear();
                }

                disposedValue = true;
            }
        }

        ~MulticastClient()
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
