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
        [Import(typeof(ILoggingService))]
        private static readonly ILoggingService log = new LoggingService();

        private UInt32 runningTcpPort;

        private static ServiceDiscovery instance;

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
        ///   <see cref="ServiceDiscovery"/> passively monitors the network for any answers.
        ///   When an answer containing type 1 received this event is raised.
        /// </remarks>
        public event EventHandler<MessageEventArgs> ServiceInstanceShutdown;

        private ServiceDiscovery(ServiceProfile profile)
        {
            if(profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }
            else
            {
                UseIpv4 = profile.UseIpv4;
                UseIpv6 = profile.UseIpv6;

                GroupsSupported = profile.GroupsSupported;
                KnownIps = profile.KnownIps;

                MachineId = profile.MachineId;

                MinValueTcpPort = profile.MinValueTcpPort;
                KadPort = profile.KadPort;

                InitService();
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
        /// <param name="knownIps">
        /// IP address of groups which were discovered.
        /// Key is a name of group, which current peer supports.
        /// Value is a network in a format "IPAddress:port"
        /// </param>
        public static ServiceDiscovery Instance(ServiceProfile profile)
        {
            Lock.InitWithLock(Lock.LockService, new ServiceDiscovery(profile), ref instance);
            return instance;
        }

        /// <summary>
        /// If user of ServiceDiscovery forget to call method Stop
        /// </summary>
        ~ServiceDiscovery()
        {
            Stop();
        }

        /// <summary>
        ///   A unique identifier for the service instance.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        public String MachineId { get; }

        public UInt32 MinValueTcpPort { get; }

        public UInt32 MaxValueTcpPort { get; }

        /// <summary>
        /// TCP port which current peer is using in TCP connections
        /// </summary>
        public UInt32 RunningTcpPort
        {
            get => runningTcpPort;
            internal set
            {
                runningTcpPort = (value < MinValueTcpPort) || (MaxValueTcpPort < value) ?
                    MinValueTcpPort : value;
            }
        }

        /// <summary>
        /// UDP port which current peer is using in UDP connections
        /// </summary>
        internal UInt32 RunningUdpPort { get; }
        /// <summary>
        /// Kademilia port, that we send to other computers.
        /// </summary>
        public UInt32 KadPort { get; }

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
        /// Flag indicating whether Discovery Service should use IPv4 protocol.
        /// </summary>
        public Boolean UseIpv4 { get; private set; } = Socket.OSSupportsIPv4;

        /// <summary>
        /// Flag indicating whether Discovery Service should use IPv6 protocol.
        /// </summary>
        public Boolean UseIpv6 { get; private set; } = Socket.OSSupportsIPv6;

        /// <summary>
        /// IP address of groups which were discovered.
        /// Key is a name of group, which current peer supports.
        /// Value is a network in a format "IP-address:port"
        /// </summary>
        public ConcurrentDictionary<String, String> KnownIps { get; private set; }

        /// <summary>
        /// Groups which current peer supports.
        /// Key is a name of group, which current peer supports.
        /// Value is a SSL certificate of group
        /// </summary>
        public ConcurrentDictionary<String, String> GroupsSupported { get; private set; }

        private void InitService()
        {
            Service = new Service(RunningUdpPort, MinValueTcpPort, 
                MachineId, UseIpv4, UseIpv6);
            Service.QueryReceived += SendTcpMess;
        }

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
        public void SendTcpMess(Object sender, MessageEventArgs e)
        {
            if((e?.Message == null) || (e?.RemoteEndPoint == null) || (!(e?.Message is MulticastMessage)))
            {
                throw new ArgumentException($"Bad format of {nameof(e)}");
            }
            else
            {
                TcpClient client = null;
                NetworkStream stream = null;
                try
                {
                    Random random = new Random();

                    var message = e.Message as MulticastMessage;
                    client = new TcpClient(e.RemoteEndPoint.AddressFamily);
                    client.Connect(((IPEndPoint)e.RemoteEndPoint).Address, (Int32)message.TcpPort);
                    stream = client.GetStream();

                    if(GroupsSupported.Keys != null)
                    {
                        var tcpMess = new TcpMessage(messageId: (UInt32)random.Next(maxValue: Int32.MaxValue),
                        KadPort, groupsIds: GroupsSupported.Keys.ToList());
                        var bytes = tcpMess.ToByteArray();

                        //if (Service.IgnoreDuplicateMessages && sentMessages.TryAdd(bytes))
                        //{
                        //    return;
                        //}

                        stream.WriteAsync(bytes, offset: 0, bytes.Length);
                    }
                    else
                    {
                        throw new NullReferenceException($"{nameof(GroupsSupported.Keys)} is null");
                    }
                }
                catch (SocketException)
                {
                    throw;
                }
                finally
                {
                    stream?.Close();
                    client?.Close();
                }
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
    }
}