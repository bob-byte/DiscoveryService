﻿using System;
using System.Net;
using System.Net.Sockets;
using LUC.DiscoveryService.Messages;
using System.Linq;
using System.Collections.Concurrent;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.DiscoveryService.Kademlia;
using System.Collections.Generic;
using LUC.DiscoveryService.Kademlia.Protocols.Tcp;
using LUC.DiscoveryService.Kademlia.Interfaces;
using System.Threading;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   LightUpon.Cloud Service Discovery maintens the list of IP addresses in LAN. It realizes pattern Singleton
    /// </summary>
    public class DiscoveryService : CollectedInfoInLan
    {
        private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(1);

        private static DiscoveryService instance;
        private readonly ConnectionPool connectionPool;

        private IProtocol protocol;

        /// <summary>
        /// To avoid sending recent duplicate messages
        /// </summary>
        private readonly RecentMessages sentMessages = new RecentMessages();

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

        static DiscoveryService()
        {
            AppDomain.CurrentDomain.DomainUnload += StopDiscovery;
            AppDomain.CurrentDomain.ProcessExit += StopDiscovery;
        }

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
                protocol = new TcpProtocol(log);
                connectionPool = ConnectionPool.Instance(log);

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

        public static List<Contact> KnownContacts { get; set; } = new List<Contact>();

        /// <summary>
        /// IP address of peers which were discovered.
        /// Key is a network in a format "IP-address:port".
        /// Value is a list of group names, which peer supports
        /// </summary>
        public ConcurrentDictionary<String, String> KnownIps { get; protected set; } = 
            new ConcurrentDictionary<String, String>();

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
        public Service Service { get; private set; }

        private void InitService()
        {
            Service = new Service(MachineId, protocol, UseIpv4, UseIpv6, ProtocolVersion);

            Service.QueryReceived += SendTcpMessage;
            Service.AnswerReceived += AddNewContact;
            //Service.AnswerReceived += Service.Bootstrap;
            Service.PingReceived += (s, e) =>
            {
                SendKademliaResponse<PingRequest>(e.AcceptedSocket, e, (receiver, request) =>
                {
                    PingResponse.SendSameRandomId(receiver, Constants.SendTimeout, request);
                });
            };

            //Service.StoreReceived += (s, e) =>
            //{
            //    SendKademliaResponse<StoreRequest>(e, (client, request) =>
            //    {
            //        StoreResponse.SendSameRandomId(client, Constants.SendTimeout, request);
            //    });
            //};

            //Service.FindNodeReceived += (s, e) =>
            //{
            //    SendKademliaResponse<FindNodeRequest>(e, (client, request) =>
            //    {
            //         var closeContacts = Service.DistributedHashTable.Node.BucketList.GetCloseContacts(new ID(e.LocalContactId), new ID(e.LocalContactId));
            //         FindNodeResponse.SendOurCloseContactsAndPort(client, closeContacts, RunningTcpPort, SendTimeout, request);
            //    });
            //};

            //Service.FindValueReceived += (s, e) =>
            //{
            //    SendKademliaResponse<FindValueRequest>(s as DiscoveryServiceSocket, e, (client, request) =>
            //    {
            //        var closeContacts = Service.DistributedHashTable.Node.BucketList.GetCloseContacts(new ID(e.LocalContactId), new ID(e.LocalContactId));
            //        FindValueResponse.SendOurCloseContactsAndMachineValue(request, client, closeContacts, MachineId);
            //    });
            //};
        }

        private void SendKademliaResponse<T>(Socket acceptedSocket, TcpMessageEventArgs eventArgs, Action<Socket, T> funcSend)
            where T: Request, new()
        {
            try
            {
                var request = eventArgs.Message<T>(whetherReadMessage: false);
                funcSend(acceptedSocket, request);
            }
            catch(Exception ex)
            {
                log.LogInfo($"Failed to answer at {typeof(T)}: {ex.Message}");
            }
        }

        public void AddNewContact(Object sender, TcpMessageEventArgs e)
        {
            var tcpMessage = e.Message<AcknowledgeTcpMessage>(whetherReadMessage: false);
            if ((tcpMessage != null) && (e.RemoteContact is IPEndPoint iPEndPoint))
            {
                KnownContacts.Add(new Contact(new TcpProtocol(log), new ID(tcpMessage.IdOfSendingContact), iPEndPoint.Address, tcpMessage.TcpPort));
            }
            else
            {
                throw new ArgumentException($"Bad format of {nameof(e)}");
            }
        }

        //TODO: check SSL certificate with SNI
        /// <summary>
        ///  Sends TCP message of "acknowledge" custom type to <seealso cref="TcpMessageEventArgs.RemoteContact"/> using <seealso cref="UdpMessage.TcpPort"/>
        ///  It is added to <seealso cref="Service.QueryReceived"/>. 
        /// </summary>
        /// <param name="sender">
        ///  Object which invoked event <seealso cref="Service.QueryReceived"/>
        /// </param>
        /// <param name="e">
        ///  Information about UDP sender, that we have received.
        /// </param>
        public void SendTcpMessage(Object sender, UdpMessageEventArgs e)
        {
            var udpMessage = e.Message<UdpMessage>(whetherReadMessage: false);

            if ((udpMessage != null) && (e?.RemoteEndPoint is IPEndPoint ipEndPoint))
            {

                var sendingContact = Service.OurContacts.Single(c => c.ID.Value == e.LocalContactId);

                Random random = new Random();
                var tcpMessage = new AcknowledgeTcpMessage(
                    messageId: (UInt32)random.Next(maxValue: Int32.MaxValue),
                    MachineId,
                    sendingContact.ID.Value,
                    RunningTcpPort,
                    ProtocolVersion,
                    groupsIds: GroupsSupported?.Keys?.ToList());
                var bytesToSend = tcpMessage.ToByteArray();

                if ((Service.IgnoreDuplicateMessages) && (!sentMessages.TryAdd(bytesToSend)))
                {
                    return;
                }

                var remoteEndPoint = new IPEndPoint(ipEndPoint.Address, (Int32)udpMessage.TcpPort);
                var client = connectionPool.SocketAsync(remoteEndPoint, Constants.ConnectTimeout, IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
                client.Send(bytesToSend, Constants.SendTimeout, out _);

                client.ReturnToPoolAsync(IOBehavior.Synchronous).ConfigureAwait(continueOnCapturedContext: false);
            }
            else
            {
                throw new ArgumentException($"Bad format of {nameof(e)}");
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
        /// Sends query to all services in a local network
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

        //internal static List<Contact> KnownContacts(UInt32 protocolVersion)
        //{

        //}

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
