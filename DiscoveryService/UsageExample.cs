using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;

namespace LUC.DiscoveryService
{
    class UsageExample
    {
        private static readonly Object ttyLock = new Object();
        private static DiscoveryService discoveryService;

        ~UsageExample()
        {
            discoveryService.Stop();
        }

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

        static void Main(string[] args)
        {
            ConcurrentDictionary<String, String> groupsSupported = new ConcurrentDictionary<String, String>();
            groupsSupported.TryAdd("the-dubstack-engineers-res", "<SSL-Cert1>");
            groupsSupported.TryAdd("the-dubstack-architects-res", "<SSL-Cert2>");

            discoveryService = DiscoveryService.Instance(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 1, groupsSupported));
            discoveryService.Start();

            foreach (var address in discoveryService.Service.RunningIpAddresses())
            {
                Console.WriteLine($"IP address {address}");
            }

            discoveryService.Service.AnswerReceived += OnGoodTcpMessage;
            discoveryService.Service.QueryReceived += OnGoodUdpMessage;

            discoveryService.Service.PingReceived += OnPingReceived;
            discoveryService.Service.StoreReceived += OnStoreReceived;
            discoveryService.Service.FindNodeReceived += OnFindNodeReceived;
            discoveryService.Service.FindValueReceived += OnFindValueReceived;

            discoveryService.Service.NetworkInterfaceDiscovered += (s, e) =>
            {
                foreach (var nic in e.NetworkInterfaces)
                {
                    Console.WriteLine($"discovered NIC '{nic.Name}'");
                };
            };

            Contact remoteContact = null;
            while (true)
            {
                while (remoteContact == null)
                {
                    discoveryService.QueryAllServices();//to get any contacts

                    try
                    {
                        remoteContact = RandomContact(discoveryService);
                    }
                    catch (IndexOutOfRangeException) //if knowContacts.Count == 0
                    {
                        ;//do nothing
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(value: 3));
                }


                TryExecuteSelectedOperationWhileKeyIsInvalid(remoteContact);
            }
        }

        private static void OnGoodTcpMessage(Object sender, TcpMessageEventArgs e)
        {
            lock (ttyLock)
            {
                Console.WriteLine("=== TCP {0:O} ===", DateTime.Now);
                var tcpMessage = e.Message<AcknowledgeTcpMessage>(whetherReadMessage: false);
                Console.WriteLine(tcpMessage.ToString());

                if ((tcpMessage != null) && (e.SendingEndPoint is IPEndPoint endPoint))
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

        private static Contact RandomContact(DiscoveryService discoveryService)
        {
            var contacts = discoveryService.KnownContacts.Where(c => (c.ID != discoveryService.Service.OurContact.ID) &&
                (c.LastActiveIpAddress != null)).ToArray();

            Random random = new Random();
            var randomContact = contacts[random.Next(contacts.Length)];

            return randomContact;
        }

        private static void TryExecuteSelectedOperationWhileKeyIsInvalid(Contact remoteContact)
        {
            Console.WriteLine($"Select operation:\n" +
                    $"1 - send multicast\n" +
                    $"2 - send {typeof(PingRequest).Name}\n" +
                    $"3 - send {typeof(StoreRequest).Name}\n" +
                    $"4 - send {typeof(FindNodeRequest).Name}\n" +
                    $"5 - send {typeof(FindValueRequest).Name}\n" +
                    $"6 - send {typeof(AcknowledgeTcpMessage).Name}\n");

            while (true)
            {
                ClientKadOperation kadOperation = new ClientKadOperation();

                var pressedKey = Console.ReadKey(intercept: true).Key;//does not display pressed key
                switch (pressedKey)
                {
                    case ConsoleKey.NumPad1:
                    case ConsoleKey.D1:
                        {
                            discoveryService.QueryAllServices();

                            //We need to wait, because Bootstrap method executes in another threads. 
                            //When we send UDP messages, we will receive TCP messages and 
                            //call NetworkEventHandler.DistributedHashTable.Bootstrap-s
                            Thread.Sleep(TimeSpan.FromSeconds(value: 2));

                            return;
                        }

                    case ConsoleKey.NumPad2:
                    case ConsoleKey.D2:
                        {
                            kadOperation.Ping(discoveryService.Service.OurContact, remoteContact);
                            return;
                        }

                    case ConsoleKey.NumPad3:
                    case ConsoleKey.D3:
                        {
                            kadOperation.Store(discoveryService.Service.OurContact, discoveryService.Service.OurContact.ID,
                                discoveryService.MachineId, remoteContact);
                            return;
                        }

                    case ConsoleKey.NumPad4:
                    case ConsoleKey.D4:
                        {
                            kadOperation.FindNode(discoveryService.Service.OurContact, remoteContact.ID, remoteContact);
                            return;
                        }

                    case ConsoleKey.NumPad5:
                    case ConsoleKey.D5:
                        {
                            kadOperation.FindValue(discoveryService.Service.OurContact, discoveryService.Service.OurContact.ID, remoteContact);
                            return;
                        }

                    case ConsoleKey.NumPad6:
                    case ConsoleKey.D6:
                        {
                            var remoteEndPoint = new IPEndPoint(remoteContact.LastActiveIpAddress, remoteContact.TcpPort);
                            var eventArgs = new UdpMessageEventArgs
                            {
                                RemoteEndPoint = remoteEndPoint
                            };

                            Random random = new Random();
                            UInt32 messageId = (UInt32)random.Next(maxValue: Int32.MaxValue);

                            eventArgs.SetMessage(new UdpMessage(messageId, discoveryService.ProtocolVersion,
                                remoteContact.TcpPort, machineId: discoveryService.MachineId));
                            discoveryService.SendTcpMessageAsync(discoveryService, eventArgs).GetAwaiter().GetResult();

                            Thread.Sleep(TimeSpan.FromSeconds(value: 2));
                            return;
                        }

                    default:
                        {
                            continue;
                        }
                }
            }
        }
    }
}
