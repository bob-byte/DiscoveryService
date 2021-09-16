using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Threading.Tasks;

using LUC.DiscoveryService.Common;
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
    public class NetworkEventInvoker : AbstractService
    {
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

        public event EventHandler<TcpMessageEventArgs> CheckFileExistsReceived;

        public event EventHandler<TcpMessageEventArgs> DownloadFileReceived;

        private const Int32 MAX_DATAGRAM_SIZE = UdpMessage.MAX_LENGTH;

        private static readonly ConcurrentDictionary<UInt16, Dht> s_dhts;

        /// <summary>
        ///   Recently received messages.
        /// </summary>
        private readonly RecentMessages m_receivedMessages;

        /// <summary>
        ///   Function used for listening filtered network interfaces.
        /// </summary>
        private readonly Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> m_networkInterfacesFilter;

        private Listeners m_listeners;

        private UdpSenders m_udpSenders;

        static NetworkEventInvoker()
        {
            s_dhts = new ConcurrentDictionary<UInt16, Dht>();
        }

        /// <summary>
        ///   Create a new instance of the <see cref="NetworkEventInvoker"/> class.
        /// </summary>
        /// <param name="filter">
        ///   Multicast listener will be bound to result of filtering function.
        /// </param>
        internal NetworkEventInvoker( String machineId, Boolean useIpv4, Boolean useIpv6,
            UInt16 protocolVersion, Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> filter = null )
        {
            m_receivedMessages = new RecentMessages();

            MachineId = machineId;
            UseIpv4 = useIpv4;
            UseIpv6 = useIpv6;
            ProtocolVersion = protocolVersion;

            OurContact = new Contact( MachineId, KademliaId.RandomIDInKeySpace, RunningTcpPort );
            Dht distributedHashTable = new Dht( OurContact, ProtocolVersion,
                storageFactory: () => new VirtualStorage(), new ParallelRouter( ProtocolVersion ) );
            s_dhts.TryAdd( protocolVersion, distributedHashTable );

            m_networkInterfacesFilter = filter;

            IgnoreDuplicateMessages = false;
        }

        public Contact OurContact { get; }

        /// <summary>
        /// Known network interfaces
        /// </summary>
        internal static List<NetworkInterface> KnownNetworks { get; private set; } = new List<NetworkInterface>();

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

        internal static Dht DistributedHashTable( UInt16 protocolVersion )
        {
            if ( s_dhts.ContainsKey( protocolVersion ) )
            {
                return s_dhts[ protocolVersion ];
            }
            else
            {
                throw new ArgumentException( $"DHT with {protocolVersion} isn\'t created" );
            }
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
        public static IEnumerable<NetworkInterface> NetworkInterfaces()
        {
            IEnumerable<NetworkInterface> nics = NetworkInterface.GetAllNetworkInterfaces().
                Where( nic => nic.OperationalStatus == OperationalStatus.Up ).
                Where( nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback );

            // Special case: no operational NIC, then use loopbacks.
            if ( nics.Count() == 0 )
            {
                nics = NetworkInterface.GetAllNetworkInterfaces().Where( nic => nic.OperationalStatus == OperationalStatus.Up );
            }

            return nics;
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
        public static IEnumerable<IPAddress> IPAddresses() => NetworkInterfaces()
                .SelectMany( nic => nic.GetIPProperties().UnicastAddresses )
                .Select( u => u.Address );

        /// <summary>
        /// IP-addresses which <seealso cref="DiscoveryService"/> uses to exchange messages
        /// </summary>
        public List<IPAddress> RunningIpAddresses =>
            Listeners.IpAddressesOfInterfaces( m_networkInterfacesFilter?.Invoke( KnownNetworks ) ?? KnownNetworks, UseIpv4, UseIpv6 );

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
        public static IEnumerable<IPAddress> LinkLocalAddresses() => IPAddresses()
                .Where( a => a.AddressFamily == AddressFamily.InterNetwork ||
                     ( a.AddressFamily == AddressFamily.InterNetworkV6 && a.IsIPv6LinkLocal ) );

        private void OnNetworkAddressChanged( Object sender, EventArgs e ) => FindNetworkInterfaces();

        private void FindNetworkInterfaces()
        {
            LoggingService.LogInfo( "Finding network interfaces" );

            try
            {
                List<NetworkInterface> currentNics = NetworkInterfaces().ToList();

                List<NetworkInterface> newNics = new List<NetworkInterface>();
                List<NetworkInterface> oldNics = new List<NetworkInterface>();

                foreach ( NetworkInterface nic in KnownNetworks.Where( k => !currentNics.Any( n => k.Id == n.Id ) ) )
                {
                    oldNics.Add( nic );

#if DEBUG
                    LoggingService.LogInfo( $"Removed nic \'{nic.Name}\'." );
#endif
                }

                foreach ( NetworkInterface nic in currentNics.Where( nic => !KnownNetworks.Any( k => k.Id == nic.Id ) ) )
                {
                    newNics.Add( nic );

#if DEBUG
                    LoggingService.LogInfo( $"Found nic '{nic.Name}'." );
#endif
                }

                KnownNetworks = currentNics;

                // Only create client if something has change.
                if ( newNics.Any() || oldNics.Any() )
                {
                    //InitKademliaProtocol();

                    m_udpSenders?.Dispose();
                    m_udpSenders = new UdpSenders( RunningIpAddresses );

                    m_listeners?.Dispose();
                    InitListeners();
                }

                if ( newNics.Any() )
                {
                    NetworkInterfaceDiscovered?.Invoke( this, new NetworkInterfaceEventArgs
                    {
                        NetworkInterfaces = newNics
                    } );
                }

                //
                // Situation has seen when NetworkAddressChanged is not triggered 
                // (wifi off, but NIC is not disabled, wifi - on, NIC was not changed 
                // so no event). Rebinding fixes this.
                //
                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            }
            catch ( Exception e )
            {
                LoggingService.LogError( e, "FindNics failed" );
            }
        }

        private void InitListeners()
        {
            m_listeners = new Listeners( UseIpv4, UseIpv6, RunningIpAddresses );
            m_listeners.UdpMessageReceived += OnUdpMessage;
            m_listeners.TcpMessageReceived += RaiseAnswerReceived;
        }

        /// <summary>
        ///   Called by the MulticastClient when a UDP message is received.
        /// </summary>
        /// <param name="sender">
        ///   The <see cref="Listeners"/> that got the message.
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
        private void OnUdpMessage( Object sender, UdpMessageEventArgs result )
        {
            if ( result?.Buffer?.Length <= MAX_DATAGRAM_SIZE )
            {
                UdpMessage message = new UdpMessage();
                try
                {
                    message.Read( result.Buffer );
                }
                catch ( ArgumentNullException )
                {
                    // ignore malformed message
                    return;
                }
                catch ( EndOfStreamException )
                {
                    // ignore malformed message
                    return;
                }

                // If recently received, then ignore.
                Boolean isRecentlyReceived = /*!*/m_receivedMessages.TryAdd( message.MessageId );

                Boolean isDsMessage = IsMessageFromDs( message.TcpPort );

#if RECEIVE_TCP_FROM_OURSELF
                Boolean isOwnMessage = ( message.ProtocolVersion == ProtocolVersion ) ||
                    ( message.MachineId != MachineId );
#else
                Boolean isOwnMessage = (message.ProtocolVersion == ProtocolVersion) &&
                    (message.MachineId != MachineId);
#endif

                if ( ( !IgnoreDuplicateMessages || !isRecentlyReceived ) && ( isDsMessage ) && ( isOwnMessage ) )
                {
                    result.SetMessage( message );

                    try
                    {
                        QueryReceived?.Invoke( sender, result );
                    }
                    catch ( TimeoutException e )
                    {
                        LoggingService.LogError( $"Receive handler failed: {e.Message}" );
                        // eat the exception
                    }
                    catch ( SocketException e )
                    {
                        LoggingService.LogError( $"Receive handler failed: {e.Message}" );
                        // eat the exception
                    }
                    catch ( EndOfStreamException e )
                    {
                        LoggingService.LogError( $"Receive handler failed: {e.Message}" );
                        // eat the exception
                    }
                }
            }
        }

        /// <summary>
        ///   TCP message received.
        ///
        ///   Called by <see cref="Listeners.TcpMessageReceived"/> in method <see cref="Listeners.ListenTcp(TcpListener)"/>
        /// </summary>
        /// <param name="message">
        ///   Received message is then processed by corresponding event handler, depending on type of message
        /// </param>
        private void RaiseAnswerReceived( Object sender, TcpMessageEventArgs receiveResult )
        {
            //lock (this)
            //{
            try
            {
                IPAddress lastActiveAddress = ( receiveResult.LocalEndPoint as IPEndPoint ).Address;
                OurContact.LastActiveIpAddress = lastActiveAddress;
                s_dhts[ ProtocolVersion ].OurContact.LastActiveIpAddress = lastActiveAddress;

                Message message = receiveResult.Message<Message>();
                IPEndPoint ipEndPoint = receiveResult.AcceptedSocket.LocalEndPoint as IPEndPoint;
                Boolean isMessageFromDs = IsMessageFromDs( ipEndPoint?.Port );

                if ( isMessageFromDs )
                {
                    switch ( message.MessageOperation )
                    {
                        case MessageOperation.Acknowledge:
                        {
                            HandleReceivedTcpMessage<AcknowledgeTcpMessage>( sender, receiveResult, AnswerReceived );
                            break;
                        }

                        case MessageOperation.Ping:
                        {
                            /// Someone is pinging us.  Register the contact and respond.
                            HandleReceivedTcpMessage<PingRequest>( sender, receiveResult, PingReceived );
                            break;
                        }

                        case MessageOperation.Store:
                        {
                            HandleReceivedTcpMessage<StoreRequest>( sender, receiveResult, StoreReceived );
                            break;
                        }

                        case MessageOperation.FindNode:
                        {
                            HandleReceivedTcpMessage<FindNodeRequest>( sender, receiveResult, FindNodeReceived );
                            break;
                        }

                        case MessageOperation.FindValue:
                        {
                            HandleReceivedTcpMessage<FindValueRequest>( sender, receiveResult, FindValueReceived );
                            break;
                        }

                        case MessageOperation.CheckFileExists:
                        {
                            HandleReceivedTcpMessage<CheckFileExistsRequest>( sender, receiveResult, CheckFileExistsReceived );
                            break;
                        }

                        case MessageOperation.DownloadFile:
                        {
                            HandleReceivedTcpMessage<DownloadFileRequest>( sender, receiveResult, DownloadFileReceived );
                            break;
                        }
                    }
                }
            }
            catch ( EndOfStreamException ex )
            {
                LoggingService.LogError( $"Received malformed message: {ex}" );
            }
            catch ( InvalidDataException ex )
            {
                LoggingService.LogError( $"Received malformed message: {ex}" );
            }
            catch ( Exception ex )
            {
                LoggingService.LogError( $"Cannot to handle TCP message: {ex}" );
            }
        }

        private void HandleReceivedTcpMessage<T>( Object sender, TcpMessageEventArgs receiveResult, EventHandler<TcpMessageEventArgs> receiveEvent )
            where T : Message, new()
        {
            T request = new T();
            request.Read( receiveResult.Buffer );
            receiveResult.SetMessage( request );

            receiveEvent?.Invoke( sender, receiveResult );
        }

        public void Bootstrap( Object sender, TcpMessageEventArgs receiveResult )
        {
            AcknowledgeTcpMessage tcpMessage = receiveResult.Message<AcknowledgeTcpMessage>( whetherReadMessage: false );
            try
            {
                if ( ( tcpMessage != null ) && ( receiveResult.RemoteEndPoint is IPEndPoint ipEndPoint ) )
                {
                    Contact knownContact = new Contact( tcpMessage.MachineId, new KademliaId( tcpMessage.IdOfSendingContact ), tcpMessage.TcpPort, ipEndPoint.Address );

                    s_dhts[ ProtocolVersion ].Bootstrap( knownContact );
                }
            }
            catch ( Exception e )
            {
                LoggingService.LogError( $"Kademlia operation failed: {e}" );
                // eat the exception
            }
        }
        /// <summary>
        ///   Start the service.
        /// </summary>
        internal void Start()
        {
            KnownNetworks.Clear();

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
            m_listeners?.Dispose();
            m_listeners = null;
        }

        /// <summary>
        ///   Sends out UDP multicast messages
        /// </summary>
        internal void SendQuery()
        {
            Random random = new Random();
            UdpMessage msg = new UdpMessage( messageId: (UInt32)random.Next( 0, Int32.MaxValue ), ProtocolVersion,
                RunningTcpPort, MachineId );
            Byte[] packet = msg.ToByteArray();

            m_udpSenders?.SendUdpAsync( packet ).GetAwaiter().GetResult();
        }
    }
}
