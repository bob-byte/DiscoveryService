using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Messages;
//using LUC.Interfaces;
//using LUC.Services.Implementation;

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

        //[Import(typeof(ILoggingService))]
        //private static readonly ILoggingService log = new LoggingService();
        private static ServiceDiscovery instance;

        private readonly ServiceProfile profile;

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

        private ServiceDiscovery(Boolean useIpv4, Boolean useIpv6, ConcurrentDictionary<String, List<String>> groupsSupported = null)
        {
            profile = new ServiceProfile(MinValueTcpPort, MaxValueTcpPort, UdpPort, 
                Message.ProtocolVersion, groupsSupported);
            Service = new Service(profile);

            Service.UseIpv4 = useIpv4;
            Service.UseIpv6 = useIpv6;
        }

        private ServiceDiscovery(ConcurrentDictionary<String, List<String>> groupsSupported = null)
        {
            profile = new ServiceProfile(MinValueTcpPort, MaxValueTcpPort, UdpPort, 
                Message.ProtocolVersion, groupsSupported);
            Service = new Service(profile);
            Service.QueryReceived += SendTcpMessOnQuery;
        }

        //TODO: check SSL certificate with SNI
        internal void SendTcpMessOnQuery(Object sender, MessageEventArgs e)
        {
            var parsingSsl = new ParsingTcpData();
            TcpClient client = null;
            NetworkStream stream = null;
            try
            {
                Byte[] bytes = parsingSsl.GetDecodedData(new TcpMessage(e.Message.VersionOfProtocol, profile.GroupsSupported));

                client = new TcpClient(e.RemoteEndPoint.AddressFamily);
                if (!(e.Message is MulticastMessage message))
                {
                    throw new ArgumentException("Bad format of the message");
                }
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
        /// If user of ServiceDiscovery forget to call method Stop
        /// </summary>
        ~ServiceDiscovery()
        {
            Stop();
        }

        public Service Service { get; }

        public ConcurrentDictionary<String, List<String>> GroupsSupported
        {
            get
            {
                return profile.GroupsSupported;
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
        public static ServiceDiscovery GetInstance(ConcurrentDictionary<String, List<String>> groupsSupported = null)
        {
            Lock.InitWithLock(Lock.lockService, new ServiceDiscovery(groupsSupported), ref instance);
            return instance;
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceDiscovery"/> class.
        /// </summary>
        public static ServiceDiscovery GetInstance(Boolean useIpv4, Boolean useIpv6, ConcurrentDictionary<String, List<String>> groupsSupported = null)
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

            machineId = profile.MachineId;
        }

        public void QueryAllServices()
        {
            Service.SendQuery();
        }
    }
}