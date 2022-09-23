//#define DOES_CONTAINER_USE
#define IS_IN_LUC

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Extensions;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Kademlia.ClientPool;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.NetworkEventHandlers;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;
using LUC.Services.Implementation.Helpers;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

//access permission to internal members in current project for DiscoveryService.Test
[assembly: InternalsVisibleTo( assemblyName: "DiscoveryService.Test" )]
namespace LUC.DiscoveryServices
{
    // TODO: fix support of several DS 
    /// <summary>
    ///   LightUpon.Cloud Service Discovery maintens the list of IP addresses in LAN. 
    /// </summary>
    public class DiscoveryService : AbstractDsData, IDiscoveryService
    {
        /// <summary>
        ///   Raised when a service instance is shutted down.
        /// </summary>
        public event EventHandler ServiceInstanceShutdown;

        private static readonly ConcurrentDictionary<UInt16, DiscoveryService> s_instances = new ConcurrentDictionary<UInt16, DiscoveryService>();

        private static readonly Random s_random = new Random();

        /// <summary>
        /// Indicates whether DS.Test project uses DS
        /// </summary>
        private Boolean m_isDsTest;

        private Dht m_distributedHashTable;

        private Timer m_tryFindAndUpdateNodesTimer;

        private NetworkEventHandler m_networkEventHandler;

        private ConnectionPool m_connectionPool;

        private ConcurrentDictionary<String, String> m_localBuckets;

        /// <summary>
        /// Creates a new instance of the <see cref="DiscoveryService"/> class.
        /// </summary>
        /// <remarks>
        /// It is only for functional tests
        /// </remarks>
        /// <param name="profile">
        /// <seealso cref="DiscoveryService"/> settings
        /// </param>
        /// <param name="currentUserProvider">
        /// Logged user to LUC app
        /// </param>
        private DiscoveryService( 
            String machineId, 
            UInt16 protocolVersion, 
            Boolean useIpv4, 
            Boolean useIpv6, 
            ConcurrentDictionary<String, String> groups, 
            ICurrentUserProvider currentUserProvider 
        ){
            #region Check useIpv4 and useIpv6 parameters
            Boolean osSupportsIPv4 = Socket.OSSupportsIPv4;
            Boolean osSupportsIPv6 = Socket.OSSupportsIPv6;

            String baseLogRecord = $"Underlying OS or network adapters don't support";
            if ( useIpv4 && !osSupportsIPv4 )
            {
                throw new ArgumentException( message: $"{baseLogRecord} IPv4", paramName: nameof( useIpv4 ) );
            }
            else if ( useIpv6 && !osSupportsIPv6 )
            {
                throw new ArgumentException( $"{baseLogRecord} IPv6", paramName: nameof( useIpv6 ) );
            }
            #endregion
            
            DefaultInit( machineId, protocolVersion, useIpv4, useIpv6, groups, currentUserProvider );
        }

        private DiscoveryService( 
            String machineId, 
            UInt16 protocolVersion, 
            ConcurrentDictionary<String, String> groups, 
            ICurrentUserProvider currentUserProvider 
        ){
            Boolean osSupportsIPv4 = Socket.OSSupportsIPv4;
            Boolean osSupportsIPv6 = Socket.OSSupportsIPv6;

            if ( !osSupportsIPv4 && !osSupportsIPv6 )
            {
                String message = $"OS and network adaptors don't support IPv4 and IPv6";

                DsLoggerSet.DefaultLogger.LogFatal( message );
                throw new InvalidOperationException( message );
            }

            DefaultInit( machineId, protocolVersion, osSupportsIPv4, osSupportsIPv6, groups, currentUserProvider );
        }

