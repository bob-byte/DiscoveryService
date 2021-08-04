using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Test
{
    class FunctionalTest
    {
        private static readonly Object ttyLock = new Object();
        private static DiscoveryService discoveryService;

        ~FunctionalTest()
        {
            discoveryService?.Stop();
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

            foreach (var address in discoveryService.Service.RunningIpAddresses)
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

            while (true)
            {
                ShowAvailableUserOptions();

                var pressedKey = Console.ReadKey().Key;
                Console.WriteLine();

                Contact remoteContact = null;
                if (((ConsoleKey.D1 <= pressedKey) && (pressedKey <= ConsoleKey.D6)) || 
                    ((ConsoleKey.NumPad1 <= pressedKey) && (pressedKey <= ConsoleKey.NumPad6)))
                {
                    RemoteContact(discoveryService, ref remoteContact);
                }

                TryExecuteSelectedOperationWhileKeyIsInvalid(remoteContact, pressedKey);
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

        private static void ShowAvailableUserOptions()
        {
            Console.WriteLine($"Select an operation:\n" +
                              $"1 - send multicast\n" +
                              $"2 - send {typeof(PingRequest).Name}\n" +
                              $"3 - send {typeof(StoreRequest).Name}\n" +
                              $"4 - send {typeof(FindNodeRequest).Name}\n" +
                              $"5 - send {typeof(FindValueRequest).Name}\n" +
                              $"6 - send {typeof(AcknowledgeTcpMessage).Name}\n" +
                              $"7 - test count available connections");
        }

        private static void RemoteContact(DiscoveryService discoveryService, ref Contact remoteContact)
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
        }

        private static Contact RandomContact(DiscoveryService discoveryService)
        {
            var contacts = discoveryService.KnownContacts.Where(c => (c.ID != discoveryService.Service.OurContact.ID) &&
                (c.LastActiveIpAddress != null)).ToArray();

            Random random = new Random();
            var randomContact = contacts[random.Next(contacts.Length)];

            return randomContact;
        }

        private static void TryExecuteSelectedOperationWhileKeyIsInvalid(Contact remoteContact, ConsoleKey pressedKey)
        {
            while (true)
            {
                ClientKadOperation kadOperation = new ClientKadOperation();

                switch (pressedKey)
                {
                    case ConsoleKey.NumPad1:
                    case ConsoleKey.D1:
                        {
                            discoveryService.QueryAllServices();

                            //We need to wait, because Bootstrap method executes in other threads. 
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
                            SendTcpMessage(remoteContact);
                            Thread.Sleep(TimeSpan.FromSeconds(value: 2));//because we listen TCP messages in other threads

                            return;
                        }

                    case ConsoleKey.NumPad7:
                    case ConsoleKey.D7:
                        {
                            try
                            {
                                CountAvailableConnectionsTest();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }

                            return;
                        }

                    default:
                        {
                            continue;
                        }
                }
            }
        }

        private static void SendTcpMessage(Contact remoteContact)
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
        }

        private static void CountAvailableConnectionsTest()
        {
            using (var powerShell = PowerShell.Create())
            {
                var webClient = new WebClient();
                var pathToPowercatScript = "https://raw.githubusercontent.com/besimorhino/powercat/master/powercat.ps1";
                var script = webClient.DownloadString(pathToPowercatScript);

                try
                {
                    powerShell.AddScript(script).AddScript("Invoke-Method").Invoke();
                }
                catch (ParseException)
                {
                    throw;
                }
                powerShell.AddCommand(cmdlet: "powercat");

                var parameters = PowercatParameters();
                powerShell.AddParameters(parameters);

                Int32 countConnection;
                do
                {
                    Console.Write($"Input count times you want to send the file: ");
                }
                while (!Int32.TryParse(Console.ReadLine(), out countConnection));

                for (Int32 numConnection = 1; numConnection <= countConnection; numConnection++)
                {
                    Console.WriteLine($"{nameof(numConnection)} = {numConnection}");

                    powerShell.BeginInvoke();

                    var waitToServerRead = TimeSpan.FromSeconds(0.5);
                    Thread.Sleep(waitToServerRead);

                    powerShell.Stop();
                }
            }
        }

        private static Dictionary<String, Object> PowercatParameters()
        {
            Dictionary<String, Object> parameters = new Dictionary<String, Object>();

            IPAddress serverIpAddress = null;
            do
            {
                Console.Write("Input IP-address of a server to connect: ");

                try
                {
                    serverIpAddress = IPAddress.Parse(Console.ReadLine());
                }
                catch (FormatException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            while (serverIpAddress == null);

            parameters.Add(key: "c", value: serverIpAddress.ToString());
            parameters.Add("p", discoveryService.RunningTcpPort.ToString());

            String pathToFileToSend;
            do
            {
                Console.Write($"Input full path to file which you want to send to server: ");
                pathToFileToSend = Console.ReadLine();
            }
            while (!File.Exists(pathToFileToSend));

            parameters.Add("i", pathToFileToSend);

            return parameters;
        }
    }
}
