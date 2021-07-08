using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Threading.Tasks;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.Interfaces;
using LUC.DiscoveryService.Kademlia.Protocols.Tcp;
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
    public class Service : AbstractService
    {
        /// <summary>
        ///   Recently received messages.
        /// </summary>
        private readonly RecentMessages receivedMessages = new RecentMessages();
        private const Int32 MaxDatagramSize = DiscoveryServiceMessage.MaxLength;

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
        ///   Contains the answer <see cref="TcpMessage"/>.
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
        ///   Raised when any link-local service sends PONG ( MessageOperation.PingResponse ).
        ///   This is a Kadamilia pong answer to ping.
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> PingResponseReceived;

        /// <summary>
        ///   Raised when any link-local service sends STORE ( MessageOperation.Store ).
        ///   This is a Kadamilia STORE RPC call.
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> StoreReceived;

        /// <summary>
        ///   Raised when any link-local service responds to STORE request ( MessageOperation.StoreResponse ).
        ///   This is a response to Kadamilia's STORE RPC call.
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> StoreResponseReceived;

        /// <summary>
        ///   Raised when any link-local service sends FindNode node request ( MessageOperation.FindNode ).
        ///   This is a Kadamilia's FindNode RPC call.
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> FindNodeReceived;

        /// <summary>
        ///   Raised when any link-local service answers to FindNode RPC ( MessageOperation.FindNodeResponse ).
        ///   This is a response to Kadamilia's FindNode RPC call.
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> FindNodeResponseReceived;

        /// <summary>
        ///   Raised when any link-local service asends FindValue RPC ( MessageOperation.FindValue ).
        ///   This is a Kadamilia's FindValue RPC call.
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> FindValueReceived;

        /// <summary>
        ///   Raised when any link-local service answers to FindValue RPC ( MessageOperation.FindValueResponse ).
        ///   This is a response to Kadamilia's FindValue RPC call.
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> FindValueResponseReceived;

        /// <summary>
        ///   Raised when message is received that cannot be decoded.
        /// </summary>
        /// <value>
        ///   The message as a byte array.
        /// </value>
        public event EventHandler<Byte[]> MalformedMessage;

        public List<Contact> OurContacts { get; } = new List<Contact>();

        /// <summary>
        ///   Create a new instance of the <see cref="Service"/> class.
        /// </summary>
        /// <param name="filter">
        ///   Multicast listener will be bound to result of filtering function.
        /// </param>
        internal Service(String machineId, IProtocol protocol, Boolean useIpv4, Boolean useIpv6, 
            UInt32 protocolVersion, Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> filter = null)
        {
            MachineId = machineId;

            
            UseIpv4 = useIpv4;
            UseIpv6 = useIpv6;
            ProtocolVersion = protocolVersion;
            networkInterfacesFilter = filter;

            IgnoreDuplicateMessages = true;
        }

        public Dht DistributedHashTable { get; private set; }

        public IProtocol Protocol { get; set; }

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

        private void InitKademliaProtocol(IProtocol protocol)
        {
            var runningIpAddresses = RunningIpAddresses();
            Protocol = protocol;
            foreach (var ipAddress in runningIpAddresses)
            {
                OurContacts.Add(new Contact(protocol, ID.RandomID, ipAddress, RunningTcpPort));
            }

            DistributedHashTable = new Dht(OurContacts[0], protocol, () => new VirtualStorage(), new Router());
        }

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
        public static IEnumerable<NetworkInterface> GetNetworkInterfaces()
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
        public static IEnumerable<IPAddress> GetIPAddresses()
        {
            return GetNetworkInterfaces()
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address);
        }

        public List<IPAddress> RunningIpAddresses() =>
            client.RunningIpAddresses(networkInterfacesFilter?.Invoke(KnownNics) ?? KnownNics);

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
        public static IEnumerable<IPAddress> GetLinkLocalAddresses()
        {
            return GetIPAddresses()
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork ||
                    (a.AddressFamily == AddressFamily.InterNetworkV6 && a.IsIPv6LinkLocal));
        }

        private void OnNetworkAddressChanged(object sender, EventArgs e) => FindNetworkInterfaces();

        private void FindNetworkInterfaces()
        {
            log.LogInfo("Finding network interfaces");

            try
            {
                var currentNics = GetNetworkInterfaces().ToList();

                var newNics = new List<NetworkInterface>();
                var oldNics = new List<NetworkInterface>();

                foreach (var nic in KnownNics.Where(k => !currentNics.Any(n => k.Id == n.Id)))
                {
                    oldNics.Add(nic);

                    log.LogInfo($"Removed nic \'{nic.Name}\'.");
                    //if (log.IsDebugEnabled)
                    //{
                    //    log.Debug($"Removed nic '{nic.Name}'.");
                    //}
                }

                foreach (var nic in currentNics.Where(nic => !KnownNics.Any(k => k.Id == nic.Id)))
                {
                    newNics.Add(nic);

                    log.LogInfo($"Found nic '{nic.Name}'.");
                    //if (log.IsDebugEnabled)
                    //{
                    //    log.Debug($"Found nic '{nic.Name}'.");
                    //}
                }

                KnownNics = currentNics;

                // Only create client if something has change.
                if (newNics.Any() || oldNics.Any())
                {
                    InitKademliaProtocol(Protocol);

                    client?.Dispose();
                    InitClient();
                }

                if(newNics.Any())
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
            var runningIpAddresses = new Dictionary<BigInteger, IPAddress>();
            foreach (var contact in OurContacts)
            {
                runningIpAddresses.Add(contact.ID.Value, contact.IPAddress);
            }

            client = new Client(UseIpv4, UseIpv6, runningIpAddresses);
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
        private void OnUdpMessage(object sender, UdpMessageEventArgs result)
        {
            if (result.Buffer.Length > MaxDatagramSize)
            {
                return;
            }

            //If recently received, then ignore.
            //if (IgnoreDuplicateMessages && !receivedMessages.TryAdd(result.Buffer))
            //{
            //    return;
            //}

            UdpMessage message = new UdpMessage();
            try
            {
                message.Read(result.Buffer);
            }
            catch (ArgumentNullException e)
            {
                log.LogError(e, "Received malformed message");
                MalformedMessage.Invoke(sender, result.Buffer);

                return;
            }

            //if ((message.ProtocolVersion != ProtocolVersion) ||
            //    (message.MachineId == MachineId))
            //{
            //    return;
            //}

            // Dispatch the message.
            try
            {
                QueryReceived?.Invoke(this, new UdpMessageEventArgs { Message = message, RemoteEndPoint = result.RemoteEndPoint, IdOfReceivingContact =  });
            }
            catch (Exception e)
            {
                log.LogError(e, "Receive handler failed");
                // eat the exception
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
            if(receiveResult.Message is TcpMessage message)
            {
                lock (sender)
                {
                    switch (message.MessageOperation)
                    {
                        case MessageOperation.Ping:
                            {
                                PingReceived?.Invoke(sender, new TcpMessageEventArgs
                                {
                                    Message = message,
                                    RemoteContact = receiveResult.RemoteContact
                                });


                                //Protocol..PingReceived = () => requestManagement.SendSameRandomId(receiveResult.Message as PingRequest);
                                break;
                            }

                        case MessageOperation.PingResponse:
                            PingResponseReceived?.Invoke(sender, new TcpMessageEventArgs
                            {
                                Message = message,
                                RemoteContact = receiveResult.RemoteContact
                            });
                            break;
                        case MessageOperation.Store:
                            StoreReceived?.Invoke(sender, new TcpMessageEventArgs
                            {
                                Message = message,
                                RemoteContact = receiveResult.RemoteContact
                            });
                            break;
                        case MessageOperation.StoreResponse:
                            StoreResponseReceived?.Invoke(sender, new TcpMessageEventArgs
                            {
                                Message = message,
                                RemoteContact = receiveResult.RemoteContact
                            });
                            break;
                        case MessageOperation.FindNode:
                            FindNodeReceived?.Invoke(sender, new TcpMessageEventArgs
                            {
                                Message = message,
                                RemoteContact = receiveResult.RemoteContact
                            });
                            break;
                        case MessageOperation.FindNodeResponse:
                            FindNodeResponseReceived?.Invoke(sender, new TcpMessageEventArgs
                            {
                                Message = message,
                                RemoteContact = receiveResult.RemoteContact
                            });
                            break;
                        case MessageOperation.FindValue:
                            FindValueReceived?.Invoke(sender, new TcpMessageEventArgs
                            {
                                Message = message,
                                RemoteContact = receiveResult.RemoteContact
                            });
                            break;
                        case MessageOperation.FindValueResponse:
                            FindValueResponseReceived?.Invoke(sender, new TcpMessageEventArgs
                            {
                                Message = message,
                                RemoteContact = receiveResult.RemoteContact
                            });
                            break;
                    }

                    AnswerReceived?.Invoke(sender, new TcpMessageEventArgs
                    {
                        Message = message,
                        RemoteContact = receiveResult.RemoteContact
                    });
                }
            }
        }

        public void Bootstrap(Object sender, TcpMessageEventArgs receiveResult)
        {
            lock(sender)
            {
                if ((receiveResult.Message is TcpMessage tcpMessage) && (receiveResult.RemoteContact is IPEndPoint ipEndPoint))
                {
                    //Protocol.Ping(DistributedHashTable.Contact, DistributedHashTable.Contact.IPAddress, DistributedHashTable.Contact.TcpPort);

                    DistributedHashTable.Bootstrap(knownPeer: new Contact(Protocol, new ID(tcpMessage.IdOfSendingContact), ipEndPoint.Address, tcpMessage.TcpPort));
                }
            }
        }

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
            PingResponseReceived = null;
            StoreReceived = null;
            StoreResponseReceived = null;
            FindNodeReceived = null;
            FindNodeResponseReceived = null;
            FindValueReceived = null;
            FindValueResponseReceived = null;

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
