using System;
using System.Net;
using System.Net.Sockets;
using LUC.DiscoveryServices.Messages;
using System.Linq;
using System.Collections.Concurrent;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.DiscoveryServices.Kademlia;
using System.Collections.Generic;
using LUC.DiscoveryServices.Kademlia.ClientPool;
using System.Threading;
using System.Numerics;
using System.Threading.Tasks;
using System.IO;
using LUC.Services.Implementation.Models;
using LUC.Interfaces.Extensions;
using LUC.Interfaces;
using LUC.DiscoveryServices.NetworkEventHandlers;
using System.Runtime.CompilerServices;
using LUC.DiscoveryServices.Common;
using System.ComponentModel.Composition;
using Castle.Core.Internal;
using System.Reflection;
using LUC.Services.Implementation;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using LUC.DiscoveryServices.Interfaces;

//access permission to internal members in current project for DiscoveryService.Test
[assembly: InternalsVisibleTo( assemblyName: "DiscoveryService.Test" )]
[assembly: InternalsVisibleTo( InternalsVisible.ToDynamicProxyGenAssembly2 )]
namespace LUC.DiscoveryServices
{
    /// <summary>
    ///   LightUpon.Cloud Service Discovery maintens the list of IP addresses in LAN
    /// </summary>
    [Export(typeof(IDiscoveryService))]
    public class DiscoveryService : AbstractService, IDiscoveryService
    {
        /// <summary>
        ///   Raised when a servive instance is shutting down.
        /// </summary>
        /// <value>
        ///   Contains the service instance ip.
        /// </value>
        /// <remarks>
        ///   <see cref="DiscoveryService"/> passively monitors the network for any answers.
        ///   When an answer containing type 1 received this event is raised.
        /// </remarks>
        public event EventHandler<TcpMessageEventArgs> ServiceInstanceShutdown;

        private Dht m_distributedHashTable;

        private ForcingConcurrencyError m_forceConcurrencyError;

        private NetworkEventHandler m_networkEventHandler;

        private ConnectionPool m_connectionPool;

        private ConcurrentDictionary<String, String> m_supportedBuckets;

        private readonly ICurrentUserProvider m_currentUserProvider;

        /// <summary>
        /// Creates a new instance of the <see cref="DiscoveryService"/> class.
        /// </summary>
        /// <param name="profile">
        /// <seealso cref="DiscoveryService"/> settings
        /// </param>
        public DiscoveryService( ServiceProfile profile )
        {
            if ( profile != null )
            {
                InitDiscoveryService( profile );

                InitNetworkEventInvoker();
            }
            else
            {
                throw new ArgumentNullException( nameof( profile ) );
            }
        }

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
        internal DiscoveryService( ServiceProfile profile, ICurrentUserProvider currentUserProvider )
        {
            if ( profile != null )
            {
                InitDiscoveryService( profile );

                m_currentUserProvider = currentUserProvider;
                InitNetworkEventInvoker();

                SettingsService = new SettingsService
                {
                    CurrentUserProvider = m_currentUserProvider
                };

                LoggingService = new LoggingService
                {
                    SettingsService = SettingsService
                };
            }
            else
            {
                throw new ArgumentNullException( nameof( profile ) );
            }
        }

        /// <summary>
        /// If user of ServiceDiscovery forget to call method Stop
        /// </summary>
        ~DiscoveryService()
        {
            Stop();
        }

        /// <inheritdoc/>
        public ConcurrentDictionary<EndPoint, String> KnownIps { get; protected set; } =
            new ConcurrentDictionary<EndPoint, String>();

        /// <inheritdoc/>
        public ConcurrentDictionary<String, String> LocalBuckets() =>
            new ConcurrentDictionary<String, String>( m_supportedBuckets );

        internal Boolean IsRunning { get; private set; }

        internal KademliaId ContactId
        {
            get
            {
                if ( IsRunning )
                {
                    return NetworkEventInvoker.OurContact.KadId;
                }
                else
                {
                    throw new InvalidOperationException( $"{nameof( DiscoveryService )} is stopped" );
                }
            }
        }

