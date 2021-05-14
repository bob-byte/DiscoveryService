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

        private ServiceDiscovery(Boolean useIpv4, Boolean useIpv6, ConcurrentDictionary<String, String> groupsSupported = null, ConcurrentDictionary<String, String> knownIps = null)
            : this(groupsSupported, knownIps)
        {
            Service.UseIpv4 = useIpv4;
            Service.UseIpv6 = useIpv6;
        }

        private ServiceDiscovery(ConcurrentDictionary<String, String> groupsSupported = null, ConcurrentDictionary<String, String> knownIps = null)
        {
            Profile = new ServiceProfile(MinValueTcpPort, MaxValueTcpPort, UdpPort,
                Messages.Message.ProtocolVersion, groupsSupported, knownIps);

            Service = new Service(UdpPort, MinValueTcpPort, Profile.MachineId);
            Service.TcpPortChanged += OnTcpPortChanged;
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
        /// Info of current peer
        /// </summary>
        public ServiceProfile Profile { get; }

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
        /// IP address of groups which were discovered.
        /// Key is a name of group, which current peer supports.
        /// Value is a network in a format "IP-address:port"
        /// </summary>
        public ConcurrentDictionary<String, String> KnownIps => Profile.KnownIps;

        /// <summary>
        /// Groups which current peer supports.
        /// Key is a name of group, which current peer supports.
        /// Value is a SSL certificate of group
        /// </summary>
        public ConcurrentDictionary<String, String> GroupsSupported => Profile.GroupsSupported;

        /// <summary>
        /// Change field <see cref="ServiceProfile.RunningTcpPort"/> to current run TCP port
        /// </summary>
        /// <param name="sender">
        /// Object which invoked this event
        /// </param>
        /// <param name="tcpPort">
        /// New TCP port
        /// </param>
        private void OnTcpPortChanged(Object sender, UInt32 tcpPort)
        {
            Profile.RunningTcpPort = tcpPort;
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
                var tcpMess = new TcpMessage(messageId: (UInt32)random.Next(maxValue: Int32.MaxValue), 
                    Profile.RunningTcpPort, Profile.GroupsSupported.Keys.ToList());
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
        /// <param name="knownIps">
        /// IP address of groups which were discovered.
        /// Key is a name of group, which current peer supports.
        /// Value is a network in a format "IPAddress:port"
        /// </param>
        public static ServiceDiscovery Instance(ConcurrentDictionary<String, String> groupsSupported = null,
            ConcurrentDictionary<String, String> knownIps = null)
        {
            Lock.InitWithLock(Lock.LockService, new ServiceDiscovery(groupsSupported, knownIps), ref instance);
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
        /// <param name="knownIps">
        /// IP address of groups which were discovered.
        /// Key is a name of group, which current peer supports.
        /// Value is a network in a format "IPAddress:port"
        /// </param>
        public static ServiceDiscovery Instance(Boolean useIpv4, Boolean useIpv6, ConcurrentDictionary<String, String> groupsSupported = null, ConcurrentDictionary<String, String> knownIps = null)
        {
            Lock.InitWithLock(Lock.LockService, new ServiceDiscovery(useIpv4, useIpv6, groupsSupported, knownIps), ref instance);
            return instance;
        }

        /// <summary>
        ///    Start listening TCP, UDP messages and sending them
        /// </summary>
        public void Start(out String machineId)
        {
            if(!isDiscoveryServiceStarted)
            {
                Service.Start();
                isDiscoveryServiceStarted = true;
            }

            machineId = Profile.MachineId;
        }

        /// <summary>
        /// Sends query to all services in local network
        /// </summary>
        public void QueryAllServices()
        {
            Service.SendQuery();
        }
    }
}