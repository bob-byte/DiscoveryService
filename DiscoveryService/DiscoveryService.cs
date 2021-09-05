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

[assembly: InternalsVisibleTo("DiscoveryService.Test")]
namespace LUC.DiscoveryService
{
    /// <summary>
    ///   LightUpon.Cloud Service Discovery maintens the list of IP addresses in LAN. It realizes pattern Singleton
    /// </summary>
    public class DiscoveryService : AbstractService
    {
        private readonly Dht distributedHashTable;

        private readonly NetworkEventHandler networkEventHandler;

        private readonly ConnectionPool connectionPool;

        private Boolean isDiscoveryServiceStarted = false;

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

        /// <summary>
        /// For functional tests
        /// </summary>
        public DiscoveryService(ServiceProfile profile, ICurrentUserProvider currentUserProvider)
            : this(profile)
        {
            networkEventHandler = new NetworkEventHandler(this, NetworkEventInvoker, currentUserProvider);//puts in events of NetworkEventInvoker sendings response
        }

        public DiscoveryService(ServiceProfile profile)
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
                distributedHashTable = NetworkEventInvoker.DistributedHashTable(ProtocolVersion);
            }
        }

        ///// <summary>
        ///// If user of ServiceDiscovery forget to call method Stop
        ///// </summary>
        //~DiscoveryService()
        //{
        //    Stop();
        //}

        /// <summary>
        /// Groups which current peer supports.
        /// Key is a name of group, which current peer supports.
        /// Value is a SSL certificate of group
        /// </summary>
        public ConcurrentDictionary<String, String> GroupsSupported { get; protected set; }

        public ID ContactId
        {
            get
            {
                if(isDiscoveryServiceStarted)
                {
                    return NetworkEventInvoker.OurContact.ID;
                }
                else
                {
                    throw new InvalidOperationException($"{nameof(DiscoveryService)} is stopped");
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

        public List<Contact> OnlineContacts => NetworkEventInvoker.DistributedHashTable(ProtocolVersion).OnlineContacts;

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

        private void InitService()
        {
            NetworkEventInvoker = new NetworkEventInvoker(MachineId, UseIpv4, UseIpv6, ProtocolVersion);

            NetworkEventInvoker.QueryReceived += async (invokerEvent, eventArgs) => await SendTcpMessageAsync(invokerEvent, eventArgs);

            NetworkEventInvoker.AnswerReceived += AddEndpoint;
            NetworkEventInvoker.AnswerReceived += NetworkEventInvoker.Bootstrap;

            NetworkEventInvoker.PingReceived += (invokerEvent, eventArgs) =>
            {
                HandleKademliaRequest<PingRequest>(eventArgs.AcceptedSocket, eventArgs, handleRequest: (acceptedSocket, sender, request) =>
                {
                    PingResponse.SendSameRandomId(acceptedSocket, Constants.SendTimeout, request);

                    if (sender != null)
                    {
                        distributedHashTable.Node.Ping(sender);
                    }
                });
            };

            NetworkEventInvoker.StoreReceived += (invokerEvent, eventArgs) =>
            {
                HandleKademliaRequest<StoreRequest>(eventArgs.AcceptedSocket, eventArgs, handleRequest: (acceptedSocket, sender, request) =>
                {
                    StoreResponse.SendSameRandomId(acceptedSocket, Constants.SendTimeout, request);

                    if (sender != null)
                    {
                        distributedHashTable.Node.Store(sender, new ID(request.KeyToStore), request.Value);
                    }
                });
            };

            NetworkEventInvoker.FindNodeReceived += (invokerEvent, eventArgs) =>
            {
                HandleKademliaRequest<FindNodeRequest>(eventArgs.AcceptedSocket, eventArgs, (client, sender, request) =>
                {
                    List<Contact> closeContacts;
                    if (sender != null)
                    {
                        distributedHashTable.Node.FindNode(sender, new ID(request.KeyToFindCloseContacts), out closeContacts);
                    }
                    else
                    {
                        closeContacts = distributedHashTable.Node.BucketList.GetCloseContacts(new ID(request.KeyToFindCloseContacts), new ID(request.Sender));
                    }
                    
                    FindNodeResponse.SendOurCloseContacts(client, closeContacts, Constants.SendTimeout, request);
                });
            };

            NetworkEventInvoker.FindValueReceived += (invokerEvent, eventArgs) =>
            {
                HandleKademliaRequest<FindValueRequest>(eventArgs.AcceptedSocket, eventArgs, (client, sender, request) =>
                {
                    List<Contact> closeContacts;
                    String nodeValue = null;

                    if (sender != null)
                    {
                        distributedHashTable.Node.FindValue(sender, new ID(request.KeyToFindCloseContacts),
                        out closeContacts, out nodeValue);
                    }
                    else
                    {
                        closeContacts = distributedHashTable.Node.BucketList.GetCloseContacts(new ID(request.KeyToFindCloseContacts), new ID(request.Sender));
                    }

                    FindValueResponse.SendOurCloseContactsAndMachineValue(request, client, closeContacts, Constants.SendTimeout, nodeValue);
                });
            };
        }

        //TODO: check SSL certificate with SNI
        /// <summary>
        ///  Sends TCP message of "acknowledge" custom type to <seealso cref="TcpMessageEventArgs.SendingEndPoint"/> using <seealso cref="UdpMessage.TcpPort"/>
        ///  It is added to <seealso cref="NetworkEventInvoker.QueryReceived"/>. 
        /// </summary>
        /// <param name="sender">
        ///  Object which invoked event <seealso cref="NetworkEventInvoker.QueryReceived"/>
        /// </param>
        /// <param name="e">
        ///  Information about UDP sender, that we have received.
        /// </param>
        public async Task SendTcpMessageAsync(Object sender, UdpMessageEventArgs eventArgs)
        {
            //lock (this)
            //{
            var udpMessage = eventArgs.Message<UdpMessage>(whetherReadMessage: false);

            if ((udpMessage != null) && (eventArgs?.RemoteEndPoint is IPEndPoint ipEndPoint))
            {
                var sendingContact = NetworkEventInvoker.OurContact/*.Single(c => c.ID.Value == e.LocalContactId)*/;

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

                    client = await client.SendWithAvoidErrorsInNetworkAsync(bytesToSend, Constants.SendTimeout,
                        Constants.ConnectTimeout, IOBehavior.Asynchronous).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    LoggingService.LogError($"Receive handler failed: {ex.Message}");
                    // eat the exception
                }

                finally
                {
                    if (client != null)
                    {
                        await client.ReturnToPoolAsync(IOBehavior.Asynchronous).ConfigureAwait(continueOnCapturedContext: false);
                    }
                }
            }
            else
            {
                throw new ArgumentException($"Bad format of {nameof(eventArgs)}");
            }
            //}
        }

        public void AddEndpoint(Object sender, TcpMessageEventArgs e)
        {
            var tcpMessage = e.Message<AcknowledgeTcpMessage>(whetherReadMessage: false);
            if ((tcpMessage != null) && (e.SendingEndPoint is IPEndPoint ipEndPoint))
            {
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
            Contact sender = null;
            T request = null;
            try
            {
                request = eventArgs.Message<T>(whetherReadMessage: false);
                sender = OnlineContacts.Single(c => c.ID == new ID(request.Sender));
            }
            catch(InvalidOperationException ex)
            {
                LoggingService.LogInfo($"Cannot find sender of {typeof(T).Name}: {ex.Message}");
            }
            catch(EndOfStreamException ex)
            {
                LoggingService.LogInfo($"Failed to answer at a {typeof(T).Name}: {ex}");
                return;
            }

            try
            {
                handleRequest(acceptedSocket, sender, request);
            }
            catch (SocketException ex)
            {
                LoggingService.LogInfo($"Failed to answer at a {typeof(T).Name}: {ex}");
            }
            catch (TimeoutException ex)
            {
                LoggingService.LogInfo($"Failed to answer at a {typeof(T).Name}: {ex}");
            }
        }

        private void SendFileDescription(Object sender, TcpMessageEventArgs eventArgs)
        {
            //check request
            var request = eventArgs.Message<CheckFileExistsRequest>(whetherReadMessage: false);
            if (request != null)
            {

            }

            //check file exists
            //send response
        }

        private void SendSomeBytesOfFile(Object sender, TcpMessageEventArgs eventArgs)
        {
            //check request
            var request = eventArgs.Message<DownloadFileRequest>(whetherReadMessage: false);
            if (request != null)
            {
                
            }

            //check file exists
            //send response
        }

        /// <summary>
        ///    Start listening TCP, UDP messages and sending them
        /// </summary>
        public void Start()
        {
            if (!isDiscoveryServiceStarted)
            {
                if (NetworkEventInvoker == null)
                {
                    InitService();
                }
                NetworkEventInvoker.Start();

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
                NetworkEventInvoker.SendQuery();
            }
            else
            {
                throw new InvalidOperationException("First you need to start discovery service");
            }
        }

        /// <summary>
        ///    Stop listening and sending TCP and UDP messages
        /// </summary>
        public void Stop()
        {
            if (isDiscoveryServiceStarted)
            {
                NetworkEventInvoker.Stop();
                NetworkEventInvoker = null;
                ServiceInstanceShutdown?.Invoke(this, new TcpMessageEventArgs());
                connectionPool.ClearPoolAsync(IOBehavior.Synchronous, respectMinPoolSize: false, CancellationToken.None).GetAwaiter();

                isDiscoveryServiceStarted = false;
            }
        }
    }
}