        /// <summary>
        /// LightUpon.Cloud Network Event Invoker.
        /// </summary>
        /// <remarks>
        /// Sends UDP queries via the multicast mechachism
        /// defined in <see href="https://tools.ietf.org/html/rfc6762"/>.
        /// Receives UDP queries and answers with TCP/IP/SSL responses.
        /// </remarks>
        internal NetworkEventInvoker NetworkEventInvoker { get; private set; }

        ///<inheritdoc/>
        public void TryAddNewBucketLocalName( String bucketLocalName, String pathToSslCert, out Boolean isAdded)
        {
            if(!String.IsNullOrWhiteSpace(bucketLocalName) && !String.IsNullOrWhiteSpace(pathToSslCert))
            {
                isAdded = m_supportedBuckets.TryAdd( bucketLocalName, pathToSslCert );
                if(isAdded)
                {
                    NetworkEventInvoker.OurContact.TryAddBucketLocalName( bucketLocalName, out isAdded );

                    if(!isAdded)
                    {
                        m_supportedBuckets.TryRemove( bucketLocalName, value: out _ );
                    }
                }
            }
            else
            {
                throw new ArgumentException($"{nameof(bucketLocalName)} or {nameof(pathToSslCert)} is null or white space");
            }
        }

        /// <inheritdoc/>
        public void TryRemoveBucket(String bucketLocalName, out Boolean isRemoved )
        {
            if ( ( bucketLocalName != null ) && ( m_supportedBuckets.ContainsKey( bucketLocalName ) ) )
            {
                isRemoved = m_supportedBuckets.TryRemove( bucketLocalName, out String fullPathToSslCert );

                if(isRemoved)
                {
                    NetworkEventInvoker.OurContact.TryRemoveBucketLocalName( bucketLocalName, out isRemoved );

                    if(!isRemoved)
                    {
                        m_supportedBuckets.TryAdd( bucketLocalName, fullPathToSslCert );
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
                LoggingService.LogInfo( "DS (Discovery Service) is starting" );

                if ( NetworkEventInvoker == null )
                {
                    InitNetworkEventInvoker();
                }
                m_forceConcurrencyError = new ForcingConcurrencyError();
                NetworkEventInvoker.Start();

                IsRunning = true;

                LoggingService.LogInfo( "DS is started" );
            }
        }

        /// <inheritdoc/>
        public void QueryAllServices()
        {
            if ( IsRunning )
            {
                NetworkEventInvoker.SendQuery();
            }
            else
            {
                throw new InvalidOperationException( "First you need to start discovery service" );
            }
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if ( IsRunning )
            {
                NetworkEventInvoker.Stop();
                NetworkEventInvoker = null;

                ServiceInstanceShutdown?.Invoke( sender: this, new TcpMessageEventArgs() );

                m_connectionPool.ClearPoolAsync( IOBehavior.Synchronous, respectMinPoolSize: false, CancellationToken.None ).GetAwaiter().GetResult();
                m_forceConcurrencyError?.Dispose();

                IsRunning = false;
            }
        }

        internal List<Contact> OnlineContacts() =>
            m_distributedHashTable.OnlineContacts;

        //TODO: check SSL certificate with SNI
        /// <summary>
        ///  Sends TCP message of "acknowledge" custom type to <seealso cref="TcpMessageEventArgs.RemoteEndPoint"/> using <seealso cref="UdpMessage.TcpPort"/>
        ///  It is added to <seealso cref="NetworkEventInvoker.QueryReceived"/>. 
        /// </summary>
        /// <param name="sender">
        ///  Object which invoked event <seealso cref="NetworkEventInvoker.QueryReceived"/>
        /// </param>
        /// <param name="e">
        ///  Information about UDP sender, that we have received.
        /// </param>
        internal async Task SendTcpMessageAsync( Object sender, UdpMessageEventArgs eventArgs )
        {
            UdpMessage udpMessage = eventArgs.Message<UdpMessage>( whetherReadMessage: false );

            if ( ( udpMessage != null ) && ( eventArgs?.RemoteEndPoint is IPEndPoint ipEndPoint ) )
            {
                ForcingConcurrencyError.TryForce();

                Boolean isTheSameNetwork = IpAddressFilter.IsIpAddressInTheSameNetwork( ipEndPoint.Address );

                if ( isTheSameNetwork )
                {
                    Contact sendingContact = NetworkEventInvoker.OurContact;

                    Random random = new Random();
                    AcknowledgeTcpMessage tcpMessage = new AcknowledgeTcpMessage(
                       messageId: (UInt32)random.Next( maxValue: Int32.MaxValue ),
                       MachineId,
                       sendingContact.KadId.Value,
                       RunningTcpPort,
                       ProtocolVersion,
                       groupsIds: LocalBuckets()?.Keys?.ToList()
                   );

                    ForcingConcurrencyError.TryForce();
                    Byte[] bytesToSend = tcpMessage.ToByteArray();

                    IPEndPoint remoteEndPoint = new IPEndPoint( ipEndPoint.Address, (Int32)udpMessage.TcpPort );
                    ConnectionPoolSocket client = null;
                    try
                    {
                        client = await m_connectionPool.SocketAsync( remoteEndPoint, Constants.ConnectTimeout,
                           IOBehavior.Asynchronous, Constants.TimeWaitSocketReturnedToPool ).ConfigureAwait( continueOnCapturedContext: false );

                        ForcingConcurrencyError.TryForce();
                        client = await client.DsSendWithAvoidErrorsInNetworkAsync( bytesToSend, Constants.SendTimeout,
                           Constants.ConnectTimeout, IOBehavior.Asynchronous ).ConfigureAwait( false );
                        ForcingConcurrencyError.TryForce();
                    }
                    catch ( TimeoutException ex )
                    {
                        LoggingService.LogError( ex.ToString() );
                    }
                    catch ( SocketException ex )
                    {
                        LoggingService.LogError( ex.ToString() );
                    }
                    catch ( AggregateException ex )
                    {
                        LoggingService.LogError( ex.ToString() );
                    }
                    catch ( ObjectDisposedException ex )
                    {
                        LoggingService.LogError( ex.ToString() );
                    }
                    finally
                    {
                        client?.ReturnedToPool();
                    }
                }
            }
            else
            {
                throw new ArgumentException( $"Bad format of {nameof( eventArgs )}" );
            }
        }

        private void AddEndpoint( Object sender, TcpMessageEventArgs e )
        {
            AcknowledgeTcpMessage tcpMessage = e.Message<AcknowledgeTcpMessage>( whetherReadMessage: false );
            if ( ( tcpMessage != null ) && ( e.RemoteEndPoint is IPEndPoint ipEndPoint ) )
            {
                foreach ( String groupId in tcpMessage.GroupIds )
                {
                    if ( !KnownIps.TryAdd( ipEndPoint, groupId ) )
                    {
                        KnownIps.TryRemove( ipEndPoint, out _ );
                        KnownIps.TryAdd( ipEndPoint, groupId );
                    }
                }
            }
            else
            {
                throw new ArgumentException( $"Bad format of {nameof( e )}" );
            }
        }

        private void InitDiscoveryService( ServiceProfile profile )
        {
            m_supportedBuckets = profile.GroupsSupported;

            UseIpv4 = profile.UseIpv4;
            UseIpv6 = profile.UseIpv6;
            ProtocolVersion = profile.ProtocolVersion;
            MachineId = profile.MachineId;
            m_connectionPool = ConnectionPool.Instance();
        }

        private void InitNetworkEventInvoker()
        {
            NetworkEventInvoker = new NetworkEventInvoker( MachineId, UseIpv4, UseIpv6, ProtocolVersion, LocalBuckets().Keys );

            m_networkEventHandler = new NetworkEventHandler( this, NetworkEventInvoker, m_currentUserProvider );//puts in events of NetworkEventInvoker sendings response
            NetworkEventInvoker.QueryReceived += async ( invokerEvent, eventArgs ) => await SendTcpMessageAsync( invokerEvent, eventArgs );

            NetworkEventInvoker.AnswerReceived += AddEndpoint;
            NetworkEventInvoker.AnswerReceived += NetworkEventInvoker.Bootstrap;

            m_distributedHashTable = NetworkEventInvoker.DistributedHashTable( ProtocolVersion );
        }
    }
}
