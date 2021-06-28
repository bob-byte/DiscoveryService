using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.Protocols.Tcp;
using LUC.DiscoveryService.Kademlia.Routers;
using LUC.DiscoveryService.Messages;
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
    ///   raised when a <see cref="Message"/> is received.
    ///   </para>
    /// </remarks>
    public class Service : AbstractService
    {
        /// <summary>
        ///   Recently received messages.
        /// </summary>
        private readonly RecentMessages receivedMessages = new RecentMessages();
        private const Int32 MaxDatagramSize = Message.MaxLength;
        private Dht distributedHashTable;

        private Client client;

        /// <summary>
        ///   Function used for listening filtered network interfaces.
        /// </summary>
        private readonly Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> networkInterfacesFilter;

        /// <summary>
        ///   Raised when any service sends a query.
        /// </summary>
        /// <value>
        ///   Contains the query <see cref="Message"/>.
        /// </value>
        /// <remarks>
        ///   Any exception throw by the event handler is simply logged and
        ///   then forgotten.
        /// </remarks>
        /// <seealso cref="SendQuery(Message)"/>
        public event EventHandler<MessageEventArgs> QueryReceived;

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
        public event EventHandler<MessageEventArgs> AnswerReceived;

        /// <summary>
        ///   Raised when any link-local service sends PING ( MessageOperation.Ping ).
        ///   This is a Kadamilia ping request.
        /// </summary>
        public event EventHandler<MessageEventArgs> PingReceived;

        /// <summary>
        ///   Raised when any link-local service sends PONG ( MessageOperation.PingResponse ).
        ///   This is a Kadamilia pong answer to ping.
        /// </summary>
        public event EventHandler<MessageEventArgs> PongReceived;

        /// <summary>
        ///   Raised when any link-local service sends STORE ( MessageOperation.Store ).
        ///   This is a Kadamilia STORE RPC call.
        /// </summary>
        public event EventHandler<MessageEventArgs> StoreReceived;

        /// <summary>
        ///   Raised when any link-local service responds to STORE request ( MessageOperation.StoreResponse ).
        ///   This is a response to Kadamilia's STORE RPC call.
        /// </summary>
        public event EventHandler<MessageEventArgs> StoreResponseReceived;

        /// <summary>
        ///   Raised when any link-local service sends FindNode node request ( MessageOperation.FindNode ).
        ///   This is a Kadamilia's FindNode RPC call.
        /// </summary>
        public event EventHandler<MessageEventArgs> FindNodeReceived;

        /// <summary>
        ///   Raised when any link-local service answers to FindNode RPC ( MessageOperation.FindNodeResponse ).
        ///   This is a response to Kadamilia's FindNode RPC call.
        /// </summary>
        public event EventHandler<MessageEventArgs> FindNodeResponseReceived;

        /// <summary>
        ///   Raised when any link-local service asends FindValue RPC ( MessageOperation.FindValue ).
        ///   This is a Kadamilia's FindValue RPC call.
        /// </summary>
        public event EventHandler<MessageEventArgs> FindValueReceived;

        /// <summary>
        ///   Raised when any link-local service answers to FindValue RPC ( MessageOperation.FindValueResponse ).
        ///   This is a response to Kadamilia's FindValue RPC call.
        /// </summary>
        public event EventHandler<MessageEventArgs> FindValueResponseReceived;

        /// <summary>
        ///   Raised when message is received that cannot be decoded.
        /// </summary>
        /// <value>
        ///   The message as a byte array.
        /// </value>
        public event EventHandler<Byte[]> MalformedMessage;

        /// <summary>
        ///   Create a new instance of the <see cref="Service"/> class.
        /// </summary>
        /// <param name="filter">
        ///   Multicast listener will be bound to result of filtering function.
        /// </param>
        internal Service(ID machineId, Boolean useIpv4, Boolean useIpv6, 
            UInt32 protocolVersion, Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> filter = null)
        {
            MachineId = machineId;

            var ipAddresses = GetIPAddresses().ToArray();
            var protocols = new List<IProtocol>();
            for (Int32 numAddress = 0; numAddress < ipAddresses.Length; numAddress++)
            {
                protocols.Add(new TcpProtocol(url: ipAddresses[numAddress].ToString(), RunningTcpPort));
            }

            distributedHashTable = new Dht(machineId,  protocols, () => new VirtualStorage(), new Router());

            UseIpv4 = useIpv4;
            UseIpv6 = useIpv6;
            ProtocolVersion = protocolVersion;
            networkInterfacesFilter = filter;

            IgnoreDuplicateMessages = true;
        }

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

#if DEBUG
                    log.LogInfo($"Removed nic \'{nic.Name}\'.");
#endif
                    //if (log.IsDebugEnabled)
                    //{
                    //    log.Debug($"Removed nic '{nic.Name}'.");
                    //}
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
                    client?.Dispose();

                    client = new Client(UseIpv4, UseIpv6, networkInterfacesFilter?.Invoke(KnownNics) ?? KnownNics);

                    client.UdpMessageReceived += OnUdpMessage;
                    client.TcpMessageReceived += RaiseAnswerReceived;
                    client.TcpMessageReceived += Bootstrap;
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
        private void OnUdpMessage(object sender, UdpReceiveResult result)
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

            MulticastMessage message = new MulticastMessage();
            try
            {
                message.Read(result.Buffer);
            }
            catch (ArgumentNullException e)
            {
                log.LogError(e, "Received malformed message");
                MalformedMessage?.Invoke(sender, result.Buffer);

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
                QueryReceived?.Invoke(this, new MessageEventArgs { Message = message, RemoteEndPoint = result.RemoteEndPoint });
            }
            catch (Exception e)
            {
                log.LogError(e, "Receive handler failed");
                // eat the exception
            }
        }

        /// <summary>
        ///   Called by <see cref="Client.TcpMessageReceived"/> in method <see cref="Client.ListenTcp(TcpListener)"/>
        /// </summary>
        /// <param name="message">
        ///   Received message is then processed by corresponding event handler, depending on type of message
        /// </param>
        private void RaiseAnswerReceived(Object sender, MessageEventArgs receiveResult)
        {

            if(receiveResult.Message is TcpMessage message)
            {
                switch(message.Opcode)
                {
                    case MessageOperation.Ping:
                        PingReceived?.Invoke(sender, new MessageEventArgs
                        {
                            Message = message,
                            RemoteEndPoint = receiveResult.RemoteEndPoint
                        });
                        break;
                    case MessageOperation.PingResponse:
                        PongReceived?.Invoke(sender, new MessageEventArgs
                        {
                            Message = message,
                            RemoteEndPoint = receiveResult.RemoteEndPoint
                        });
                        break;
                    case MessageOperation.Store:
                        StoreReceived?.Invoke(sender, new MessageEventArgs
                        {
                            Message = message,
                            RemoteEndPoint = receiveResult.RemoteEndPoint
                        });
                        break;
                    case MessageOperation.StoreResponse:
                        StoreResponseReceived?.Invoke(sender, new MessageEventArgs
                        {
                            Message = message,
                            RemoteEndPoint = receiveResult.RemoteEndPoint
                        });
                        break;
                    case MessageOperation.FindNode:
                        FindNodeReceived?.Invoke(sender, new MessageEventArgs
                        {
                            Message = message,
                            RemoteEndPoint = receiveResult.RemoteEndPoint
                        });
                        break;
                    case MessageOperation.FindNodeResponse:
                        FindNodeResponseReceived?.Invoke(sender, new MessageEventArgs
                        {
                            Message = message,
                            RemoteEndPoint = receiveResult.RemoteEndPoint
                        });
                        break;
                    case MessageOperation.FindValue:
                        FindValueReceived?.Invoke(sender, new MessageEventArgs
                        {
                            Message = message,
                            RemoteEndPoint = receiveResult.RemoteEndPoint
                        });
                        break;
                    case MessageOperation.FindValueResponse:
                        FindValueResponseReceived?.Invoke(sender, new MessageEventArgs
                        {
                            Message = message,
                            RemoteEndPoint = receiveResult.RemoteEndPoint
                        });
                        break;
                    default:
                        AnswerReceived?.Invoke(sender, new MessageEventArgs
                        {
                            Message = message,
                            RemoteEndPoint = receiveResult.RemoteEndPoint
                        });
                        break;
                }
            }
        }

        private void Bootstrap(Object sender, MessageEventArgs receiveResult)
        {
            if(receiveResult.Message is TcpMessage tcpMess && receiveResult.RemoteEndPoint is IPEndPoint endPoint)
            {
                var url = endPoint.Address.ToString();
                distributedHashTable.Bootstrap(knownPeer: new Contact(new TcpProtocol(url, tcpMess.TcpPort), tcpMess.MachineId));
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
            PongReceived = null;
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
            var msg = new MulticastMessage(messageId: (UInt32)random.Next(0, Int32.MaxValue), ProtocolVersion,
                RunningTcpPort, MachineId);
            var packet = msg.ToByteArray();

            client?.SendUdpAsync(packet).GetAwaiter().GetResult();
        }
    }
}
