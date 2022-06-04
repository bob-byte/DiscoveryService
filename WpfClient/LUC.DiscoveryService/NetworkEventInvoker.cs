using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Extensions;
using LUC.DiscoveryServices.Interfaces;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.Interfaces.Discoveries;

using Nito.AsyncEx;

namespace LUC.DiscoveryServices
{
    /// <summary>
    ///   Invoker of receiving specific messages, discovering connected network interface
    /// </summary>
    /// <remarks>
    ///   Sends UDP queries via the multicast mechachism
    ///   defined in <see href="https://tools.ietf.org/html/rfc6762"/>.
    ///   Receives UDP queries and answers with TCP/IP/SSL responses.
    ///   <para>
    ///   One of the events, <see cref="QueryReceived"/> or <see cref="AnswerReceived"/>, is
    ///   raised when a <see cref="DiscoveryMessage"/> is received.
    ///   </para>
    /// </remarks>
    public class NetworkEventInvoker : AbstractDsData
    {
        /// <summary>
        ///   Raised when any service sends a query (see <seealso cref="DiscoveryService.TryFindAllNodes"/>).
        /// </summary>
        /// <value>
        ///   Contains the query <see cref="MulticastMessage"/>.
        /// </value>
        /// <remarks>
        ///   Any exception throw by the event handler is simply forgotten.
        /// </remarks>
        public event EventHandler<UdpMessageEventArgs> QueryReceived;

        /// <summary>
        ///   Raised when one or more network interfaces are discovered. 
        /// </summary>
        /// <value>
        ///   Contains the network interface(s).
        /// </value>
        public event EventHandler<NetworkInterfaceEventArgs> NetworkInterfaceDiscovered;

        /// <summary>
        ///   Raised when any link-local service responds to a query by message <seealso cref="AcknowledgeTcpMessage"/>.
        ///   This is an answer to UDP multicast (<seealso cref="MulticastMessage"/>).
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
        ///   Raised we any receive valid <seealso cref="FindNodeRequest"/> RPC
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> PingReceived;

        /// <summary>
        /// Raised we any receive valid <seealso cref="FindNodeRequest"/> RPC
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> StoreReceived;

        /// <summary>
        /// Raised we any receive valid <seealso cref="FindNodeRequest"/> RPC
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> FindNodeReceived;

        /// <summary>
        /// Raised we any receive valid <seealso cref="FindNodeRequest"/> RPC
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> FindValueReceived;

        /// <summary>
        /// Raised we any receive valid <seealso cref="CheckFileExistsRequest"/> RPC
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> CheckFileExistsReceived;

        public event EventHandler<TcpMessageEventArgs> DownloadFileReceived;

        private const Int32 MAX_DATAGRAM_SIZE = MulticastMessage.MAX_LENGTH;

        /// <summary>
        /// Distributed hash tables for all version of protocols.
        /// A key is the protocol version.
        /// </summary>
        private static readonly ConcurrentDictionary<UInt16, Dht> s_dhts;

        /// <summary>
        ///   Recently received messages.
        /// </summary>
        private readonly RecentMessages m_receivedMessages;

        /// <summary>
        /// Lock using <seealso cref="m_filteredInterfaces"/>, <seealso cref="m_listeners"/> and <seealso cref="m_udpSenders"/>, because network change can be in any second
        /// </summary>
        private readonly AsyncLock m_asyncLock;

        private readonly List<AddressFamily> m_supportedAddressFamilies;

        /// <summary>
        ///   Function used for listening filtered network interfaces.
        /// </summary>
        private readonly Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> m_networkInterfacesFilter;

        private readonly Boolean m_isDsTest;

        /// <summary>
        /// Waiting time to make sure that there will be no TCP-message processing in the near future. 
        /// See <seealso cref="HandleReceivedTcpMessage{T}(TcpMessageEventArgs, EventHandler{TcpMessageEventArgs})"/> 
        /// </summary>
        private readonly TimeSpan m_minTimeOfAbsenceTcpEvents;

        /// <summary>
        /// To get random message ID
        /// </summary>
        private readonly Random m_random;

        private Int32 m_countOfNowHandlingTcpEvents;

        private IList<NetworkInterface> m_filteredInterfaces;

        private ListenersCollection<UdpMessageEventArgs, UdpClient> m_udpListeners;
        private ListenersCollection<TcpMessageEventArgs, TcpServer> m_tcpListenersCollection;

