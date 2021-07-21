using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;

namespace LUC.DiscoveryService
{
    class UsageExample
    {
        private static readonly Object ttyLock = new Object();

        /// <summary>
        ///   User Groups with their SSL certificates.
        ///   SSL should have SNI ( Server Name Indication ) feature enabled
        ///   This allows us to tell which group we are trying to connect to, so that the server knows which certificate to use.
        ///
        ///   We generate SSL and key/certificate pairs for every group. These are distributed from server to user’s computers 
        ///   which are authenticated for the buckets later.
        ///
        ///   These are rotated any time membership changes e.g., when someone is removed from a group/shared folder. 
        ///   We can require both ends of the HTTPS connection to authenticate with the same certificate (the certificate for the group).
        ///   This proves that both ends of the connection are authenticated.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        public static ConcurrentDictionary<String, String> OurSupportedGroups { get; set; } = new ConcurrentDictionary<String, String>();

        public static ConcurrentDictionary<String, String> GroupsDiscovered { get; set; } = new ConcurrentDictionary<String, String>();

        /// <summary>
        /// IP address of groups which were discovered.
        /// Key is a network in a format "IP-address:port"
        /// Value is the list of groups, which peer supports.
        /// </summary>
        /// <remarks>
        /// This property is populated when OnGoodTcpMessage event arrives.
        /// </remarks>
        public static ConcurrentDictionary<String, String> KnownIps { get; set; } = new ConcurrentDictionary<String, String>();

        private static void OnBadMessage(Object sender, Byte[] packet)
        {
            lock (ttyLock)
            {
                Console.WriteLine(">>> {0:O} <<<", DateTime.Now);
                Console.WriteLine("Malformed message (base64)");
                Console.WriteLine(Convert.ToBase64String(packet));
                // log message
            }

            Environment.Exit(1);
        }

        private static void OnGoodTcpMessage(Object sender, TcpMessageEventArgs e)
        {
            lock (ttyLock)
            {
                Console.WriteLine("=== TCP {0:O} ===", DateTime.Now);
                var tcpMessage = e.Message<AcknowledgeTcpMessage>(whetherReadMessage: false);
                Console.WriteLine(tcpMessage.ToString());

                if ((tcpMessage != null) && (e.RemoteContact is IPEndPoint endPoint))
                {
                    var realEndPoint = $"{endPoint.Address}:{tcpMessage.TcpPort}";

                    foreach (var group in tcpMessage.GroupIds)
                    {
                        if (!GroupsDiscovered.TryAdd(realEndPoint, group))
                        {
                            GroupsDiscovered.TryRemove(realEndPoint, out _);
                            GroupsDiscovered.TryAdd(realEndPoint, group);
                        }
                    }
                }
            }
        }

        private static void OnGoodUdpMessage(Object sender, UdpMessageEventArgs e)
        {
            lock (ttyLock)
            {
                Console.WriteLine("=== UDP {0:O} ===", DateTime.Now);

                var message = e.Message<UdpMessage>(whetherReadMessage: false);
                Console.WriteLine(message.ToString());
                // do nothing, this is for debugging only
            }
        }

        private static void OnPingReceived(Object sender, TcpMessageEventArgs e)
        {
            lock (ttyLock)
            {
                Console.WriteLine("=== Kad PING received {0:O} ===", DateTime.Now);

                var message = e.Message<PingRequest>(whetherReadMessage: false);
                Console.WriteLine(message.ToString());
            }
        }

        private static void OnStoreReceived(Object sender, TcpMessageEventArgs e)
        {
            lock (ttyLock)
            {
                Console.WriteLine("=== Kad STORE received {0:O} ===", DateTime.Now);

                var message = e.Message<StoreRequest>(whetherReadMessage: false);
                Console.WriteLine(message.ToString());
            }
        }

        private static void OnFindNodeReceived(Object sender, TcpMessageEventArgs e)
        {
            lock (ttyLock)
            {
                Console.WriteLine("=== Kad FindNode received {0:O} ===", DateTime.Now);

                var message = e.Message<FindNodeRequest>(whetherReadMessage: false);
                Console.WriteLine(message.ToString());
            }
        }

        private static void OnFindValueReceived(Object sender, TcpMessageEventArgs e)
        {
            lock (ttyLock)
            {
                Console.WriteLine("=== Kad FindValue received {0:O} ===", DateTime.Now);

                var message = e.Message<FindValueRequest>(whetherReadMessage: false);
                Console.WriteLine(message.ToString());
            }
        }

        static void Main(string[] args)
        {
            foreach (var a in NetworkEventHandler.IPAddresses())
            {
                Console.WriteLine($"IP address {a}");
            }

            ConcurrentDictionary<String, String> groupsSupported = new ConcurrentDictionary<String, String>();
            groupsSupported.TryAdd("the-dubstack-engineers-res", "<SSL-Cert1>");
            groupsSupported.TryAdd("the-dubstack-architects-res", "<SSL-Cert2>");

            var serviceDiscovery = DiscoveryService.Instance(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 1, groupsSupported));
            serviceDiscovery.Start();

            serviceDiscovery.Service.AnswerReceived += OnGoodTcpMessage;
            serviceDiscovery.Service.QueryReceived += OnGoodUdpMessage;
            serviceDiscovery.Service.MalformedMessage += OnBadMessage;

            serviceDiscovery.Service.PingReceived += OnPingReceived;
            serviceDiscovery.Service.StoreReceived += OnStoreReceived;
            serviceDiscovery.Service.FindNodeReceived += OnFindNodeReceived;
            serviceDiscovery.Service.FindValueReceived += OnFindValueReceived;

            serviceDiscovery.Service.NetworkInterfaceDiscovered += (s, e) =>
            {
                foreach (var nic in e.NetworkInterfaces)
                {
                    Console.WriteLine($"discovered NIC '{nic.Name}'");
                };
            };

            Console.WriteLine("If you want to start sending multicast messages, " +
                "in order to receive TCP answers, press any key each time.\n" +
                "If you want to stop, press Esc");

            ConsoleKey pressedKey = default;
            while (true)
            {
                pressedKey = Console.ReadKey(intercept: true).Key;//does not display pressed key
                if (pressedKey != ConsoleKey.Escape)
                {
                    serviceDiscovery.QueryAllServices();
                }
                else
                {
                    serviceDiscovery.Stop();
                    break;
                }
            }
        }
    }
}