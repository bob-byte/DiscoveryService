using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using LUC.DiscoveryService.Messages;
using LUC.Interfaces;
using LUC.Services.Implementation;
using System.ComponentModel.Composition;
using System.Linq;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   LightUpon.Cloud Service Discovery maintens the list of IP addresses in LAN. It realizes pattern Singleton
    /// </summary>
    public class ServiceDiscovery
    {
        private const UInt32 UdpPort = 17500;
        private const UInt32 MinValueTcpPort = 17500;
        private const UInt32 MaxValueTcpPort = 17510;

        [Import(typeof(ILoggingService))]
        private static readonly ILoggingService log = new LoggingService();

        private static ServiceDiscovery instance;

        /// <summary>
        /// To avoid sending recent duplicate messages
        /// </summary>
        private readonly RecentMessages sentMessages = new RecentMessages();

        private Boolean isDiscoveryServiceStarted = false;

        private Boolean useIpv4 = true;
        private Boolean useIpv6 = true;
        public String MachineId { get; set; }

        /// <summary>
        ///   Raised when a servive instance is shutting down.
        /// </summary>
        /// <value>
        ///   Contains the service instance ip.
        /// </value>
        /// <remarks>
        ///   <see cref="ServiceDiscovery"/> passively monitors the network for any answers.
        ///   When an answer containing type 1 received this event is raised.
        /// </remarks>
        public event EventHandler<MessageEventArgs> ServiceInstanceShutdown;

        private ServiceDiscovery(ServiceProfile profile)
        {
            KadPort = profile.KadPort;
            useIpv4 = profile.useIpv4;
            useIpv6 = profile.useIpv6;
            GroupsSupported = profile.GroupsSupported;

            machineId = profile.MachineId;

            Service = new Service(UdpPort, profile.MachineId);
            Service.QueryReceived += SendTcpMessOnQuery;
        }

        /// <summary>
        /// If user of ServiceDiscovery forget to call method Stop
        /// </summary>
        ~ServiceDiscovery()
        {
            Stop();
        }

        /// <summary>
        /// Kademilia port, that we send to other computers.
        /// </summary>
        public UInt32 KadPort { get; }

        /// <summary>
        /// Flag indicating whether Discovery Service should use IPv4 protocol.
        /// </summary>
        public Boolean useIpv4 { get; }

        /// <summary>
        /// Flag indicating whether Discovery Service should use IPv6 protocol.
        /// </summary>
        public Boolean useIpv6 { get; }

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
        public Service Service { get; private set; }

        /// <summary>
        /// Groups which current peer supports.
        /// Key is a name of group, which current peer supports.
        /// Value is a SSL certificate of group
        /// </summary>
        public ConcurrentDictionary<String, String> GroupsSupported;

        //TODO: check SSL certificate with SNI
        /// <summary>
        /// It is added to <seealso cref="Service.QueryReceived"/>. It sends TCP message to <seealso cref="MessageEventArgs.RemoteEndPoint"/> using <seealso cref="MulticastMessage.TcpPort"/>
        /// </summary>
        /// <param name="sender">
        /// Object which invoked event <seealso cref="Service.QueryReceived"/>
        /// </param>
        /// <param name="e">
        /// Info about peer which sent UDP message to current machine
        /// </param>
        internal void SendTcpMessOnQuery(Object sender, MessageEventArgs e)
        {
            if (!(e.Message is MulticastMessage))
            {
                throw new ArgumentException("Bad format of the message");
            }

            TcpClient client = null;
            NetworkStream stream = null;
            try
            {
                Random random = new Random();

                var message = e.Message as MulticastMessage;
                client = new TcpClient(e.RemoteEndPoint.AddressFamily);
                client.Connect(((IPEndPoint)e.RemoteEndPoint).Address, (Int32)message.TcpPort);

                stream = client.GetStream();
                var tcpMess = new TcpMessage(
                    messageId: (UInt32)random.Next(maxValue: Int32.MaxValue),
                    KadPort,
                    GroupsSupported.Keys.ToList());
                var bytes = tcpMess.ToByteArray();
                if (Service.IgnoreDuplicateMessages && sentMessages.TryAdd(bytes))
                {
                    return;
                }
                stream.WriteAsync(bytes, offset: 0, bytes.Length);
            }
            catch
            {
                throw;
            }
            finally
            {
                stream?.Close();
                client?.Close();
            }
        }

        /// <summary>
        ///    Stop listening TCP, UDP messages and sending them
        /// </summary>
        public void Stop()
        {
            if (isDiscoveryServiceStarted)
            {
                Service.Stop();
                Service = null;
                ServiceInstanceShutdown?.Invoke(this, new MessageEventArgs());

                isDiscoveryServiceStarted = false;
            }
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceDiscovery"/> class.
        /// </summary>
        /// <param name="groupsSupported">
        /// Groups which current machine supports.
        /// Key is a name of group, which current peer supports.
        /// Value is a SSL certificate of group
        /// </param>
        public static ServiceDiscovery Instance(ConcurrentDictionary<String, String> groupsSupported = null)
        {
            Lock.InitWithLock(Lock.LockService, new ServiceDiscovery(groupsSupported), ref instance);
            return instance;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="ServiceDiscovery"/> class.
        /// </summary>
        /// <param name="useIpv4">
        /// Send and receive on IPv4.
        /// <value>
        /// Defaults to <b>true</b> if the OS supports it.
        /// </value>
        /// </param>
        /// <param name="useIpv6">
        /// Send and receive on IPv6.
        /// <value>
        /// Defaults to <b>true</b> if the OS supports it.
        /// </value>
        /// </param>
        /// <param name="groupsSupported">
        /// Groups which current machine supports.
        /// Key is a name of group, which current peer supports.
        /// Value is a SSL certificate of group
        /// </param>
        public static ServiceDiscovery Instance(Boolean useIpv4, Boolean useIpv6, ConcurrentDictionary<String, String> groupsSupported = null)
        {
            Lock.InitWithLock(Lock.LockService, new ServiceDiscovery(useIpv4, useIpv6, groupsSupported), ref instance);
            return instance;
        }

        /// <summary>
        ///    Start listening TCP, UDP messages and sending them
        /// </summary>
        public void Start()
        {
            if(!isDiscoveryServiceStarted)
            {
                Service.Start();
                isDiscoveryServiceStarted = true;
            }
        }

        /// <summary>
        /// Sends query to all services in a local network
        /// </summary>
        public void QueryAllServices()
        {
            Service.SendQuery();
        }
    }
}