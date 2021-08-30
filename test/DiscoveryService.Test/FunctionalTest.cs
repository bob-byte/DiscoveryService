using LUC.ApiClient;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.Interfaces;
using LUC.Services.Implementation;
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
        private static readonly LoggingService loggingService;
        private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        static FunctionalTest()
        {
            loggingService = new LoggingService
            {
                SettingsService = new SettingsService()
            };
        }

        ~FunctionalTest()
        {
            discoveryService?.Stop();
            cancellationTokenSource.Cancel();
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

        static async Task Main(string[] args)
        {
            ConcurrentDictionary<String, String> groupsSupported = new ConcurrentDictionary<String, String>();
            groupsSupported.TryAdd("the-dubstack-engineers-res", "<SSL-Cert1>");
            groupsSupported.TryAdd("the-dubstack-architects-res", "<SSL-Cert2>");

            discoveryService = new DiscoveryService(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 1, groupsSupported));
            discoveryService.Start();

            foreach (var address in discoveryService.NetworkEventInvoker.RunningIpAddresses)
            {
                Console.WriteLine($"IP address {address}");
            }

            discoveryService.NetworkEventInvoker.AnswerReceived += OnGoodTcpMessage;
            discoveryService.NetworkEventInvoker.QueryReceived += OnGoodUdpMessage;

            discoveryService.NetworkEventInvoker.PingReceived += OnPingReceived;
            discoveryService.NetworkEventInvoker.StoreReceived += OnStoreReceived;
            discoveryService.NetworkEventInvoker.FindNodeReceived += OnFindNodeReceived;
            discoveryService.NetworkEventInvoker.FindValueReceived += OnFindValueReceived;

            discoveryService.NetworkEventInvoker.NetworkInterfaceDiscovered += (s, e) =>
            {
                foreach (var nic in e.NetworkInterfaces)
                {
                    Console.WriteLine($"discovered NIC '{nic.Name}'");
                };
            };

            loggingService.SettingsService.CurrentUserProvider = new CurrentUserProvider();

            ApiClient.ApiClient apiClient = new ApiClient.ApiClient(loggingService.SettingsService.CurrentUserProvider, loggingService);

            String Login = "integration1";
            String Password = "integration1";
            await apiClient.LoginAsync(Login, Password).ConfigureAwait(continueOnCapturedContext: false);
            apiClient.CurrentUserProvider.RootFolderPath = loggingService.SettingsService.ReadUserRootFolderPath();

            while (true)
            {
                ShowAvailableUserOptions();

                var pressedKey = Console.ReadKey().Key;
                Console.WriteLine();

                Contact remoteContact = null;
                if (((ConsoleKey.D1 <= pressedKey) && (pressedKey <= ConsoleKey.D6)) || 
                    ((ConsoleKey.NumPad1 <= pressedKey) && (pressedKey <= ConsoleKey.NumPad6)))
                {
                    GetRemoteContact(discoveryService, ref remoteContact);
                }

                await TryExecuteSelectedOperationAsync(remoteContact, apiClient, loggingService.SettingsService.CurrentUserProvider, pressedKey);
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
                              $"7 - test count available connections\n" +
                              $"8 - download random file from another contact(-s)");
        }

        private static void GetRemoteContact(DiscoveryService discoveryService, ref Contact remoteContact)
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
            var contacts = discoveryService.KnownContacts.Where(c => (c.ID != discoveryService.NetworkEventInvoker.OurContact.ID) &&
                (c.LastActiveIpAddress != null)).ToArray();

            Random random = new Random();
            var randomContact = contacts[random.Next(contacts.Length)];

            return randomContact;
        }

        /// <summary>
        /// Try execute selected operation while key is invalid
        /// </summary>
        /// <param name="remoteContact"></param>
        /// <param name="apiClient"></param>
        /// <param name="currentUserProvider"></param>
        /// <param name="pressedKey"></param>
        /// <returns></returns>
        private static async Task TryExecuteSelectedOperationAsync(Contact remoteContact, IApiClient apiClient, ICurrentUserProvider currentUserProvider, ConsoleKey pressedKey)
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
                            await Task.Delay(TimeSpan.FromSeconds(value: 2)).ConfigureAwait(continueOnCapturedContext: false);

                            return;
                        }

                    case ConsoleKey.NumPad2:
                    case ConsoleKey.D2:
                        {
                            kadOperation.Ping(discoveryService.NetworkEventInvoker.OurContact, remoteContact);
                            return;
                        }

                    case ConsoleKey.NumPad3:
                    case ConsoleKey.D3:
                        {
                            kadOperation.Store(discoveryService.NetworkEventInvoker.OurContact, discoveryService.NetworkEventInvoker.OurContact.ID,
                                discoveryService.MachineId, remoteContact);
                            return;
                        }

                    case ConsoleKey.NumPad4:
                    case ConsoleKey.D4:
                        {
                            kadOperation.FindNode(discoveryService.NetworkEventInvoker.OurContact, remoteContact.ID, remoteContact);
                            return;
                        }

                    case ConsoleKey.NumPad5:
                    case ConsoleKey.D5:
                        {
                            kadOperation.FindValue(discoveryService.NetworkEventInvoker.OurContact, discoveryService.NetworkEventInvoker.OurContact.ID, remoteContact);
                            return;
                        }

                    case ConsoleKey.NumPad6:
                    case ConsoleKey.D6:
                        {
                            SendTcpMessage(remoteContact);
                            await Task.Delay(TimeSpan.FromSeconds(value: 2)).ConfigureAwait(continueOnCapturedContext: false);//because we listen TCP messages in other threads

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

                    case ConsoleKey.NumPad8:
                    case ConsoleKey.D8:
                        {
                            var download = new Download(discoveryService);
                            var localFolderPath = loggingService.SettingsService.ReadUserRootFolderPath();

                            var bucketDirectoryPathes = currentUserProvider.ProvideBucketDirectoryPathes();

                            var random = new Random();
                            var rndBcktDrctrPths = bucketDirectoryPathes[random.Next(bucketDirectoryPathes.Count)];
                            var serverBucketName = currentUserProvider.GetBucketNameByDirectoryPath(rndBcktDrctrPths).ServerName;

                            String filePrefix = String.Empty;
                            var objectsListResponse = await apiClient.ListAsync(serverBucketName, filePrefix).ConfigureAwait(continueOnCapturedContext: false);

                            var rndFlDscrptn = objectsListResponse.ObjectFileDescriptions[random.Next(objectsListResponse.ObjectFileDescriptions.Length)];

                            await download.DownloadFileAsync(localFolderPath, rndFlDscrptn.OriginalName, filePrefix, 
                                rndFlDscrptn.Bytes, IOBehavior.Asynchronous, 
                                cancellationTokenSource.Token).ConfigureAwait(false);

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