        private UdpSendersCollection m_udpSenders;

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
        internal NetworkEventInvoker(
            String machineId,
            Boolean useIpv4,
            Boolean useIpv6,
            UInt16 protocolVersion,
            IEnumerable<String> bucketLocalNames,
            Boolean isDsTest,
            Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> filter = null )
        {
            m_random = new Random();

            m_isDsTest = isDsTest;

            if ( m_isDsTest )
            {
                WaitHandleAllTcpEvents = new AsyncManualResetEvent( set: true );
                m_minTimeOfAbsenceTcpEvents = TimeSpan.FromSeconds( value: 5 );
            }

            m_filteredInterfaces = new List<NetworkInterface>();

            m_asyncLock = new AsyncLock();

            m_receivedMessages = new RecentMessages();

            MachineId = machineId;

            m_supportedAddressFamilies = new List<AddressFamily>();
            UseIpv4 = useIpv4;
            if(useIpv4)
            {
                m_supportedAddressFamilies.Add( AddressFamily.InterNetwork );
            }

            UseIpv6 = useIpv6;
            if ( useIpv6 )
            {
                m_supportedAddressFamilies.Add( AddressFamily.InterNetworkV6 );
            }

            ProtocolVersion = protocolVersion;

            OurContact = new Contact( MachineId, KademliaId.Random(), RunningTcpPort, bucketLocalNames );
            var distributedHashTable = new Dht( OurContact, ProtocolVersion,
                storageFactory: () => new VirtualStorage() );
            s_dhts.TryAdd( protocolVersion, distributedHashTable );

            m_networkInterfacesFilter = filter;

#if DEBUG
            IgnoreDuplicateMessages = false;
#else
            IgnoreDuplicateMessages = true;
#endif
        }

        public IContact OurContact { get; }

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
        /// IP-addresses which <seealso cref="DiscoveryService"/> uses to exchange messages
        /// </summary>
        public List<IPAddress> ReachableIpAddresses { get; private set; }

        /// <summary>
        /// It is not always valid value, because some events handling async
        /// </summary>
        internal Boolean IsNowHandlingAnyTcpEvent => m_countOfNowHandlingTcpEvents > 0;

        /// <summary>
        /// Wait when is not handling any TCP events
        /// </summary>
        internal AsyncManualResetEvent WaitHandleAllTcpEvents { get; }

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
        public static IEnumerable<NetworkInterface> AllTransmitableNetworkInterfaces() =>
            NetworkInterface.GetAllNetworkInterfaces().TransmitableNetworkInterfaces();

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
        public static IEnumerable<IPAddress> IPAddresses() => AllTransmitableNetworkInterfaces()
                .SelectMany( nic => nic.GetIPProperties().UnicastAddresses )
                .Select( u => u.Address );

        public static IEnumerable<IPAddress> IpAddressesOfInterfaces( IEnumerable<NetworkInterface> nics, Boolean useIpv4, Boolean useIpv6 ) =>
            nics.SelectMany( NetworkInterfaceLocalAddresses ).
                 Where( a => ( useIpv4 && ( a.AddressFamily == AddressFamily.InterNetwork ) ) ||
                             ( useIpv6 && ( a.AddressFamily == AddressFamily.InterNetworkV6 ) ) );


        /// <summary>
        /// Get distributed hash table of object with type of <see cref="DiscoveryService"/> with <paramref name="protocolVersion"/>
        /// </summary>
        /// <remarks>
        /// Use this method only if <see cref="DiscoveryService"/> is initialized with <paramref name="protocolVersion"/>
        /// </remarks>
        /// <param name="protocolVersion">
        /// The version of protocol which object with type of <see cref="DiscoveryService"/> has
        /// </param>
        /// <exception cref="ArgumentException">
        /// If object <see cref="NetworkEventInvoker"/> is not initialized with <paramref name="protocolVersion"/>
        /// </exception>
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

        /// <param name="nic">
        /// <see cref="NetworkInterface"/> wherefrom you want to get collection of local <see cref="IPAddress"/>
        /// </param>
        /// <returns>
        /// Collection of local <see cref="IPAddress"/> according to <paramref name="nic"/>
        /// </returns>
        private static IEnumerable<IPAddress> NetworkInterfaceLocalAddresses( NetworkInterface nic ) =>
            nic.GetIPProperties().
               UnicastAddresses.
               Select( a => a.Address ).
               Where( a => ( a.AddressFamily != AddressFamily.InterNetworkV6 ) || a.IsIPv6LinkLocal );

