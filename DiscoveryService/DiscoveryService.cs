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

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   LightUpon.Cloud Service Discovery maintens the list of IP addresses in LAN. It realizes pattern Singleton
    /// </summary>
    public class DiscoveryService : AbstractService
    {
        private static DiscoveryService instance;

        private readonly ConnectionPool connectionPool;

        private Boolean isDiscoveryServiceStarted = false;

        //
        // Kademilia contacts
        //
        private static readonly ConcurrentDictionary<UInt32, ConcurrentDictionary<BigInteger, Contact>> knownContacts = new ConcurrentDictionary<UInt32, ConcurrentDictionary<BigInteger, Contact>>();

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

        private DiscoveryService(ServiceProfile profile)
        {
            if(profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }
            else
            {
                GroupsSupported = profile.GroupsSupported;

                UseIpv4 = profile.UseIpv4;
                UseIpv6 = profile.UseIpv6;
                ProtocolVersion = profile.ProtocolVersion;
                MachineId = profile.MachineId;
                connectionPool = ConnectionPool.Instance();
                
                InitService();
            }
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="DiscoveryService"/> class.
        /// </summary>
        /// <param name="groupsSupported">
        /// Groups which current machine supports.
        /// Key is a name of group, which current peer supports.
        /// Value is a SSL certificate of group
        /// </param>
        /// <param name="knownIps">
        /// IP address of groups which were discovered.
        /// Key is a name of group, which current peer supports.
        /// Value is a network in a format "IPAddress:port"
        /// </param>
        public static DiscoveryService Instance(ServiceProfile profile)
        {
            Lock.InitWithLock(Lock.LockService, new DiscoveryService(profile), ref instance);
            return instance;
        }

        ///// <summary>
        ///// If user of ServiceDiscovery forget to call method Stop
        ///// </summary>
        //~DiscoveryService()
        //{
        //    Stop();
        //}

        public ConcurrentDictionary<String, String> GroupsSupported { get; protected set; }

        /// <summary>
        /// IP address of peers which were discovered.
        /// Key is a network in a format "IP-address:port".
        /// Value is a list of group names, which peer supports
        /// </summary>
        public ConcurrentDictionary<EndPoint, String> KnownIps { get; protected set; } = 
            new ConcurrentDictionary<EndPoint, String>();

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
        public NetworkEventHandler Service { get; private set; }

        private void InitService()
        {
            Service = new NetworkEventHandler(MachineId, UseIpv4, UseIpv6, ProtocolVersion);

            Service.QueryReceived += async (invokerEvent, eventArgs) => await SendTcpMessageAsync(invokerEvent, eventArgs);
            Service.AnswerReceived += AddEndpoint;
            Service.AnswerReceived += Service.TryKademliaOperation;
            Service.PingReceived += (invokerEvent, eventArgs) =>
            {
                HandleKademliaRequest<PingRequest>(eventArgs.AcceptedSocket, eventArgs, handleRequest: (acceptedSocket, sender, request) =>
                {
                    Service.DistributedHashTable.Node.Ping(sender);

                    PingResponse.SendSameRandomId(acceptedSocket, Constants.SendTimeout, request);
                });
            };

            Service.StoreReceived += (invokerEvent, eventArgs) =>
            {
                HandleKademliaRequest<StoreRequest>(eventArgs.AcceptedSocket, eventArgs, handleRequest: (acceptedSocket, sender, request) =>
                {
                    Service.DistributedHashTable.Store(new ID(request.KeyToStore), request.Value);

                    StoreResponse.SendSameRandomId(acceptedSocket, Constants.SendTimeout, request);
                });
            };

            Service.FindNodeReceived += (invokerEvent, eventArgs) =>
            {
                HandleKademliaRequest<FindNodeRequest>(eventArgs.AcceptedSocket, eventArgs, (client, sender, request) =>
                {
                    Service.DistributedHashTable.Node.FindNode(sender, new ID(request.KeyToFindCloseContacts), out var closeContacts);
                    
                    FindNodeResponse.SendOurCloseContacts(client, closeContacts, Constants.SendTimeout, request);
                });
            };

            Service.FindValueReceived += (invokerEvent, eventArgs) =>
            {
                HandleKademliaRequest<FindValueRequest>(eventArgs.AcceptedSocket, eventArgs, (client, sender, request) =>
                {
                    //Service.DistributedHashTable.Node.FindValue(sender, new ID(request.KeyToFindCloseContacts), out var closeContacts, out var nodeValue);
                    Service.DistributedHashTable.FindValue(new ID(request.KeyToFindCloseContacts), 
                        out var isFound, out var closeContacts, out var nodeValue);

                    if(isFound)
                    {
                        FindValueResponse.SendOurCloseContactsAndMachineValue(request, client, closeContacts, Constants.SendTimeout, nodeValue);
                    }
                    else
                    {
                        //TODO send response with another MessageOperation or just do nothing
                    }
                });
            };
        }

        //TODO: check SSL certificate with SNI
        /// <summary>
        ///  Sends TCP message of "acknowledge" custom type to <seealso cref="TcpMessageEventArgs.SendingEndPoint"/> using <seealso cref="UdpMessage.TcpPort"/>
        ///  It is added to <seealso cref="NetworkEventHandler.QueryReceived"/>. 
        /// </summary>
        /// <param name="sender">
        ///  Object which invoked event <seealso cref="NetworkEventHandler.QueryReceived"/>
        /// </param>
        /// <param name="e">
        ///  Information about UDP sender, that we have received.
        /// </param>
        public async Task SendTcpMessageAsync(Object sender, UdpMessageEventArgs e)
        {
            //lock(this)
            //{
                var udpMessage = e.Message<UdpMessage>(whetherReadMessage: false);

            if ((udpMessage != null) && (e?.RemoteEndPoint is IPEndPoint ipEndPoint))
            {
                var sendingContact = Service.OurContact/*.Single(c => c.ID.Value == e.LocalContactId)*/;

                Random random = new Random();
                var tcpMessage = new AcknowledgeTcpMessage(
                    messageId: (UInt32)random.Next(maxValue: Int32.MaxValue),
                    MachineId,
                    sendingContact.ID.Value,
                    RunningTcpPort,
                    ProtocolVersion,
                    groupsIds: GroupsSupported?.Keys?.ToList());
                var bytesToSend = tcpMessage.ToByteArray();

                var remoteEndPoint = new IPEndPoint(ipEndPoint.Address, (Int32)udpMessage.TcpPort);
                ConnectionPoolSocket client = null;
                try
                {
                    client = await connectionPool.SocketAsync(remoteEndPoint, Constants.ConnectTimeout,
                    IOBehavior.Asynchronous, Constants.TimeWaitReturnToPool).ConfigureAwait(continueOnCapturedContext: false);

                    ConnectionPoolSocket.SendWithAvoidErrorsInNetwork(bytesToSend, Constants.SendTimeout,
                        Constants.ConnectTimeout, ref client);
                }
                finally
                {
                    if (client != null)
                    {
                        await client.ReturnToPoolAsync(IOBehavior.Synchronous).ConfigureAwait(continueOnCapturedContext: false);
                    }
                }
            }
            else
            {
                throw new ArgumentException($"Bad format of {nameof(e)}");
            }
            //}
        }

        public void AddEndpoint(Object sender, TcpMessageEventArgs e)
        {
            var tcpMessage = e.Message<AcknowledgeTcpMessage>(whetherReadMessage: false);
            if ((tcpMessage != null) && (e.SendingEndPoint is IPEndPoint ipEndPoint))
            {
                var knownContacts = KnownContacts(ProtocolVersion);
                knownContacts.TryAdd(tcpMessage.IdOfSendingContact, new Contact(new ID(tcpMessage.IdOfSendingContact), tcpMessage.TcpPort, ipEndPoint.Address));

                foreach (var groupId in tcpMessage.GroupIds)
                {
                    if (!KnownIps.TryAdd(ipEndPoint, groupId))
                    {
                        KnownIps.TryRemove(ipEndPoint, out _);
                        KnownIps.TryAdd(ipEndPoint, groupId);
                    }
                }
            }
            else
            {
                throw new ArgumentException($"Bad format of {nameof(e)}");
            }
        }

        private void HandleKademliaRequest<T>(Socket acceptedSocket, TcpMessageEventArgs eventArgs, Action<Socket, Contact, T> handleRequest)
            where T: Request, new()
        {
            try
            {
                var request = eventArgs.Message<T>(whetherReadMessage: false);
                var sender = KnownContacts(ProtocolVersion)[request.Sender];

                handleRequest(acceptedSocket, sender, request);
            }
            catch(Exception ex)
            {
                log.LogInfo($"Failed to answer at {typeof(T)}: {ex.Message}");
            }
        }

        /// <summary>
        ///    Start listening TCP, UDP messages and sending them
        /// </summary>
        public void Start()
        {
            if (!isDiscoveryServiceStarted)
            {
                if (Service == null)
                {
                    InitService();
                }
                Service.Start();

                isDiscoveryServiceStarted = true;
            }
        }

        /// <summary>
        /// Sends multicast message
        /// </summary>
        public void QueryAllServices()
        {
            if (isDiscoveryServiceStarted)
            {
                Service.SendQuery();
            }
            else
            {
                throw new InvalidOperationException("First you need to start discovery service");
            }
        }

        internal static ConcurrentDictionary<BigInteger, Contact> KnownContacts(UInt32 protocolVersion)
        {
            knownContacts.TryAdd(protocolVersion, new ConcurrentDictionary<BigInteger, Contact>());
            return knownContacts[protocolVersion];
        }

        private static void StopDiscovery(Object sender, EventArgs e)
        {
            instance?.Stop();
        }

        /// <summary>
        ///    Stop listening and sending TCP and UDP messages
        /// </summary>
        public void Stop()
        {
            if (isDiscoveryServiceStarted)
            {
                Service.Stop();
                Service = null;
                ServiceInstanceShutdown?.Invoke(this, new TcpMessageEventArgs());
                connectionPool.ClearPoolAsync(IOBehavior.Synchronous, respectMinPoolSize: false, CancellationToken.None).GetAwaiter();

                isDiscoveryServiceStarted = false;
            }
        }
    }
}
