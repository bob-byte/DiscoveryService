﻿using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Messages;
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
using System.Threading;
using System.Threading.Tasks;

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
        private static readonly IPAddress MulticastAddressIp6 = IPAddress.Parse("FF02::FB");

        private  List<UdpClient> udpReceivers;
        private  List<TcpListener> tcpReceivers;

        private readonly ConcurrentDictionary<IPAddress, UdpClient> sendersUdp = new ConcurrentDictionary<IPAddress, UdpClient>();

        private readonly ServiceProfile profile;

        /// <summary>
        /// It calls method OnUdpMessage, which run SendTcp
        /// </summary>
        public event EventHandler<UdpReceiveResult> UdpMessageReceived;

        /// <summary>
        /// It calls method OnTcpMessage, which add new groups to ServiceDiscovery.GroupsSupported
        /// </summary>
        public event EventHandler<TcpMessage> TcpMessageReceived;

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
            this.profile = profile;

            // Get the IP addresses that we should send to.
            var addresses = nics
                .SelectMany(GetNetworkInterfaceLocalAddresses)
                .Where(a => (useIpv4 && a.AddressFamily == AddressFamily.InterNetwork)
                    || (useIpv6 && a.AddressFamily == AddressFamily.InterNetworkV6));

            udpReceivers = new List<UdpClient>();
            tcpReceivers = new List<TcpListener>();
            InitUdpClients(useIpv4, useIpv6,
                out var udpReceiver4, out var udpReceiver6);

            foreach (var address in addresses)
            {
                if (sendersUdp.Keys.Contains(address))
                {
                    continue;
                }

                var localEndpoint = new IPEndPoint(address, profile.RunningUdpPort);
                var senderUdp = new UdpClient(address.AddressFamily);
                var senderTcp = new TcpClient(address.AddressFamily);
                TcpListener receiverTcp = new TcpListener(address, profile.RunningTcpPort);

                try
                {
                    switch (address.AddressFamily)
                    {
                        case AddressFamily.InterNetwork:
                            {
                                SetOptionsOfSocket(SocketOptionLevel.IP, senderUdp, senderTcp,
                                    udpReceiver4, localEndpoint, 
                                    new MulticastOption(MulticastAddressIp4, address), 
                                    new MulticastOption(MulticastAddressIp4));

                                break;
                            }
                        case AddressFamily.InterNetworkV6:
                            {
                                SetOptionsOfSocket(SocketOptionLevel.IPv6, senderUdp, senderTcp,
                                    udpReceiver6, localEndpoint, 
                                    new IPv6MulticastOption(MulticastAddressIp6, address.ScopeId),  
                                    new IPv6MulticastOption(MulticastAddressIp6));
                                

                                break;
                            }
                        default:
                            {
                                throw new NotSupportedException($"Address family {address.AddressFamily}.");
                            }
                    }

                    //log.LogInfo($"Will send via {localEndpoint}");
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

            // Start listening for messages.
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
                foreach (var address in addresses)
                {
                    TcpListener tcpReceiver = new TcpListener(address, profile.RunningTcpPort);
                    tcpReceiver.Start();
                    ListenTcp(tcpReceiver);
                }
            });
        }

        private void InitUdpClients(bool useIPv4, bool useIpv6,
            out UdpClient udpReceiver4, out UdpClient udpReceiver6)
        {
            udpReceiver4 = null;

            if (useIPv4)
            {
                udpReceiver4 = new UdpClient(AddressFamily.InterNetwork);
                SetOptionsOfClient(udpReceiver4.Client, IPAddress.Any);
                udpReceivers.Add(udpReceiver4);
            }

            udpReceiver6 = null;
            if (useIpv6)
            {
                udpReceiver6 = new UdpClient(AddressFamily.InterNetworkV6);
                SetOptionsOfClient(udpReceiver6.Client, IPAddress.IPv6Any);
                udpReceivers.Add(udpReceiver6);
            }
        }

        private void SetOptionsOfClient(Socket socket, IPAddress address)
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(address, profile.RunningUdpPort));
        }

        private void SetOptionsOfSocket(SocketOptionLevel socketLevel,
            UdpClient udpSender, TcpClient tcpSender, UdpClient udpReceiver, IPEndPoint localEndpoint,
            Object optionValueOfReceiver, Object optionValueOfSender)
        {
            udpReceiver.Client.SetSocketOption(socketLevel, SocketOptionName.AddMembership, optionValueOfReceiver);

            tcpSender.Client.SetSocketOption(socketLevel, SocketOptionName.ReuseAddress, true);
            udpSender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            udpSender.Client.Bind(localEndpoint);
            udpSender.Client.SetSocketOption(socketLevel, SocketOptionName.AddMembership, optionValueOfSender);
            udpSender.Client.SetSocketOption(socketLevel, SocketOptionName.MulticastLoopback, true);
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
        public async Task SendUdpAsync(Byte[] message, Int32 periodInMs, CancellationToken tokenOuter)
        {
            foreach (var sender in sendersUdp)
            {
                try
                {
                    var endpoint = sender.Key.AddressFamily == AddressFamily.InterNetwork ? 
                        new IPEndPoint(MulticastAddressIp4, profile.RunningTcpPort) : 
                        new IPEndPoint(MulticastAddressIp6, profile.RunningTcpPort);
                    await sender.Value.SendAsync(message, message.Length, endpoint)
                                      .ConfigureAwait(false);

                    //if outer task is cancelled we stop current task
                    if (IsCancelledOuterTask(tokenOuter, periodInMs))
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    //log.LogError($"Sender {sender.Key} failure: {e.Message}");

                    profile.RunningTcpPort++;
                    if (IsCancelledOuterTask(tokenOuter, periodInMs))
                    {
                        return;
                    }
                }
            }

            _ = IsCancelledOuterTask(tokenOuter, periodInMs);
        }

        /// <summary>
        /// It waits <paramref name="periodInMs"/> ms and if <paramref name="tokenOuter"/>.IsCancellationRequested equals to true, it will return true immediately without waiting, else it will return false
        /// </summary>
        private Boolean IsCancelledOuterTask(CancellationToken tokenOuter, Int32 periodInMs) =>
            tokenOuter.WaitHandle.WaitOne(periodInMs);

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
                catch(Exception)
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
                    var task = ReceiveTcpAsync(receiver);

                    _ = task.ContinueWith(x => ListenTcp(receiver), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    _ = task.ContinueWith(x => TcpMessageReceived?.Invoke(this, x.Result), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    await task.ConfigureAwait(false);
                }
                catch(Exception)
                {
                    return;
                }
            });
        }

        /// <summary>
        /// Equivalent to <see cref="UdpClient.ReceiveAsync"/>
        /// </summary>
        /// <returns>
        /// Task which can return data of <see cref="TcpMessage"/>
        /// </returns>
        private Task<TcpMessage> ReceiveTcpAsync(TcpListener receiver)
        {
            return Task.Run(async () =>
            {
                Int32 countDataToReadAtTime = 256;
                Byte[] buffer = new Byte[countDataToReadAtTime];

                var client = await receiver.AcceptTcpClientAsync();
                var stream = client.GetStream();
                
                stream.Read(buffer, 0, countDataToReadAtTime);
                Parsing<TcpMessage> parsing = new ParsingTcpData();
                var message = parsing.GetEncodedData(buffer);

                stream.Close();
                client.Close();

                return message;
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