        public List<IPAddress> CurrentReachableIpAddresses()
        {
            List<NetworkInterface> clonedKnownInterfaces;

            using ( m_asyncLock.Lock() )
            {
                IEnumerable<NetworkInterface> knownInterfaces = m_networkInterfacesFilter?.Invoke( m_filteredInterfaces ) ?? m_filteredInterfaces;
                clonedKnownInterfaces = knownInterfaces.ToList();

                List<IPAddress> runningIpAddresses = IpAddressesOfInterfaces( clonedKnownInterfaces, UseIpv4, UseIpv6 ).Where( ip => IpAddressExtension.CanBeReachableInCurrentNetwork( ip, clonedKnownInterfaces ) ).ToList();
                return runningIpAddresses;
            }
        }

        public void Bootstrap( Object sender, TcpMessageEventArgs receiveResult )
        {
            AcknowledgeTcpMessage tcpMessage = receiveResult.Message<AcknowledgeTcpMessage>( whetherReadMessage: false );
            try
            {
                if ( ( tcpMessage != null ) && ( receiveResult.RemoteEndPoint is IPEndPoint ipEndPoint ) )
                {
                    var knownContact = new Contact( tcpMessage.MachineId, new KademliaId( tcpMessage.IdOfSendingContact ), tcpMessage.TcpPort, ipEndPoint.Address, tcpMessage.BucketIds );

                    s_dhts[ ProtocolVersion ].Bootstrap( knownContact );
                }
            }
            catch ( Exception e )
            {
                DsLoggerSet.DefaultLogger.LogCriticalError( $"Kademlia bootstrap failed. Received: \n{tcpMessage}", e );
                // eat the exception
            }
        }
        /// <summary>
        ///   Start the service.
        /// </summary>
        internal void Start()
        {
            using ( m_asyncLock.Lock() )
            {
                m_filteredInterfaces.Clear();
            }

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
            using ( m_asyncLock.Lock() )
            {
                m_udpListeners?.Dispose();
                m_udpListeners = null;

                m_tcpListenersCollection?.Dispose();
                m_tcpListenersCollection = null;

                m_udpSenders?.Dispose();
                m_udpSenders = null;
            }
        }

        /// <summary>
        ///   Sends out UDP multicast messages to query information about another nodes
        /// </summary>
        internal async ValueTask SendMulticastsAsync( IoBehavior ioBehavior )
        {
            if ( m_udpSenders != null )
            {
                using ( await m_asyncLock.LockAsync( ioBehavior ).ConfigureAwait( continueOnCapturedContext: false ) )
                {
                    if ( m_udpSenders != null )
                    {
                        try
                        {
                            Boolean isFirstSend = true;

                            foreach ( AddressFamily addressFamily in m_supportedAddressFamilies)
                            {
                                var multicastMessage = new MulticastMessage(
                                    messageId: (UInt32)m_random.Next( minValue: 0, Int32.MaxValue ),
                                    ProtocolVersion,
                                    RunningTcpPort,
                                    MachineId
                                );

                                if ( isFirstSend )
                                {
                                    DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Started send UDP messages. Their content:\n{multicastMessage}" );
                                }

                                Byte[] packet = multicastMessage.ToByteArray();
                                await m_udpSenders.SendMulticastsAsync( packet, ioBehavior, addressFamily ).ConfigureAwait( false );

                                isFirstSend = false;
                            }
                            
                            DsLoggerSet.DefaultLogger.LogInfo( "Finished send UDP messages" );
                        }
                        catch ( Exception ex )
                        {
                            DsLoggerSet.DefaultLogger.LogCriticalError( ex );
                        }
                    }
                }
            }
        }

        private void OnNetworkAddressChanged( Object sender, EventArgs e ) => FindNetworkInterfaces();