        private void DefaultInit(
            String machineId,
            UInt16 protocolVersion,
            Boolean useIpv4,
            Boolean useIpv6,
            ConcurrentDictionary<String, String> groups,
            ICurrentUserProvider currentUserProvider
        ){
            #region Check parameters
            if ( machineId == null )
            {
                throw new ArgumentNullException( nameof( machineId ) );
            }
            else if ( !useIpv4 && !useIpv6 )
            {
                throw new ArgumentException( message: $"{nameof( useIpv4 )} and {nameof( useIpv6 )} aren't in use, so {nameof( DiscoveryService )} will do nothing" );
            }
            else if( groups == null )
            {
                throw new ArgumentNullException( nameof( groups ) );
            }
            else if ( currentUserProvider == null )
            {
                throw new ArgumentNullException( nameof( currentUserProvider ) );
            }
            #endregion

#if !DOES_CONTAINER_USE
            ConfigureFirewall();
#endif

            String dsTestNamespace = "DiscoveryServices.Test";

            var stackTrace = new StackTrace();
            m_isDsTest = stackTrace.ToString().Contains( dsTestNamespace );

            m_localBuckets = groups;

            UseIpv4 = useIpv4;
            UseIpv6 = useIpv6;
            ProtocolVersion = protocolVersion;
            MachineId = machineId;

            m_connectionPool = ConnectionPool.Instance;

            CurrentUserProvider = currentUserProvider;

            //TODO: replace init NetworkEventInvoker here
        }


        /// <inheritdoc/>
        public ConcurrentDictionary<String, String> LocalBuckets =>
            new ConcurrentDictionary<String, String>( m_localBuckets );

        /// <summary>
        /// LightUpon.Cloud Network Event Invoker.
        /// </summary>
        /// <remarks>
        /// Sends UDP queries via the multicast mechachism
        /// defined in <see href="https://tools.ietf.org/html/rfc6762"/>.
        /// Receives UDP queries and answers with TCP/IP/SSL responses.
        /// </remarks>
        public NetworkEventInvoker NetworkEventInvoker { get; private set; }

        public Boolean IsRunning { get; private set; }

        public ICurrentUserProvider CurrentUserProvider { get; set; }

        public IContact OurContact
        {
            get
            {
                if ( IsRunning )
                {
                    return NetworkEventInvoker.OurContact;
                }
                else
                {
                    throw new InvalidOperationException( $"{nameof( DiscoveryService )} still isn't started" );
                }
            }
        }

        public static DiscoveryService Instance(
            String machineId,
            UInt16 protocolVersion,
            Boolean useIpv4,
            Boolean useIpv6,
            ConcurrentDictionary<String, String> userGroups,
            ICurrentUserProvider currentUserProvider
        ) => Instance( 
                 protocolVersion, 
                 userGroups, 
                 () => new DiscoveryService( machineId, protocolVersion, useIpv4, useIpv6, userGroups, currentUserProvider ) 
             );

        /// <summary>
        /// Get an instance of the <seealso cref="DiscoveryService"/> class with some <paramref name="protocolVersion"/>. 
        /// If it has not been created before with this parameter, it will be initialized
        /// </summary>
        public static DiscoveryService Instance( 
            String machineId, 
            UInt16 protocolVersion, 
            ICurrentUserProvider currentUserProvider, 
            ConcurrentDictionary<String, String> userGroups 
        ) => Instance(
                 protocolVersion,
                 userGroups,
                 () => new DiscoveryService( machineId, protocolVersion, userGroups, currentUserProvider )
             );

        /// <summary>
        /// Get before created instance of the <seealso cref="DiscoveryService"/> class
        /// </summary>
        /// <param name="protocolVersion">
        /// Version of protocol which is used by before created <seealso cref="DiscoveryService"/>
        /// </param>
        /// <exception cref="ArgumentException">
        /// The instance with <paramref name="protocolVersion"/> still wasn't created
        /// </exception>
        public static DiscoveryService BeforeCreatedInstance( UInt16 protocolVersion )
        {
            Boolean wasCreated = s_instances.TryGetValue( protocolVersion, out DiscoveryService discoveryService );
            if ( wasCreated )
            {
                return discoveryService;
            }
            else
            {
                String protocolVersionAsStr = Display.VariableWithValue( nameof( protocolVersion ), protocolVersion, useTab: false );
                throw new ArgumentException( message: $"{nameof( DiscoveryService )} with {protocolVersionAsStr} still wasn't created" );
            }
        }

