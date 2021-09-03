using LUC.ApiClient;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.Interfaces;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;
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
        private static readonly Object ttyLock;
        private static DiscoveryService discoveryService;
        private static readonly SettingsService settingsService;
        private static readonly CancellationTokenSource cancellationTokenSource;

        static FunctionalTest()
        {
            ttyLock = new Object();

            settingsService = new SettingsService();
            cancellationTokenSource = new CancellationTokenSource();
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
            SetUpTests.AssemblyInitialize();

            settingsService.CurrentUserProvider = new CurrentUserProvider();

            ApiClient.ApiClient apiClient = new ApiClient.ApiClient(settingsService.CurrentUserProvider, SetUpTests.LoggingService);
            apiClient.SyncingObjectsList = new SyncingObjectsList();

            String Login = "integration1";
            String Password = "integration1";

            LoginResponse loginResponse = null;
            do
            {
                loginResponse = await apiClient.LoginAsync(Login, Password).ConfigureAwait(continueOnCapturedContext: false);

                if(!loginResponse.IsSuccess)
                {
                    Console.WriteLine("Check your connection to Internet, because you cannot login\n" +
                        "If you did that, press any keyboard key to try login again");

                    //intercept: true says that pressed key won't be shown in console
                    Console.ReadKey(intercept: true);
                }
            }
            while (!loginResponse.IsSuccess);

            apiClient.CurrentUserProvider.RootFolderPath = settingsService.ReadUserRootFolderPath();

            ConcurrentDictionary<String, String> groupsSupported = new ConcurrentDictionary<String, String>();

            var bucketDirectoryPathes = settingsService.CurrentUserProvider.ProvideBucketDirectoryPathes();
            var sslCert = "<SSL-Cert>";
            foreach (var bucketPath in bucketDirectoryPathes)
            {
                var bucketName = Path.GetFileName(bucketPath);
                groupsSupported.TryAdd(bucketName, sslCert);
                groupsSupported.TryAdd(bucketName, sslCert);
            }

            discoveryService = new DiscoveryService(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 1, groupsSupported), settingsService.CurrentUserProvider);
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

            

            while (true)
            {
                Boolean isTesterOnlyInNetwork = IsTesterOnlyInNetwork();

                ShowAvailableUserOptions();

                var pressedKey = Console.ReadKey().Key;
                Console.WriteLine();

                try
                {
                    Contact contact = null;
                    if ((!isTesterOnlyInNetwork) && (((ConsoleKey.D1 <= pressedKey) && (pressedKey <= ConsoleKey.D6)) ||
                        ((ConsoleKey.NumPad1 <= pressedKey) && (pressedKey <= ConsoleKey.NumPad6))))
                    {
                        GetRemoteContact(discoveryService, ref contact);
                    }
                    else if (isTesterOnlyInNetwork)
                    {
                        contact = discoveryService.NetworkEventInvoker.OurContact;
                    }

                    await TryExecuteSelectedOperationAsync(contact, apiClient, settingsService.CurrentUserProvider, pressedKey)
                        .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
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

        private static Boolean IsTesterOnlyInNetwork()
        {
            Boolean isTesterOnlyInNetwork = false;
            String readLine = null;
            String isTrue = "1";
            String isFalse = "2";

            do
            {
                Console.WriteLine("Are you only in network?\n" +
                "1 - yes\n" +
                "2 - no");
                readLine = Console.ReadLine().Trim();

                if (readLine == isTrue)
                {
                    isTesterOnlyInNetwork = true;
                }
                else if (readLine == isFalse)
                {
                    isTesterOnlyInNetwork = false;
                }
            }
            while ((readLine != isTrue) && (readLine != isFalse));

            return isTesterOnlyInNetwork;
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
            var contacts = discoveryService.OnlineContacts.Where(c => (c.ID != discoveryService.NetworkEventInvoker.OurContact.ID) &&
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
                ClientKadOperation kadOperation = new ClientKadOperation(discoveryService.ProtocolVersion);

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

                            //because we listen TCP messages in other threads
                            await Task.Delay(TimeSpan.FromSeconds(value: 2)).ConfigureAwait(continueOnCapturedContext: false);

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
                            await DownloadRandomFile(apiClient, currentUserProvider, remoteContact).ConfigureAwait(false);

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

        /// <summary>
        /// Will download random file which isn't in current PC if you aren't only in network. Otherwise it will download file which is
        /// </summary>
        /// <returns></returns>
        private async static Task DownloadRandomFile(IApiClient apiClient, 
            ICurrentUserProvider currentUserProvider, Contact remoteContact)
        {
            var download = new Download(discoveryService, IOBehavior.Asynchronous);
            var localFolderPath = settingsService.ReadUserRootFolderPath();

            String filePrefix = String.Empty;
            var randomFileToDownload = await RandomFileToDownload(apiClient, currentUserProvider, remoteContact, localFolderPath, filePrefix).ConfigureAwait(false);

            await download.DownloadFileAsync(localFolderPath, bucketName: discoveryService.GroupsSupported.First().Key,
                filePrefix, randomFileToDownload.OriginalName, randomFileToDownload.Bytes, 
                randomFileToDownload.Version, cancellationTokenSource.Token)
                .ConfigureAwait(false);
        }

        private async static Task<ObjectDescriptionModel> RandomFileToDownload(IApiClient apiClient, ICurrentUserProvider currentUserProvider, Contact remoteContact, String localFolderPath, String filePrefix)
        {
            var bucketDirectoryPathes = currentUserProvider.ProvideBucketDirectoryPathes();

            var random = new Random();
            var rndBcktDrctrPth = bucketDirectoryPathes[random.Next(bucketDirectoryPathes.Count)];
            var serverBucketName = currentUserProvider.GetBucketNameByDirectoryPath(rndBcktDrctrPth).ServerName;

            var objectsListResponse = await apiClient.ListAsync(serverBucketName, filePrefix).ConfigureAwait(continueOnCapturedContext: false);
            var objectsListModel = objectsListResponse.ToObjectsListModel();

            Boolean isOnlyInNetwork = discoveryService.ContactId == remoteContact.ID;

            //select from objectsListModel files which exist in current PC if tester is only in network. 
            //If the last one is not, select files which don't exist in current PC
            var bjctDscrptnsFrDwnld = objectsListModel.ObjectDescriptions.Where(cachedFileInServer =>
            {
                var fullPathToFile = Path.Combine(localFolderPath, rndBcktDrctrPth, filePrefix, cachedFileInServer.OriginalName);
                var isFileInCurrentPc = File.Exists(fullPathToFile);

                Boolean shouldBeDownloaded;
                if(isOnlyInNetwork)
                {
                    shouldBeDownloaded = isFileInCurrentPc;
                }
                else
                {
                    shouldBeDownloaded = !isFileInCurrentPc;
                }

                return shouldBeDownloaded;
            }).ToList();

            ObjectDescriptionModel randomFileToDownload;
            if (bjctDscrptnsFrDwnld.Count == 0 && isOnlyInNetwork)
            {
                randomFileToDownload = objectsListModel.ObjectDescriptions[random.Next(objectsListModel.ObjectDescriptions.Count)];
                await apiClient.DownloadFileAsync(serverBucketName, filePrefix, localFolderPath, randomFileToDownload.OriginalName, randomFileToDownload).ConfigureAwait(false);
            }
            else if(bjctDscrptnsFrDwnld.Count > 0)
            {
                randomFileToDownload = bjctDscrptnsFrDwnld[random.Next(bjctDscrptnsFrDwnld.Count)];
            }
            else
            {
                Console.WriteLine($"You should put few file in {localFolderPath} and using WpfClient, upload it");
                throw new InvalidOperationException();
            }

            return randomFileToDownload;
        }
    }
}
