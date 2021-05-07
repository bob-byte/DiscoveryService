using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Messages;
using Makaretu.Dns;
using LUC.Interfaces;
using LUC.Services.Implementation;
using System.ComponentModel.Composition;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   LightUpon.Cloud Service Discovery maintens the list of IP addresses in LAN.
    /// </summary>
    public class ServiceDiscovery
    {
        private const Int32 UdpPort = 17500;
        private const Int32 MinValueTcpPort = 17500;
        private const Int32 MaxValueTcpPort = 17510;

        [Import(typeof(ILoggingService))]
        private static readonly ILoggingService log = new LoggingService();

        private static ServiceDiscovery instance;
        public ServiceProfile Profile { get; }
        private readonly RecentMessages sentMessages = new RecentMessages();

        private Boolean isDiscoveryServiceStarted = false;

        /// <summary>
        ///   Raised when a servive instance is shutting down.
        /// </summary>
        /// <value>
        ///   Contains the service instance ip.
        /// </value>
        /// <remarks>
        ///   <b>ServiceDiscovery</b> passively monitors the network for any answers.
        ///   When an answer containing type 1 received this event is raised.
        /// </remarks>
        public event EventHandler<MessageEventArgs> ServiceInstanceShutdown;

        private ServiceDiscovery(Boolean useIpv4, Boolean useIpv6, ConcurrentDictionary<String, List<KeyValuePair<String, String>>> groupsSupported = null)
            : this(groupsSupported)
        {
            Service.UseIpv4 = useIpv4;
            Service.UseIpv6 = useIpv6;
        }

        private ServiceDiscovery(ConcurrentDictionary<String, List<KeyValuePair<String, String>>> groupsSupported = null)
        {
            Profile = new ServiceProfile(MinValueTcpPort, MaxValueTcpPort, UdpPort,
                Messages.Message.ProtocolVersion, groupsSupported);
            Service = new Service(Profile);
            Service.QueryReceived += SendTcpMessOnQuery;
        }

        /// <summary>
        /// If user of ServiceDiscovery forget to call method Stop
        /// </summary>
        ~ServiceDiscovery()
        {
            Stop();
        }

        public Service Service { get; private set; }

        /// <summary>
        /// Key is a network in a format "IP-address:port"
        /// Value is the list of name of groups, which current peer (with this key) supports
        /// </summary>
        public ConcurrentDictionary<String, List<KeyValuePair<String, String>>> GroupsSupported
        {
            get => Profile.GroupsSupported;
        }

        //TODO: check SSL certificate with SNI
        internal void SendTcpMessOnQuery(Object sender, MessageEventArgs e)
        {
            if (!(e.Message is MulticastMessage))
            {
                throw new ArgumentException("Bad format of the message");
            }

            var parsingSsl = new ParsingTcpData();
            TcpClient client = null;
            NetworkStream stream = null;
            try
            {
                Random random = new Random();
                Byte[] bytes = parsingSsl.GetDecodedData(new TcpMessage(random.Next(0, Int32.MaxValue), e.Message.VersionOfProtocol, Profile.GroupsSupported));

                if (Service.IgnoreDuplicateMessages && sentMessages.TryAdd(bytes))
                {
                    return;
                }

                var message = e.Message as MulticastMessage;
                client = new TcpClient(e.RemoteEndPoint.AddressFamily);
                client.Connect(((IPEndPoint)e.RemoteEndPoint).Address, message.TcpPort);

                stream = client.GetStream();
                stream.Write(bytes, 0, bytes.Length);
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
        public static ServiceDiscovery GetInstance(ConcurrentDictionary<String, List<KeyValuePair<String, String>>> groupsSupported = null)
        {
            Lock.InitWithLock(Lock.lockService, new ServiceDiscovery(groupsSupported), ref instance);
            return instance;
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceDiscovery"/> class.
        /// </summary>
        public static ServiceDiscovery GetInstance(Boolean useIpv4, Boolean useIpv6, ConcurrentDictionary<String, List<KeyValuePair<String, String>>> groupsSupported = null)
        {
            Lock.InitWithLock(Lock.lockService, new ServiceDiscovery(useIpv4, useIpv6, groupsSupported), ref instance);
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

        public void QueryAllServices()
        {
            Service.SendQuery();
        }
    }
}