        public void ReplaceAllBuckets( IDictionary<String, String> bucketsWithPathToSslCert )
        {
            if ( bucketsWithPathToSslCert != null )
            {
                if ( !m_localBuckets.Equals<String, String>( bucketsWithPathToSslCert ) )
                {
                    ClearAllLocalBuckets();

                    foreach ( KeyValuePair<String, String> bucketDescription in bucketsWithPathToSslCert )
                    {
                        TryAddNewBucket( bucketDescription.Key, bucketDescription.Value, isAdded: out _ );
                    }
                }
            }
            else
            {
                throw new ArgumentNullException( nameof( bucketsWithPathToSslCert ) );
            }
        }

        ///<inheritdoc/>
        public void TryAddNewBucket( String bucketLocalName, String pathToSslCert, out Boolean isAdded )
        {
            if ( !String.IsNullOrWhiteSpace( bucketLocalName ) && !String.IsNullOrWhiteSpace( pathToSslCert ) )
            {
                //if doesn't contain bucketLocalName or pathToSslCert then call AddOrUpdate and set isAdded to true
                isAdded = m_localBuckets.TryAdd( bucketLocalName, pathToSslCert );
                if ( isAdded )
                {
                    NetworkEventInvoker?.OurContact?.TryAddBucketLocalName( bucketLocalName, out isAdded );

                    if ( !isAdded )
                    {
                        m_localBuckets.TryRemove( bucketLocalName, value: out _ );
                    }
                }
            }
            else
            {
                throw new ArgumentException( $"{nameof( bucketLocalName )} or {nameof( pathToSslCert )} is null or white space" );
            }
        }

        public void ClearAllLocalBuckets()
        {
            m_localBuckets.Clear();
            NetworkEventInvoker?.OurContact.ClearAllLocalBuckets();
        }

        /// <inheritdoc/>
        public void TryRemoveBucket( String bucketLocalName, out Boolean isRemoved )
        {
            if ( ( bucketLocalName != null ) && m_localBuckets.ContainsKey( bucketLocalName ) )
            {
                isRemoved = m_localBuckets.TryRemove( bucketLocalName, out String fullPathToSslCert );

                if ( isRemoved )
                {
                    NetworkEventInvoker?.OurContact?.TryRemoveBucketLocalName( bucketLocalName, out isRemoved );

                    if ( !isRemoved )
                    {
                        m_localBuckets.TryAdd( bucketLocalName, fullPathToSslCert );
                    }
                }
            }
            else
            {
                isRemoved = false;
            }
        }

        /// <inheritdoc/>
        public void Start()
        {
            if ( !IsRunning )
            {
                DsLoggerSet.DefaultLogger.LogInfo( logRecord: "DS (Discovery Service) is starting" );

                InitNetworkEventInvoker( networkIterfacesFilter: null );

                if ( !m_isDsTest )
                {
                    m_tryFindAndUpdateNodesTimer = new Timer( TryFindNewServicesTick, state: this, dueTime: TimeSpan.Zero, DsConstants.IntervalFindNewServices );
                }

                IsRunning = true;

                DsLoggerSet.DefaultLogger.LogInfo( "DS is started" );
            }
        }

