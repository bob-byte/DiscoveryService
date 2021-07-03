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
using LUC.DiscoveryService.Kademlia.Protocols.Tcp;
using LUC.DiscoveryService.Kademlia.Interfaces;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   LightUpon.Cloud Service Discovery maintens the list of IP addresses in LAN. It realizes pattern Singleton
    /// </summary>
    public class DiscoveryService : CollectedInfoInLan
    {       
        private static DiscoveryService instance;

        private IProtocol protocol = new TcpProtocol();

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

        /// <summary>
        /// If user of ServiceDiscovery forget to call method Stop
        /// </summary>
        ~DiscoveryService()
        {
            Stop();
        }

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
            Service.AnswerReceived += Service.Bootstrap;
            Service.FindNodeReceived += SendOurCloseContactsAndPort;
            Service.PingReceived += SendSameRandomId;

            //Service.FindValueReceived += (sender, messageArgs) => FindValueResponse.SendOurCloseContactsAndMachineId(
            //    messageArgs.Message as FindValueRequest,
            //    messageArgs.RemoteContact.IPAddress,
            //    Service.DistributedHashTable.Node.BucketList.GetCloseContacts(messageArgs.LocalContact.ID, messageArgs.LocalContact.ID),
            //    MachineId,
            //    RunningTcpPort);
        }

        public void AddNewContact(Object sender, TcpMessageEventArgs e)
        {
            if ((!(e?.Message is TcpMessage tcpMessage)) ||
               (!(e.RemoteContact is IPEndPoint iPEndPoint)))
            {
                throw new ArgumentException($"Bad format of {nameof(e)}");
            }
            else
            {
                KnownContacts.Add(new Contact(new TcpProtocol(), tcpMessage.MachineId, iPEndPoint.Address, tcpMessage.TcpPort));
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
            if((e?.Message is UdpMessage udpMessage) && (e?.RemoteEndPoint is IPEndPoint ipEndPoint))
            {
                Random random = new Random();
                var sendingContact = Service.OurContacts.Single(c => c.IPAddress.Equals(ipEndPoint.Address));

                var tcpMessage = new TcpMessage(
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

                tcpMessage.Send(new IPEndPoint(ipEndPoint.Address, (Int32)udpMessage.TcpPort), bytesToSend).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentException($"Bad format of {nameof(e)}");
            }
        }

        public void SendOurCloseContactsAndPort(Object sender, TcpMessageEventArgs e)
        {
            if (((e?.Message is FindNodeRequest message)) &&
               ((e.RemoteContact is IPEndPoint iPEndPoint)))
            {
                var response = new FindNodeResponse
                {
                    RandomID = ID.RandomID.Value,
                    Contacts = Service.DistributedHashTable.Node.BucketList.GetCloseContacts(MachineId, MachineId),
                    TcpPort = RunningTcpPort
                };

                response.Send(new IPEndPoint(iPEndPoint.Address, (Int32)message.TcpPort)).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentException($"Bad format of {nameof(e)}");
            }
        }

        private void SendSameRandomId(Object sender, TcpMessageEventArgs e)
        {
            if (e?.Message is PingRequest message)
            {
                var response = new FindNodeResponse
                {
                    RandomID = ID.RandomID.Value,
                    Contacts = Service.DistributedHashTable.Node.BucketList.GetCloseContacts(MachineId, MachineId),
                    TcpPort = RunningTcpPort
                };

                response.Send(new IPEndPoint(e.RemoteContact.IPAddress, (Int32)e.RemoteContact.TcpPort), response.ToByteArray()).ConfigureAwait(false);
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

        /// <summary>
        ///    Stop listening TCP, UDP messages and sending them
        /// </summary>
        public void Stop()
        {
            if (isDiscoveryServiceStarted)
            {
                Service.Stop();
                Service = null;
                ServiceInstanceShutdown?.Invoke(this, new TcpMessageEventArgs());

                isDiscoveryServiceStarted = false;
            }
        }
    }
}
