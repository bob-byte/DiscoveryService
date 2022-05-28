//#define IS_IN_LUC

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Extensions;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Kademlia.ClientPool;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.NetworkEventHandlers;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Services.Implementation.Helpers;

using Nito.AsyncEx.Synchronous;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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
[assembly: InternalsVisibleTo(assemblyName: "DiscoveryService.Test")]
namespace LUC.DiscoveryServices
{
    /// <summary>
    ///   LightUpon.Cloud Service Discovery maintens the list of IP addresses in LAN. 
    ///   Realizes Singleton pattern per <seealso cref="AbstractDsData.ProtocolVersion"/>
    /// </summary>
    [Export( typeof( IDiscoveryService ) )]
    public class DiscoveryService : AbstractDsData, IDiscoveryService
    {
        /// <summary>
        ///   Raised when a service instance is shutted down.
        /// </summary>
        public event EventHandler ServiceInstanceShutdown;

        private static readonly ConcurrentDictionary<UInt16, DiscoveryService> s_instances = new ConcurrentDictionary<UInt16, DiscoveryService>();

        /// <summary>
        /// Indicates whether DS.Test project is running
        /// </summary>
        private Boolean m_isDsTest;

        private Dht m_distributedHashTable;

        private ForcingConcurrencyError m_forceConcurrencyError;

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
        private DiscoveryService( ServiceProfile profile, ICurrentUserProvider currentUserProvider )
        {
            if ( ( profile != null ) && ( currentUserProvider != null ) )
            {
                InitDiscoveryService( profile );

                CurrentUserProvider = currentUserProvider;
            }
            else
            {
                throw new ArgumentNullException( nameof( profile ) );
            }
        }

        /// <inheritdoc/>
        public ConcurrentDictionary<EndPoint, List<String>> KnownIps { get; protected set; }

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

        /// <summary>
        /// Get a new instance of the <seealso cref="DiscoveryService"/> class with the same protocol version. 
        /// If it has not been created before with this protocol version, it will be initialized
        /// </summary>
        /// <param name="profile">
        /// <seealso cref="DiscoveryService"/> settings
        /// </param>
        public static DiscoveryService Instance( ServiceProfile serviceProfile, ICurrentUserProvider currentUserProvider ) =>
            Instance( serviceProfile, () => new DiscoveryService( serviceProfile, currentUserProvider ) );

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

        public void Start() =>
            Start( networkIterfacesFilter: null );