        /// <inheritdoc/>
        public void TryFindAllNodes()
        {
            if ( IsRunning )
            {
                NetworkEventInvoker.SendMulticastsAsync( IoBehavior.Synchronous ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else
            {
                throw new InvalidOperationException( message: "First you need to start discovery service" );
            }
        }

        /// <inheritdoc/>
        public void Stop() =>
            Stop(allowReuseService: false);

        public void Stop(Boolean allowReuseService)
        {
            if (IsRunning)
            {
                IsRunning = false;

                m_tryFindAndUpdateNodesTimer?.Change( dueTime: Timeout.Infinite, period: Timeout.Infinite );
                m_tryFindAndUpdateNodesTimer?.Dispose();

                NetworkEventInvoker?.Stop();
                NetworkEventInvoker = null;

                //EventArgs is empty event args
                ServiceInstanceShutdown?.Invoke(sender: this, e: new EventArgs());

                m_connectionPool.ClearPoolAsync(IoBehavior.Synchronous, respectMinPoolSize: false, allowReuseService, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
            }
        }


        public List<IContact> OnlineContacts() =>
            m_distributedHashTable.OnlineContacts;

        //TODO: check SSL certificate with SNI
        /// <summary>
        ///  Sends TCP message of "acknowledge" custom type to <seealso cref="TcpMessageEventArgs.RemoteEndPoint"/> using <seealso cref="AllNodesRecognitionMessage.TcpPort"/>
        ///  It is added to <seealso cref="NetworkEventInvoker.QueryReceived"/>. 
        /// </summary>
        /// <param name="sender">
        ///  Object which invoked event <seealso cref="NetworkEventInvoker.QueryReceived"/>
        /// </param>
        /// <param name="e">
        ///  Information about UDP contact, that we have received.
        /// </param>
        internal async ValueTask SendAcknowledgeTcpMessageAsync( UdpMessageEventArgs eventArgs, IoBehavior ioBehavior )
        {
            if(!IsRunning)
            {
                throw new InvalidOperationException( message: "First you need to start discovery service" );
            }
            else
            {
                AllNodesRecognitionMessage multicastMessage = eventArgs.Message<AllNodesRecognitionMessage>( whetherReadMessage: false );

                if ( ( multicastMessage != null ) && ( eventArgs?.RemoteEndPoint is IPEndPoint ipEndPoint ) )
                {
                    Boolean isTheSameNetwork = ipEndPoint.Address.CanBeReachable();

                    if ( isTheSameNetwork )
                    {
                        IContact sendingContact = NetworkEventInvoker.OurContact;

                        var tcpMessage = new AcknowledgeTcpMessage(
                           messageId: (UInt32)s_random.Next( maxValue: Int32.MaxValue ),
                           MachineId,
                           sendingContact.KadId.Value,
                           RunningTcpPort,
                           ProtocolVersion,
                           groupsIds: m_localBuckets?.Keys?.ToList()
                       );

                        Byte[] bytesToSend = tcpMessage.ToByteArray();

                        var remoteEndPoint = new IPEndPoint( ipEndPoint.Address, multicastMessage.TcpPort );

                        await ConcurrencyErrorForcer.TryForceAsync().ConfigureAwait( continueOnCapturedContext: false );
                        ConnectionPool.Socket client = null;

                        try
                        {
                            client = await m_connectionPool.SocketAsync( remoteEndPoint, DsConstants.ConnectTimeout,
                               ioBehavior, DsConstants.TimeWaitSocketReturnedToPool ).ConfigureAwait( false );

                            await ConcurrencyErrorForcer.TryForceAsync().ConfigureAwait( false );
                            client = await client.DsSendWithAvoidNetworkErrorsAsync( bytesToSend, DsConstants.SendTimeout,
                               DsConstants.ConnectTimeout, ioBehavior ).ConfigureAwait( false );

#if DEBUG
                            DsLoggerSet.DefaultLogger.LogInfo( $"{tcpMessage.GetType().Name} is sent to {client.Id}:\n" +
                                                            $"{tcpMessage}\n" );
#endif
                        }
                        catch ( TimeoutException ex )
                        {
                            DsLoggerSet.DefaultLogger.LogError( ex.Message );
                        }
                        catch ( SocketException ex )
                        {
                            DsLoggerSet.DefaultLogger.LogFatal( ex.Message );
                        }
                        catch ( InvalidOperationException ex )
                        {
                            DsLoggerSet.DefaultLogger.LogError( ex.ToString() );
                        }
                        finally
                        {
                            if ( client != null )
                            {
                                await ConcurrencyErrorForcer.TryForceAsync().ConfigureAwait( false );

                                await client.ReturnToPoolAsync( DsConstants.ConnectTimeout, ioBehavior ).ConfigureAwait( false );
                            }
                        }
                    }
                }
                else
                {
                    throw new ArgumentException( message: $"Something is wrong with {nameof( eventArgs )} in {nameof( SendAcknowledgeTcpMessageAsync )}: " +
                        $"{Display.ToString( eventArgs )}\n{multicastMessage}" );
                }
            }
        }

        private static DiscoveryService Instance( UInt16 protocolVersion, ConcurrentDictionary<String, String> groups, Func<DiscoveryService> creator )
        {
            Boolean isAppreciateDsAlreadyCreated = s_instances.TryGetValue( protocolVersion, out DiscoveryService takenDiscoveryService );

            if ( isAppreciateDsAlreadyCreated )
            {
                ConcurrentDictionary<String, String> validGroups = groups ?? new ConcurrentDictionary<String, String>();
                takenDiscoveryService.ReplaceAllBuckets( validGroups );
            }
            else
            {
                DiscoveryService newDs = creator();
                s_instances.AddOrUpdate( protocolVersion, _ => newDs, ( _, previousDs ) => newDs );

                //to get last initialized DS
                s_instances.TryGetValue( protocolVersion, out takenDiscoveryService );
            }

            return takenDiscoveryService;
        }

        private void ConfigureFirewall()
        {
            var assembly = Assembly.GetEntryAssembly();

            if ( assembly == null )
            {
                assembly = Assembly.GetExecutingAssembly();
            }

            String pathToExeFile = assembly.Location;

#if IS_IN_LUC
            String appVersion = assembly.GetName().Version.ToString();
            String appName = $"Light Upon Cloud {appVersion}";
#else
            String appName = "Discovery Service";
#endif

            try
            {
                FirewallHelper firewallHelper = FirewallHelper.Instance;

                firewallHelper.GrantAppAuthInAnyNetworksInAllPorts( pathToExeFile, appName );
            }
            catch ( Exception ex )
            {
                DsLoggerSet.DefaultLogger.LogCriticalError( message: $"{appName} cannot be granted in any networks", ex );
                throw;
            }
        }

        private void TryFindNewServicesTick( Object timerState )
        {
#if !CONNECTION_POOL_TEST
            //we don't need to say about us another nodes, when we aren't in any not Kademlia buckets,
            //because no one will download files from us, while bucket refresh will not be started
            if ( !m_distributedHashTable.ExistsAnyOnlineContact && m_localBuckets.Any()  )
            {
#endif
                try
                {
                    TryFindAllNodes();
                }
                //DS is stopped
                catch ( InvalidOperationException )
                {
                    ;//do nothing
                }
#if !CONNECTION_POOL_TEST
            }
#endif
        }

        private void InitNetworkEventInvoker( Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> networkIterfacesFilter = null )
        {
            NetworkEventInvoker = new NetworkEventInvoker(
                MachineId,
                UseIpv4,
                UseIpv6,
                ProtocolVersion,
                m_localBuckets.Keys,
                m_isDsTest,
                networkIterfacesFilter
            );

            //puts in events of NetworkEventInvoker sendings response
            m_networkEventHandler = new NetworkEventHandler( this, NetworkEventInvoker, CurrentUserProvider );

            NetworkEventInvoker.QueryReceived += ( invokerEvent, eventArgs ) => 
                SendAcknowledgeTcpMessageAsync( eventArgs, IoBehavior.Synchronous ).ConfigureAwait( continueOnCapturedContext: false );
            NetworkEventInvoker.AnswerReceived += NetworkEventInvoker.Bootstrap;
            NetworkEventInvoker.NetworkInterfaceDiscovered += UpdateConnectionPoolAsync;

            NetworkEventInvoker.Start();

            m_distributedHashTable = NetworkEventInvoker.DistributedHashTable( ProtocolVersion );
        }

        private async void UpdateConnectionPoolAsync( Object sender, NetworkInterfaceEventArgs eventArgs )
        {
            if ( m_connectionPool.ConnectionSettings.ConnectionBackgroundReset )
            {
                m_connectionPool.CancelRecoverConnections();

                try
                {
                    await m_connectionPool.TryRecoverAllConnectionsAsync( DsConstants.TimeWaitSocketReturnedToPool ).ConfigureAwait( continueOnCapturedContext: false );
                }
                catch ( OperationCanceledException )
                {
                    ;//do nothing
                }
            }
        }
    }
}