        private void FindNetworkInterfaces()
        {
            DsLoggerSet.DefaultLogger.LogInfo( logRecord: "Finding network interfaces" );

            try
            {
                IEnumerable<NetworkInterface> allTransmitableNetworkInterfaces = AllTransmitableNetworkInterfaces();
                List<NetworkInterface> currentNics = InterfacesAfterFilter( allTransmitableNetworkInterfaces ).ToList();

                var newNics = new List<NetworkInterface>();
                var oldNics = new List<NetworkInterface>();

                using ( m_asyncLock.Lock() )
                {
                    foreach ( NetworkInterface nic in m_filteredInterfaces.Where( k => currentNics.All( n => k.Id != n.Id ) ) )
                    {
                        oldNics.Add( nic );

#if DEBUG
                        DsLoggerSet.DefaultLogger.LogInfo( $"Removed nic \'{nic.Name}\'." );
#endif
                    }

                    foreach ( NetworkInterface nic in currentNics.Where( nic => m_filteredInterfaces.All( k => k.Id != nic.Id ) ) )
                    {
                        newNics.Add( nic );

#if DEBUG
                        DsLoggerSet.DefaultLogger.LogInfo( $"Found nic '{nic.Name}'." );
#endif
                    }

                    m_filteredInterfaces = currentNics;

                    // Only recreate listeners and UDP-senders if something has change.
                    if ( newNics.Any() || oldNics.Any() )
                    {
                        List<IPAddress> ipAddressesOfFilteredInterfaces = ReachableIpAddressesOfInterfaces( m_filteredInterfaces ).ToList();

                        OurContact.ExchangeIpAddressRange( ipAddressesOfFilteredInterfaces );
                        ReachableIpAddresses = ipAddressesOfFilteredInterfaces;

                        String ipsAsStr = ReachableIpAddresses.ToString( showAllPropsOfItems: false, initialTabulation: String.Empty, nameOfEnumerable: "Reachable IP-addresses", nameOfEachItem: "IP" );
                        DsLoggerSet.DefaultLogger.LogInfo( ipsAsStr );

                        m_udpListeners?.Dispose();
                        m_tcpListenersCollection?.Dispose();

                        m_udpSenders?.Dispose();

                        if ( ReachableIpAddresses.Count > 0 )
                        {
                            m_udpSenders = new UdpSendersCollection( ReachableIpAddresses, RunningUdpPort );
                            InitAllListeners();
                        }
                    }
                }

                Boolean isConnectedToNetwork = newNics.Any();
                if ( isConnectedToNetwork )
                {
                    NetworkInterfaceDiscovered?.Invoke( this, new NetworkInterfaceEventArgs
                    {
                        NetworkInterfaces = newNics
                    } );
                }

                // Situation has seen when NetworkAddressChanged is not triggered 
                // (wifi off, but NIC is not disabled, wifi - on, NIC was not changed 
                // so no event). Rebinding fixes this.
                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            }
            catch ( Exception ex )
            {
                DsLoggerSet.DefaultLogger.LogCriticalError( message: $"FindNics failed", ex );
            }
        }

        private IEnumerable<NetworkInterface> InterfacesAfterFilter( IEnumerable<NetworkInterface> interfaces ) =>
            m_networkInterfacesFilter?.Invoke( interfaces ) ?? interfaces;

        private IEnumerable<IPAddress> ReachableIpAddressesOfInterfaces( IList<NetworkInterface> interfaces ) =>
                IpAddressesOfInterfaces( interfaces, UseIpv4, UseIpv6 ).
                    Where( ip => ip.CanBeReachableInCurrentNetwork( interfaces ) );

        private void InitAllListeners()
        {
            InitListeners( ProtocolType.Tcp, messageReceivedHandler: RaiseSpecificTcpReceivedEvent, out m_tcpListenersCollection );
            InitListeners( ProtocolType.Udp, HandleMulticastMessage, out m_udpListeners );
        }

        private void InitListeners<TEventArgs, TListener>(
            ProtocolType protocolType,
            EventHandler<TEventArgs> messageReceivedHandler,
            out ListenersCollection<TEventArgs, TListener> listenersCollection )

            where TEventArgs : MessageEventArgs
            where TListener : class, IDisposable
        {
            switch ( protocolType )
            {
                case ProtocolType.Udp:
                {
                    var udpListenersCollection = new UdpListenersCollection( UseIpv4, UseIpv6, ReachableIpAddresses, RunningUdpPort );
                    listenersCollection = udpListenersCollection as ListenersCollection<TEventArgs, TListener>;

                    break;
                }

                case ProtocolType.Tcp:
                {
                    var tcpListenersCollection = new TcpListenersCollection( UseIpv4, UseIpv6, ReachableIpAddresses, RunningTcpPort );
                    listenersCollection = tcpListenersCollection as ListenersCollection<TEventArgs, TListener>;

                    break;
                }

                default:
                {
                    throw new ArgumentException( message: $"Only UDP and TCP protocol types are supported", paramName: nameof( protocolType ) );
                }
            }

            listenersCollection.MessageReceived += messageReceivedHandler;
            listenersCollection.StartMessagesReceiving();
        }

