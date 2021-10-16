//#define IS_IN_LUC

using System;
using System.Net;
using System.Net.Sockets;
using LUC.DiscoveryService.Messages;
using System.Linq;
using System.Collections.Concurrent;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.DiscoveryService.Kademlia;
using System.Collections.Generic;
using LUC.DiscoveryService.Kademlia.ClientPool;
using System.Threading;
using System.Numerics;
using System.Threading.Tasks;
using System.IO;
using LUC.Services.Implementation.Models;
using LUC.Interfaces.Extensions;
using LUC.Interfaces;
using LUC.DiscoveryService.NetworkEventHandlers;
using System.Runtime.CompilerServices;
using LUC.DiscoveryService.Common;
using System.ComponentModel.Composition;
using Castle.Core.Internal;
using System.Reflection;
using LUC.Services.Implementation;

//access permission to internal members in current project for DiscoveryService.Test
[assembly: InternalsVisibleTo( "DiscoveryService.Test" )]
[assembly: InternalsVisibleTo( InternalsVisible.ToDynamicProxyGenAssembly2 )]
namespace LUC.DiscoveryService
{
    /// <summary>
    ///   LightUpon.Cloud Service Discovery maintens the list of IP addresses in LAN
    /// </summary>
    public class DiscoveryService : AbstractService
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

        private readonly Dht m_distributedHashTable;

        private readonly ForcingConcurrencyError m_forceConcurrencyError;

        private NetworkEventHandler m_networkEventHandler;

        private readonly ConnectionPool m_connectionPool;

        private Boolean m_isDiscoveryServiceStarted = false;

        private readonly ICurrentUserProvider m_currentUserProvider;

        /// <summary>
        /// For functional tests
        /// </summary>
        public DiscoveryService( ServiceProfile profile, ICurrentUserProvider currentUserProvider )
            : this( profile )
        {
            m_currentUserProvider = currentUserProvider;

#if !IS_IN_LUC
            SettingsService = new SettingsService
            {
                CurrentUserProvider = m_currentUserProvider
            };

            LoggingService = new LoggingService
            {
                SettingsService = SettingsService
            };
#endif
        }

        //TODO delete this constructor
        public DiscoveryService( ServiceProfile profile )
        {
            if ( profile == null )
            {
                throw new ArgumentNullException( nameof( profile ) );
            }
            else
            {
                m_forceConcurrencyError = new ForcingConcurrencyError();

                GroupsSupported = profile.GroupsSupported;

                UseIpv4 = profile.UseIpv4;
                UseIpv6 = profile.UseIpv6;
                ProtocolVersion = profile.ProtocolVersion;
                MachineId = profile.MachineId;
                m_connectionPool = ConnectionPool.Instance();

                InitNetworkEventInvoker();

                //It should be initialized after previous method
                m_distributedHashTable = NetworkEventInvoker.DistributedHashTable( ProtocolVersion );
            }
        }

        

        /// <summary>
        /// If user of ServiceDiscovery forget to call method Stop
        /// </summary>
        ~DiscoveryService()
        {
            Stop();
        }

        /// <summary>
        /// Groups which current peer supports.
        /// Key is a name of group, which current peer supports.
        /// Value is a SSL certificate of group
        /// </summary>
        public ConcurrentDictionary<String, String> GroupsSupported { get; protected set; }

        public KademliaId ContactId
        {
            get
            {
                if ( m_isDiscoveryServiceStarted )
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
        /// IP address of peers which were discovered.
        /// Key is a network in a format "IP-address:port".
        /// Value is a list of group names, which peer supports
        /// </summary>
        public ConcurrentDictionary<EndPoint, String> KnownIps { get; protected set; } =
            new ConcurrentDictionary<EndPoint, String>();

        public List<Contact> OnlineContacts => NetworkEventInvoker.DistributedHashTable( ProtocolVersion ).OnlineContacts;

        /// <summary>
        ///   LightUpon.Cloud Discovery Service.
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
        public NetworkEventInvoker NetworkEventInvoker { get; private set; }

        private void InitNetworkEventInvoker()
        {
            NetworkEventInvoker = new NetworkEventInvoker( MachineId, UseIpv4, UseIpv6, ProtocolVersion );

            m_networkEventHandler = new NetworkEventHandler( this, NetworkEventInvoker, m_currentUserProvider );//puts in events of NetworkEventInvoker sendings response
            NetworkEventInvoker.QueryReceived += async ( invokerEvent, eventArgs ) => await SendTcpMessageAsync( invokerEvent, eventArgs );

            NetworkEventInvoker.AnswerReceived += AddEndpoint;
            NetworkEventInvoker.AnswerReceived += NetworkEventInvoker.Bootstrap;
        }

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
        public async Task SendTcpMessageAsync( Object sender, UdpMessageEventArgs eventArgs )
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
                       groupsIds: GroupsSupported?.Keys?.ToList()
                   );

                    ForcingConcurrencyError.TryForce();
                    Byte[] bytesToSend = tcpMessage.ToByteArray();

                    IPEndPoint remoteEndPoint = new IPEndPoint( ipEndPoint.Address, (Int32)udpMessage.TcpPort );
                    ConnectionPoolSocket client = null;
                    try
                    {
                        client = await m_connectionPool.SocketAsync( remoteEndPoint, Constants.ConnectTimeout,
                           IOBehavior.Asynchronous, Constants.TimeWaitReturnToPool ).ConfigureAwait( continueOnCapturedContext: false );

                        ForcingConcurrencyError.TryForce();
                        client = await client.DsSendWithAvoidErrorsInNetworkAsync( bytesToSend, Constants.SendTimeout,
                           Constants.ConnectTimeout, IOBehavior.Asynchronous ).ConfigureAwait( false );
                        ForcingConcurrencyError.TryForce();
                    }
                    catch ( TimeoutException ex)
                    {
                        LoggingService.LogError( ex.ToString() );
                    }
                    catch ( SocketException ex )
                    {
                        LoggingService.LogError( ex.ToString() );
                    }
                    catch ( AggregateException ex)
                    {
                        LoggingService.LogError( ex.ToString() );
                    }
                    catch ( ObjectDisposedException ex)
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

        public void AddEndpoint( Object sender, TcpMessageEventArgs e )
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

        /// <summary>
        ///    Start listening TCP, UDP messages and sending them
        /// </summary>
        public void Start()
        {
            if ( !m_isDiscoveryServiceStarted )
            {
                if ( NetworkEventInvoker == null )
                {
                    InitNetworkEventInvoker();
                }
                NetworkEventInvoker.Start();

                m_isDiscoveryServiceStarted = true;
            }
        }

        /// <summary>
        /// Sends multicast message
        /// </summary>
        public void QueryAllServices()
        {
            if ( m_isDiscoveryServiceStarted )
            {
                NetworkEventInvoker.SendQuery();
            }
            else
            {
                throw new InvalidOperationException( "First you need to start discovery service" );
            }
        }

        /// <summary>
        ///    Stop listening and sending TCP and UDP messages
        /// </summary>
        public void Stop()
        {
            if ( m_isDiscoveryServiceStarted )
            {
                NetworkEventInvoker.Stop();
                NetworkEventInvoker = null;

                ServiceInstanceShutdown?.Invoke( sender: this, new TcpMessageEventArgs() );

                m_connectionPool.ClearPoolAsync( IOBehavior.Synchronous, respectMinPoolSize: false, CancellationToken.None ).GetAwaiter().GetResult();

                m_isDiscoveryServiceStarted = false;
            }

            m_forceConcurrencyError?.Dispose();
        }
    }
}
