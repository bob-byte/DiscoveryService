using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Threading.Tasks;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Kademlia.Routers;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.Interfaces;
using LUC.Services.Implementation;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   LightUpon.Cloud Service.
    /// </summary>
    /// <remarks>
    ///   Sends UDP queries via the multicast mechachism
    ///   defined in <see href="https://tools.ietf.org/html/rfc6762"/>.
    ///   Receives UDP queries and answers with TCP/IP/SSL responses.
    ///   <para>
    ///   One of the events, <see cref="QueryReceived"/> or <see cref="AnswerReceived"/>, is
    ///   raised when a <see cref="DiscoveryServiceMessage"/> is received.
    ///   </para>
    /// </remarks>
    public class NetworkEventHandler : AbstractService
    {
        /// <summary>
        ///   Recently received messages.
        /// </summary>
        private readonly RecentMessages receivedMessages = new RecentMessages();
        private const Int32 MaxDatagramSize = UdpMessage.MaxLength;

        private Client client;

        /// <summary>
        ///   Function used for listening filtered network interfaces.
        /// </summary>
        private readonly Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> networkInterfacesFilter;

        /// <summary>
        ///   Raised when any service sends a query.
        /// </summary>
        /// <value>
        ///   Contains the query <see cref="DiscoveryServiceMessage"/>.
        /// </value>
        /// <remarks>
        ///   Any exception throw by the event handler is simply logged and
        ///   then forgotten.
        /// </remarks>
        /// <seealso cref="SendQuery(DiscoveryServiceMessage)"/>
        public event EventHandler<UdpMessageEventArgs> QueryReceived;

        /// <summary>
        ///   Raised when one or more network interfaces are discovered. 
        /// </summary>
        /// <value>
        ///   Contains the network interface(s).
        /// </value>
        public event EventHandler<NetworkInterfaceEventArgs> NetworkInterfaceDiscovered;

        /// <summary>
        ///   Raised when any link-local service responds to a query ( MessageOperation.Acknowledge ).
        ///   This is an answer to UDP multicast.
        /// </summary>
        /// <value>
        ///   Contains the answer <see cref="AcknowledgeTcpMessage"/>.
        /// </value>
        /// <remarks>
        ///   Any exception throw by the event handler is simply logged and
        ///   then forgotten.
        /// </remarks>
        public event EventHandler<TcpMessageEventArgs> AnswerReceived;

        /// <summary>
        ///   Raised when any link-local service sends PING ( MessageOperation.Ping ).
        ///   This is a Kadamilia ping request.
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> PingReceived;

        /// <summary>
        ///   Raised when any link-local service sends STORE ( MessageOperation.Store ).
        ///   This is a Kadamilia STORE RPC call.
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> StoreReceived;

        /// <summary>
        ///   Raised when any link-local service sends FindNode node request ( MessageOperation.FindNode ).
        ///   This is a Kadamilia's FindNode RPC call.
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> FindNodeReceived;

        /// <summary>
        ///   Raised when any link-local service asends FindValue RPC ( MessageOperation.FindValue ).
        ///   This is a Kadamilia's FindValue RPC call.
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> FindValueReceived;

        /// <summary>
        ///   Create a new instance of the <see cref="NetworkEventHandler"/> class.
        /// </summary>
        /// <param name="filter">
        ///   Multicast listener will be bound to result of filtering function.
        /// </param>
        internal NetworkEventHandler(String machineId, Boolean useIpv4, Boolean useIpv6, 
            UInt16 protocolVersion, Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> filter = null)
        {
            MachineId = machineId;
            UseIpv4 = useIpv4;
            UseIpv6 = useIpv6;
            ProtocolVersion = protocolVersion;

            OurContact = new Contact(ID.RandomIDInKeySpace, RunningTcpPort);
            DistributedHashTable = new Dht(OurContact, ProtocolVersion, () => new VirtualStorage(), new ParallelRouter());

            networkInterfacesFilter = filter;

            IgnoreDuplicateMessages = false;
        }

        public Contact OurContact { get; }
        
        public Dht DistributedHashTable { get; private set; }

        /// <summary>
        /// Known network interfaces
        /// </summary>
        internal static List<NetworkInterface> KnownNics { get; private set; } = new List<NetworkInterface>();

        /// <summary>
        ///   Determines if received messages are checked for duplicates.
        /// </summary>
        /// <value>
        ///   <b>true</b> to ignore duplicate messages. Defaults to <b>true</b>.
        /// </value>
        /// <remarks>
        ///   When set, a message that has been received within the last minute
        ///   will be ignored.
        /// </remarks>
        public Boolean IgnoreDuplicateMessages { get; set; }

        /// <summary>
        ///   Get the network interfaces that are useable.
        /// </summary>
        /// <returns>
        ///   A sequence of <see cref="NetworkInterface"/>.
        /// </returns>
        /// <remarks>
        ///   The following filters are applied
        ///   <list type="bullet">
        ///   <item><description>interface is enabled</description></item>
        ///   <item><description>interface is not a loopback</description></item>
        ///   </list>
        ///   <para>
        ///   If no network interface is operational, then the loopback interface(s)
        ///   are included (127.0.0.1 and/or ::1).
        ///   </para>
        /// </remarks>
        public static IEnumerable<NetworkInterface> NetworkInterfaces()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToArray();
            if (nics.Length > 0)
                return nics;

            // Special case: no operational NIC, then use loopbacks.
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up);
        }

        /// <summary>
        ///   Get the IP addresses of the local machine.
        /// </summary>
        /// <returns>
        ///   A sequence of IP addresses of the local machine.
        /// </returns>
        /// <remarks>
        ///   The loopback addresses (127.0.0.1 and ::1) are NOT included in the
        ///   returned sequences.
        /// </remarks>
        public static IEnumerable<IPAddress> IPAddresses()
        {
            return NetworkInterfaces()
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address);
        }

        /// <summary>
        /// IP-addresses which <seealso cref="DiscoveryService"/> uses to exchange messages
        /// </summary>
        public List<IPAddress> RunningIpAddresses() =>
            Client.IpAddressesOfInterfaces(networkInterfacesFilter?.Invoke(KnownNics) ?? KnownNics, UseIpv4, UseIpv6);

        /// <summary>
        ///   Get the link local IP addresses of the local machine.
        /// </summary>
        /// <returns>
        ///   A sequence of IP addresses.
        /// </returns>
        /// <remarks>
        ///   All IPv4 addresses are considered link local.
        /// </remarks>
        /// <seealso href="https://en.wikipedia.org/wiki/Link-local_address"/>
        public static IEnumerable<IPAddress> LinkLocalAddresses()
        {
            return IPAddresses()
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork ||
                    (a.AddressFamily == AddressFamily.InterNetworkV6 && a.IsIPv6LinkLocal));
        }

        private void OnNetworkAddressChanged(Object sender, EventArgs e) => FindNetworkInterfaces();

        private void FindNetworkInterfaces()
        {
            log.LogInfo("Finding network interfaces");

            try
            {
                var currentNics = NetworkInterfaces().ToList();

                var newNics = new List<NetworkInterface>();
                var oldNics = new List<NetworkInterface>();

                foreach (var nic in KnownNics.Where(k => !currentNics.Any(n => k.Id == n.Id)))
                {
                    oldNics.Add(nic);

#if DEBUG
                    log.LogInfo($"Removed nic \'{nic.Name}\'.");
#endif
                }

                foreach (var nic in currentNics.Where(nic => !KnownNics.Any(k => k.Id == nic.Id)))
                {
                    newNics.Add(nic);

#if DEBUG
                    log.LogInfo($"Found nic '{nic.Name}'.");
#endif
                }

                KnownNics = currentNics;

                // Only create client if something has change.
                if (newNics.Any() || oldNics.Any())
                {
                    //InitKademliaProtocol();

                    client?.Dispose();
                    InitClient();
                }

                if (newNics.Any())
                {
                    NetworkInterfaceDiscovered?.Invoke(this, new NetworkInterfaceEventArgs
                    {
                        NetworkInterfaces = newNics
                    });
                }

                //
                // Situation has seen when NetworkAddressChanged is not triggered 
                // (wifi off, but NIC is not disabled, wifi - on, NIC was not changed 
                // so no event). Rebinding fixes this.
                //
                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            }
            catch (Exception e)
            {
                log.LogError(e, "FindNics failed");
            }
        }

        private void InitClient()
        {
            client = new Client(UseIpv4, UseIpv6, RunningIpAddresses());
            client.UdpMessageReceived += OnUdpMessage;
            client.TcpMessageReceived += RaiseAnswerReceived;
        }

        /// <summary>
        ///   Called by the MulticastClient when a UDP message is received.
        /// </summary>
        /// <param name="sender">
        ///   The <see cref="Client"/> that got the message.
        /// </param>
        /// <param name="result">
        ///   The received message <see cref="UdpReceiveResult"/>.
        /// </param>
        /// <remarks>
        ///   Decodes the <paramref name="result"/> and then raises
        ///   either the <see cref="QueryReceived"/> event.
        ///   <para>
        ///   Multicast messages received with different protocol version or 
        ///   the same machine Id as ours are silently ignored.
        ///   </para>
        ///   <para>
        ///   If the message cannot be decoded, then the <see cref="MalformedMessage"/>
        ///   event is raised.
        ///   </para>
        /// </remarks>
        private void OnUdpMessage(Object sender, UdpMessageEventArgs result)
        {
            if (result?.Buffer?.Length <= MaxDatagramSize)
            {
                UdpMessage message = new UdpMessage();
                try
                {
                    message.Read(result.Buffer);
                }
                catch (ArgumentNullException)
                {
                    // ignore malformed message
                    return;
                }
                catch (EndOfStreamException)
                {
                    // ignore malformed message
                    return;
                }

                // If recently received, then ignore.
                var isRecentlyReceived = !receivedMessages.TryAdd(message.MessageId);

                if ((!IgnoreDuplicateMessages || !isRecentlyReceived) &&
                    ((message.ProtocolVersion == ProtocolVersion) &&
                    (message.MachineId != MachineId)))
                {
                    result.SetMessage(message);

                    try
                    {
                        QueryReceived?.Invoke(sender, result);
                    }
                    catch (TimeoutException e)
                    {
                        log.LogError($"Receive handler failed: {e.Message}");
                        // eat the exception
                    }
                    catch (SocketException e)
                    {
                        log.LogError($"Receive handler failed: {e.Message}");
                        // eat the exception
                    }
                    catch (EndOfStreamException e)
                    {
                        log.LogError($"Receive handler failed: {e.Message}");
                        // eat the exception
                    }
                }
            }
        }

        /// <summary>
        ///   TCP message received.
        ///
        ///   Called by <see cref="Client.TcpMessageReceived"/> in method <see cref="Client.ListenTcp(TcpListener)"/>
        /// </summary>
        /// <param name="message">
        ///   Received message is then processed by corresponding event handler, depending on type of message
        /// </param>
        private void RaiseAnswerReceived(Object sender, TcpMessageEventArgs receiveResult)
        {
            lock (this)
            {
                try
                {
                    var lastActiveAddress = (receiveResult.LocalEndPoint as IPEndPoint).Address;
                    OurContact.LastActiveIpAddress = lastActiveAddress;
                    DistributedHashTable.OurContact.LastActiveIpAddress = lastActiveAddress;

                    Message message = receiveResult.Message<Message>();
                    switch (message.MessageOperation)
                    {
                        case MessageOperation.Acknowledge:
                            {
                                HandleReceivedTcpMessage<AcknowledgeTcpMessage>(sender, receiveResult, AnswerReceived);
                                break;
                            }

                        case MessageOperation.Ping:
                            {
                                /// Someone is pinging us.  Register the contact and respond.
                                HandleReceivedTcpMessage<PingRequest>(sender, receiveResult, PingReceived);
                                break;
                            }

                        case MessageOperation.Store:
                            {
                                HandleReceivedTcpMessage<StoreRequest>(sender, receiveResult, StoreReceived);
                                break;
                            }

                        case MessageOperation.FindNode:
                            {
                                HandleReceivedTcpMessage<FindNodeRequest>(sender, receiveResult, FindNodeReceived);
                                break;
                            }

                        case MessageOperation.FindValue:
                            {
                                HandleReceivedTcpMessage<FindValueRequest>(sender, receiveResult, FindValueReceived);
                                break;
                            }
                    }
                }
                catch(EndOfStreamException ex)
                {
                    log.LogError($"Received malformed message: {ex}");
                }
                catch (SocketException ex)
                {
                    log.LogError($"Cannot to handle TCP message: {ex}");
                }
            }
        }

        private void HandleReceivedTcpMessage<T>(Object sender, TcpMessageEventArgs receiveResult, EventHandler<TcpMessageEventArgs> receiveEvent)
            where T: Message, new()
        {
            T request = new T();
            request.Read(receiveResult.Buffer);
            receiveResult.SetMessage(request);

            receiveEvent?.Invoke(sender, receiveResult);
        }

        public void TryKademliaOperation(Object sender, TcpMessageEventArgs receiveResult)
        {
            lock (this)
            {
                var tcpMessage = receiveResult.Message<AcknowledgeTcpMessage>(whetherReadMessage: false);
                try
                {
                    if ((tcpMessage != null) && (receiveResult.SendingEndPoint is IPEndPoint ipEndPoint))
                    {
                        var knownContact = new Contact(new ID(tcpMessage.IdOfSendingContact), tcpMessage.TcpPort, ipEndPoint.Address);
                        DistributedHashTable.Node.PingRemoteContact(DistributedHashTable.OurContact, knownContact);

                        var key = knownContact.ID;

                        DistributedHashTable.Node.Store(DistributedHashTable.OurContact, key, MachineId, knownContact);
                        DistributedHashTable.Node.FindNode(OurContact, key, knownContact);
                        DistributedHashTable.Node.FindValue(OurContact, key, knownContact);

                        DistributedHashTable.Bootstrap(knownPeer: new Contact(new ID(tcpMessage.IdOfSendingContact), tcpMessage.TcpPort, ipEndPoint.Address));
                    }
                }
                catch (Exception e)
                {
                    log.LogError($"Kademlia operation failed: {e}");
                    // eat the exception
                }
            }
        }

        //public void Bootstrap(Object sender, TcpMessageEventArgs receiveResult)
        //{
        //    lock (this)
        //    {
        //        var tcpMessage = receiveResult.Message<AcknowledgeTcpMessage>(whetherReadMessage: false);
        //        if ((tcpMessage != null) && (receiveResult.RemoteContact is IPEndPoint ipEndPoint))
        //        {
        //            Protocol.Ping(DistributedHashTable.OurContact, ipEndPoint.Address, (Int32)tcpMessage.TcpPort);

        //            //DistributedHashTable.Bootstrap(knownPeer: new Contact(Protocol, new ID(tcpMessage.IdOfSendingContact), ipEndPoint.Address, tcpMessage.TcpPort));
        //        }
        //    }
        //}

        /// <summary>
        ///   Start the service.
        /// </summary>
        internal void Start()
        {
            KnownNics.Clear();

            FindNetworkInterfaces();
        }

        /// <summary>
        ///   Stop the service.
        /// </summary>
        /// <remarks>
        ///   Clears all the event handlers.
        /// </remarks>
        internal void Stop()
        {
            // All event handlers are cleared.
            QueryReceived = null;
            AnswerReceived = null;
            PingReceived = null;
            StoreReceived = null;
            FindNodeReceived = null;
            FindValueReceived = null;

            NetworkInterfaceDiscovered = null;

            // Stop current UDP and TCP listeners and senders
            client?.Dispose();
            client = null;
        }

        /// <summary>
        ///   Sends out UDP multicast messages
        /// </summary>
        internal void SendQuery()
        {
            Random random = new Random();
            var msg = new UdpMessage(messageId: (UInt32)random.Next(0, Int32.MaxValue), ProtocolVersion,
                RunningTcpPort, MachineId);
            var packet = msg.ToByteArray();

            client?.SendUdpAsync(packet).GetAwaiter().GetResult();
        }
    }
}