        /// <summary>
        ///   Called by the MulticastClient when a UDP message is received.
        /// </summary>
        /// <param name="sender">
        ///   The <see cref="Listeners"/> that got the message.
        /// </param>
        /// <param name="eventArgs">
        ///   The received message <see cref="UdpReceiveResult"/>.
        /// </param>
        /// <remarks>
        ///   Decodes the <paramref name="eventArgs"/> and then raises
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
        private void HandleMulticastMessage( Object sender, UdpMessageEventArgs eventArgs )
        {
            if ( ( eventArgs != null ) && ( eventArgs.Buffer != null ) && ( eventArgs.Buffer.Length <= MAX_DATAGRAM_SIZE ) )
            {
                MulticastMessage message;
                try
                {
                    message = new MulticastMessage( eventArgs.Buffer );
                }
                //isn't valid TcpPort
                catch ( ArgumentException ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( ex );

                    // ignore malformed message
                    return;
                }
                catch ( EndOfStreamException ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( ex );

                    // ignore malformed message
                    return;
                }

                // If recently received, then ignore.
                Boolean isRecentlyReceivedSameMess = !m_receivedMessages.TryAdd( message.MessageId );

                Boolean isDsMessage = IsInPortRange( message.TcpPort );

#if RECEIVE_UDP_FROM_OURSELF
                Boolean isMessFromOtherPeerInNetwork = message.ProtocolVersion == ProtocolVersion;
#else
                Boolean isMessFromOtherPeerInNetwork = (message.ProtocolVersion == ProtocolVersion) &&
                    (!message.MachineId.Equals(MachineId, StringComparison.Ordinal));
#endif

                if ( ( !IgnoreDuplicateMessages || !isRecentlyReceivedSameMess ) && isDsMessage && isMessFromOtherPeerInNetwork )
                {
                    eventArgs.SetMessage( message );

                    try
                    {
                        String endPoint = Display.VariableWithValue( nameof( eventArgs.RemoteEndPoint ), eventArgs.RemoteEndPoint, useTab: false );
                        DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Received UPD message with bytes from {endPoint}:\n{message}" );

                        QueryReceived?.Invoke( this, eventArgs );
                    }
                    catch ( TimeoutException ex )
                    {
                        DsLoggerSet.DefaultLogger.LogError( logRecord: $"Receive UDP handler failed {ex.Message}\n" +
                            $"UDP message:\n{message}" );
                    }
                    catch ( SocketException ex )
                    {
                        DsLoggerSet.DefaultLogger.LogError( $"Receive UDP handler failed: {ex.Message}\n" +
                            $"UDP message:\n{message}" );
                    }
                    catch ( EndOfStreamException ex )
                    {
                        DsLoggerSet.DefaultLogger.LogCriticalError( $"Receive UDP handler failed. UDP message:\n{message}", ex );
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
        private void RaiseSpecificTcpReceivedEvent( Object sender, TcpMessageEventArgs receiveResult )
        {
            if ( receiveResult.Buffer.Length < Message.MIN_TCP_CLIENT_MESS_LENGTH )
            {
                DsLoggerSet.DefaultLogger.LogFatal( message: $"Received message with length less then {Message.MIN_TCP_CLIENT_MESS_LENGTH}" );
            }
            else
            {
                DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Started to handle {receiveResult.Buffer.Length} bytes" );

                Message message = null;

                try
                {
                    message = receiveResult.Message<Message>();

                    var ipEndPoint = receiveResult.AcceptedSocket.LocalEndPoint as IPEndPoint;
                    Boolean canBeDsMessage = IsInPortRange( ipEndPoint?.Port );

                    if ( !canBeDsMessage )
                    {
                        DsLoggerSet.DefaultLogger.LogFatal( $"Received message not from DS, because port of {receiveResult.RemoteEndPoint} is {ipEndPoint?.Port}" );
                    }
                    else
                    {
                        switch ( message.MessageOperation )
                        {
                            case MessageOperation.Acknowledge:
                            {
                                HandleReceivedTcpMessage<AcknowledgeTcpMessage>( receiveResult, AnswerReceived );
                                break;
                            }

                            case MessageOperation.Ping:
                            {
                                /// Someone is pinging us.  Register the contact and respond.
                                HandleReceivedTcpMessage<PingRequest>( receiveResult, PingReceived );
                                break;
                            }

                            case MessageOperation.Store:
                            {
                                HandleReceivedTcpMessage<StoreRequest>( receiveResult, StoreReceived );
                                break;
                            }

                            case MessageOperation.FindNode:
                            {
                                HandleReceivedTcpMessage<FindNodeRequest>( receiveResult, FindNodeReceived );
                                break;
                            }

                            case MessageOperation.FindValue:
                            {
                                HandleReceivedTcpMessage<FindValueRequest>( receiveResult, FindValueReceived );
                                break;
                            }

                            case MessageOperation.CheckFileExists:
                            {
                                HandleReceivedTcpMessage<CheckFileExistsRequest>( receiveResult, CheckFileExistsReceived );
                                break;
                            }

                            case MessageOperation.DownloadChunk:
                            {
                                HandleReceivedTcpMessage<DownloadChunkRequest>( receiveResult, DownloadFileReceived );
                                break;
                            }
                        }
                    }
                }
                //received message from malefactor(-s)
                catch ( EndOfStreamException ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( message: $"Received malformed message from {receiveResult.RemoteEndPoint}", ex );

                    //we need to unregister socket in order to be available handle messages from another nodes
                    receiveResult.UnregisterSocket();
                }
                catch ( InvalidDataException ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( $"Received malformed message from {receiveResult.RemoteEndPoint}", ex );

                    //we need to unregister socket in order to be available handle messages from another nodes
                    receiveResult.UnregisterSocket();
                }
                //isn't valid TcpPort
                catch ( ArgumentException ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( $"Received malformed message from {receiveResult.RemoteEndPoint}", ex );

                    //we need to unregister socket in order to be available handle messages from another nodes
                    receiveResult.UnregisterSocket();
                }
                catch ( Exception ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( $"Cannot to handle TCP message: {message}", ex );
                }
            }
        }

        private void HandleReceivedTcpMessage<T>( TcpMessageEventArgs eventArgs, EventHandler<TcpMessageEventArgs> receiveEvent )
            where T : Message
        {
            if ( m_isDsTest )
            {
                Interlocked.Increment( ref m_countOfNowHandlingTcpEvents );
                WaitHandleAllTcpEvents.Reset();
            }

            try
            {
                var request = (T)Activator.CreateInstance( typeof( T ), eventArgs.Buffer );

                Boolean isMalformedMess;
                if ( request is Request kadRequest )
                {
                    isMalformedMess = !m_receivedMessages.TryAdd( kadRequest.RandomID );
                    if ( isMalformedMess )
                    {
                        isMalformedMess = !IgnoreDuplicateMessages;
                    }

                    IContact senderContact = s_dhts[ ProtocolVersion ].OnlineContacts.SingleOrDefault( c => c.MachineId.Equals( kadRequest.SenderMachineId, StringComparison.Ordinal ) );

                    if ( ( senderContact != null ) && ( eventArgs.RemoteEndPoint is IPEndPoint ipEndpoint ) )
                    {
                        senderContact.LastActiveIpAddress = ipEndpoint.Address;
                    }
                }
                else
                {
                    if ( request is AcknowledgeTcpMessage ackMess )
                    {
                        isMalformedMess = !m_receivedMessages.TryAdd( ackMess.MessageId );
                        if ( isMalformedMess )
                        {
                            isMalformedMess = !IgnoreDuplicateMessages;
                        }
                    }
                    else
                    {
                        isMalformedMess = true;
                    }
                }

                if ( !isMalformedMess )
                {
                    eventArgs.SetMessage( request );

                    receiveEvent?.Invoke( this, eventArgs );
                }
            }
            finally
            {
                DsLoggerSet.DefaultLogger.LogInfo( $"Finished to handle {eventArgs.Buffer.Length} bytes" );

                if ( m_isDsTest )
                {
                    Interlocked.Decrement( ref m_countOfNowHandlingTcpEvents );

                    if ( !IsNowHandlingAnyTcpEvent )
                    {
                        //wait, because we can immediately receive FindNodeRequest or DownloadFileRequest
                        Thread.Sleep( m_minTimeOfAbsenceTcpEvents );
                        if ( !IsNowHandlingAnyTcpEvent )
                        {
                            WaitHandleAllTcpEvents.Set();
                        }
                    }
                }
            }
        }
    }
}