        /// <inheritdoc/>
        public void Start( Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> networkIterfacesFilter )
        {
            if ( !IsRunning )
            {
                DsLoggerSet.DefaultLogger.LogInfo( logRecord: "DS (Discovery Service) is starting" );

                InitNetworkEventInvoker( networkIterfacesFilter );

                m_forceConcurrencyError = new ForcingConcurrencyError();

                if ( !m_isDsTest )
                {
                    TimeSpan intervalFindNewServices;
#if CONNECTION_POOL_TEST
                    intervalFindNewServices = TimeSpan.FromSeconds( value: 3 );
#elif DEBUG
                    intervalFindNewServices = TimeSpan.FromMinutes( 1 );
#else               
                    intervalFindNewServices = TimeSpan.FromMinutes( 3 );
#endif

                    m_tryFindAndUpdateNodesTimer = new Timer( TryFindNewServicesTick, state: this, dueTime: TimeSpan.Zero, intervalFindNewServices );
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

                m_tryFindAndUpdateNodesTimer?.Dispose();

                NetworkEventInvoker?.Stop();
                NetworkEventInvoker = null;

                //EventArgs is empty event args
                ServiceInstanceShutdown?.Invoke(sender: this, e: new EventArgs());

                m_connectionPool.ClearPoolAsync(IoBehavior.Synchronous, respectMinPoolSize: false, allowReuseService, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
                m_forceConcurrencyError?.Dispose();
            }
        }


        public List<IContact> OnlineContacts() =>
            m_distributedHashTable.OnlineContacts;

        //TODO: check SSL certificate with SNI
        /// <summary>
        ///  Sends TCP message of "acknowledge" custom type to <seealso cref="TcpMessageEventArgs.RemoteEndPoint"/> using <seealso cref="MulticastMessage.TcpPort"/>
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
                MulticastMessage multicastMessage = eventArgs.Message<MulticastMessage>( whetherReadMessage: false );

                if ( ( multicastMessage != null ) && ( eventArgs?.RemoteEndPoint is IPEndPoint ipEndPoint ) )
                {
                    ForcingConcurrencyError.TryForce();

                    Boolean isTheSameNetwork = ipEndPoint.Address.CanBeReachableInCurrentNetwork();

                    if ( isTheSameNetwork )
                    {
                        IContact sendingContact = NetworkEventInvoker.OurContact;

                        var random = new Random();
                        var tcpMessage = new AcknowledgeTcpMessage(
                           messageId: (UInt32)random.Next( maxValue: Int32.MaxValue ),
                           MachineId,
                           sendingContact.KadId.Value,
                           RunningTcpPort,
                           ProtocolVersion,
                           groupsIds: m_localBuckets?.Keys?.ToList()
                       );

                        ForcingConcurrencyError.TryForce();
                        Byte[] bytesToSend = tcpMessage.ToByteArray();

                        var remoteEndPoint = new IPEndPoint( ipEndPoint.Address, (Int32)multicastMessage.TcpPort );
                        ConnectionPool.Socket client = null;

                        try
                        {
                            client = await m_connectionPool.SocketAsync( remoteEndPoint, DsConstants.ConnectTimeout,
                               ioBehavior, DsConstants.TimeWaitSocketReturnedToPool ).ConfigureAwait( continueOnCapturedContext: false );

                            ForcingConcurrencyError.TryForce();
                            client = await client.DsSendWithAvoidNetworkErrorsAsync( bytesToSend, DsConstants.SendTimeout,
                               DsConstants.ConnectTimeout, ioBehavior ).ConfigureAwait( false );

#if DEBUG
                            DsLoggerSet.DefaultLogger.LogInfo( $"{tcpMessage.GetType().Name} is sent to {client.Id}:\n" +
                                                            $"{tcpMessage}\n" );
#endif
                            ForcingConcurrencyError.TryForce();
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

        private static DiscoveryService Instance( ServiceProfile serviceProfile, Func<DiscoveryService> creator )
        {
            if ( serviceProfile != null )
            {
                Boolean isAppreciateDsAlreadyCreated = s_instances.TryGetValue( serviceProfile.ProtocolVersion, out DiscoveryService takenDiscoveryService );
                if ( isAppreciateDsAlreadyCreated )
                {
                    takenDiscoveryService.ReplaceAllBuckets(serviceProfile.GroupsSupported);
                }
                else
                {
                    DiscoveryService newDs = creator();
                    s_instances.AddOrUpdate(serviceProfile.ProtocolVersion, (protocolVersion) => newDs, (protocolVersion, previousDs) => newDs);

                    //to get last initialized DS
                    s_instances.TryGetValue(serviceProfile.ProtocolVersion, out takenDiscoveryService);
                }

                return takenDiscoveryService;
            }
            else
            {
                throw new ArgumentNullException( nameof( serviceProfile ) );
            }
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
                DsLoggerSet.DefaultLogger.LogCriticalError( message: $"{appName} cannot be granted in private networks", ex );
                throw;
            }
        }

        private void AddEndpoint( Object sender, TcpMessageEventArgs e )
        {
            AcknowledgeTcpMessage tcpMessage = e.Message<AcknowledgeTcpMessage>( whetherReadMessage: false );
            if ( ( tcpMessage != null ) && ( e.RemoteEndPoint is IPEndPoint ipEndPoint ) )
            {
                KnownIps.AddOrUpdate(
                    ipEndPoint,
                    addValueFactory: ( ip ) => tcpMessage.BucketIds,
                    updateValueFactory: ( ip, previousBuckets ) => tcpMessage.BucketIds
                );
            }
            else
            {
                throw new ArgumentException( $"Bad format of {nameof( e )}" );
            }
        }

        private void InitDiscoveryService( ServiceProfile profile )
        {
            KnownIps = new ConcurrentDictionary<EndPoint, List<String>>();

#if !IS_IN_LUC
            ConfigureFirewall();
#endif

#if DEBUG
            //get assembly of executable file
            var assembly = Assembly.GetEntryAssembly();

#if IS_IN_LUC
            String dsTestAssebmlyName = "LUC.LUC.DiscoveryServices.Test";
#else
            String dsTestAssebmlyName = "DiscoveryService.Test";
#endif

            //if executable file does not use DS, then get
            //running .dll (case when unit tests uses DS) 
            if ( assembly != null )
            {
                m_isDsTest = assembly.FullName.Contains( dsTestAssebmlyName );
            }
            else
            {
                var stackTrace = new StackTrace();
                m_isDsTest = stackTrace.ToString().Contains( dsTestAssebmlyName );
            }
#else
            m_isDsTest = false;
#endif

            m_localBuckets = profile.GroupsSupported;

            UseIpv4 = profile.UseIpv4;
            UseIpv6 = profile.UseIpv6;
            ProtocolVersion = profile.ProtocolVersion;
            MachineId = profile.MachineId;

            m_connectionPool = ConnectionPool.Instance;
        }

        private void TryFindNewServicesTick( Object timerState )
        {
#if !DS_TEST
            List<IContact> onlineContacts = OnlineContacts();

            //we don't need to say about us another nodes, when we aren't in any not Kademlia buckets,
            //because no one will download files from us, while bucket refresh will not be started
            if ( onlineContacts.Count == 0 && m_localBuckets.Any() )
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
#if !DS_TEST
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

            NetworkEventInvoker.AnswerReceived += AddEndpoint;
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
