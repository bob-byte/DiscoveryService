using LUC.ApiClient;
using LUC.DiscoveryService.Common;
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
        private static readonly Object s_ttyLock;
        private static DiscoveryService s_discoveryService;
        private static readonly SettingsService s_settingsService;
        private static readonly CancellationTokenSource s_cancellationTokenSource;

        static FunctionalTest()
        {
            s_ttyLock = new Object();

            s_settingsService = new SettingsService();
            s_cancellationTokenSource = new CancellationTokenSource();
        }

        ~FunctionalTest()
        {
            s_discoveryService?.Stop();
            s_cancellationTokenSource.Cancel();
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

        static async Task Main( String[] args )
        {
            SetUpTests.AssemblyInitialize();

            s_settingsService.CurrentUserProvider = new CurrentUserProvider();

            ApiClient.ApiClient apiClient = new ApiClient.ApiClient( s_settingsService.CurrentUserProvider, SetUpTests.LoggingService );
            apiClient.SyncingObjectsList = new SyncingObjectsList();

            String login = "integration1";
            String password = "integration1";

            LoginResponse loginResponse = null;
            do
            {
                loginResponse = await apiClient.LoginAsync( login, password ).ConfigureAwait( continueOnCapturedContext: false );

                if ( !loginResponse.IsSuccess )
                {
                    Console.WriteLine( "Check your connection to Internet, because you cannot login\n" +
                        "If you did that, press any keyboard key to try login again" );

                    //intercept: true says that pressed key won't be shown in console
                    Console.ReadKey( intercept: true );
                }
            }
            while ( !loginResponse.IsSuccess );

            apiClient.CurrentUserProvider.RootFolderPath = s_settingsService.ReadUserRootFolderPath();

            ConcurrentDictionary<String, String> groupsSupported = new ConcurrentDictionary<String, String>();

            IList<String> serverBuckets = s_settingsService.CurrentUserProvider.GetServerBuckets();
            String sslCert = "<SSL-Cert>";
            foreach ( String bucketOnServer in serverBuckets )
            {
                groupsSupported.TryAdd( bucketOnServer, sslCert );
                groupsSupported.TryAdd( bucketOnServer, sslCert );
            }

            s_discoveryService = new DiscoveryService( new ServiceProfile( useIpv4: true, useIpv6: true, protocolVersion: 1, groupsSupported ), s_settingsService.CurrentUserProvider );
            s_discoveryService.Start();

            foreach ( IPAddress address in s_discoveryService.NetworkEventInvoker.RunningIpAddresses )
            {
                Console.WriteLine( $"IP address {address}" );
            }

            s_discoveryService.NetworkEventInvoker.AnswerReceived += OnGoodTcpMessage;
            s_discoveryService.NetworkEventInvoker.QueryReceived += OnGoodUdpMessage;

            s_discoveryService.NetworkEventInvoker.PingReceived += OnPingReceived;
            s_discoveryService.NetworkEventInvoker.StoreReceived += OnStoreReceived;
            s_discoveryService.NetworkEventInvoker.FindNodeReceived += OnFindNodeReceived;
            s_discoveryService.NetworkEventInvoker.FindValueReceived += OnFindValueReceived;

            s_discoveryService.NetworkEventInvoker.NetworkInterfaceDiscovered += ( s, e ) =>
            {
                foreach ( System.Net.NetworkInformation.NetworkInterface nic in e.NetworkInterfaces )
                {
                    Console.WriteLine( $"discovered NIC '{nic.Name}'" );
                };
            };

            Boolean isCountConnectionsAvailableTest = NormalResposeFromUserAtClosedQuestion( "Do you want to test count available connections?" );

            Contact contact = null;
            Boolean isFirstTest = true;
            while ( true )
            {                
                try
                {
                    ConsoleKey pressedKey;

                    if ( ( isCountConnectionsAvailableTest ) && ( isFirstTest ) )
                    {
                        pressedKey = ConsoleKey.D7;
                    }
                    else
                    {
                        Boolean isTesterOnlyInNetwork = NormalResposeFromUserAtClosedQuestion( closedQuestion: "Are you only on the local network?" );
                        if ( ( contact == null ) || ( !isTesterOnlyInNetwork ) )
                        {
                            //in order to GetRemoteContact can send multicasts messages
                            contact = null;

                            GetRemoteContact( s_discoveryService, isTesterOnlyInNetwork, ref contact );
                        }

                        ShowAvailableUserOptions();
                        pressedKey = Console.ReadKey().Key;
                    }

                    Console.WriteLine();

                    await TryExecuteSelectedOperationAsync( contact, apiClient, s_settingsService.CurrentUserProvider, pressedKey )
                        .ConfigureAwait( continueOnCapturedContext: false );
                }
                catch ( Exception ex )
                {
                    Console.WriteLine( ex.ToString() );
                }

                isFirstTest = false;
            }
        }

        private static void OnGoodTcpMessage( Object sender, TcpMessageEventArgs e )
        {
            lock ( s_ttyLock )
            {
                Console.WriteLine( "=== TCP {0:O} ===", DateTime.Now );
                AcknowledgeTcpMessage tcpMessage = e.Message<AcknowledgeTcpMessage>( whetherReadMessage: false );
                Console.WriteLine( tcpMessage.ToString() );

                if ( ( tcpMessage != null ) && ( e.RemoteEndPoint is IPEndPoint endPoint ) )
                {
                    String realEndPoint = $"{endPoint.Address}:{tcpMessage.TcpPort}";

                    foreach ( String group in tcpMessage.GroupIds )
                    {
                        if ( !GroupsDiscovered.TryAdd( realEndPoint, group ) )
                        {
                            GroupsDiscovered.TryRemove( realEndPoint, out _ );
                            GroupsDiscovered.TryAdd( realEndPoint, group );
                        }
                    }
                }
            }
        }

        private static void OnGoodUdpMessage( Object sender, UdpMessageEventArgs e )
        {
            lock ( s_ttyLock )
            {
                Console.WriteLine( "=== UDP {0:O} ===", DateTime.Now );

                UdpMessage message = e.Message<UdpMessage>( whetherReadMessage: false );
                Console.WriteLine( message.ToString() );
                // do nothing, this is for debugging only
            }
        }

        private static void OnPingReceived( Object sender, TcpMessageEventArgs e )
        {
            lock ( s_ttyLock )
            {
                Console.WriteLine( "=== Kad PING received {0:O} ===", DateTime.Now );

                PingRequest message = e.Message<PingRequest>( whetherReadMessage: false );
                Console.WriteLine( message.ToString() );
            }
        }

        private static void OnStoreReceived( Object sender, TcpMessageEventArgs e )
        {
            lock ( s_ttyLock )
            {
                Console.WriteLine( "=== Kad STORE received {0:O} ===", DateTime.Now );

                StoreRequest message = e.Message<StoreRequest>( whetherReadMessage: false );
                Console.WriteLine( message.ToString() );
            }
        }

        private static void OnFindNodeReceived( Object sender, TcpMessageEventArgs e )
        {
            lock ( s_ttyLock )
            {
                Console.WriteLine( "=== Kad FindNode received {0:O} ===", DateTime.Now );

                FindNodeRequest message = e.Message<FindNodeRequest>( whetherReadMessage: false );
                Console.WriteLine( message.ToString() );
            }
        }

        private static void OnFindValueReceived( Object sender, TcpMessageEventArgs e )
        {
            lock ( s_ttyLock )
            {
                Console.WriteLine( "=== Kad FindValue received {0:O} ===", DateTime.Now );

                FindValueRequest message = e.Message<FindValueRequest>( whetherReadMessage: false );
                Console.WriteLine( message.ToString() );
            }
        }

        private static Boolean NormalResposeFromUserAtClosedQuestion(String closedQuestion)
        {
            Boolean userResponse = false;
            String readLine;
            String isTrue = "1";
            String isFalse = "2";

            do
            {
                Console.WriteLine( $"{closedQuestion}\n" +
                "1 - yes\n" +
                "2 - no" );
                readLine = Console.ReadLine().Trim();

                if ( readLine == isTrue )
                {
                    userResponse = true;
                }
                else if ( readLine == isFalse )
                {
                    userResponse = false;
                }
            }
            while ( ( readLine != isTrue ) && ( readLine != isFalse ) );

            return userResponse;
        }

        private static void ShowAvailableUserOptions() =>
            Console.WriteLine( $"Select an operation:\n" +
                               $"1 - send multicast\n" +
                               $"2 - send {typeof( PingRequest ).Name}\n" +
                               $"3 - send {typeof( StoreRequest ).Name}\n" +
                               $"4 - send {typeof( FindNodeRequest ).Name}\n" +
                               $"5 - send {typeof( FindValueRequest ).Name}\n" +
                               $"6 - send {typeof( AcknowledgeTcpMessage ).Name}\n" +
                               $"7 - test count available connections\n" +
                               $"8 - download random file from another contact(-s)" );

        private static void GetRemoteContact( DiscoveryService discoveryService, Boolean isOnlyInNetwork, ref Contact remoteContact )
        {
            while ( remoteContact == null )
            {
                discoveryService.QueryAllServices();//to get any contacts
                AutoResetEvent receivedFindNodeRequest = new AutoResetEvent( initialState: false );

                discoveryService.NetworkEventInvoker.FindNodeReceived += ( sender, eventArgs ) => receivedFindNodeRequest.Set();

                receivedFindNodeRequest.WaitOne();
                Thread.Sleep( TimeSpan.FromSeconds( value: 2 ) );
                try
                {
                    remoteContact = RandomContact( discoveryService, isOnlyInNetwork );
                }
                catch ( IndexOutOfRangeException ) //if knowContacts.Count == 0
                {
                    ;//do nothing
                }
            }
        }

        private static Contact RandomContact( DiscoveryService discoveryService, Boolean isOnlyInNetwork )
        {
            Func<Contact, Boolean> predicateToSelectContacts;
            if(isOnlyInNetwork)
            {
                predicateToSelectContacts = c => c.LastActiveIpAddress != null;
            }
            else
            {
                predicateToSelectContacts = c => ( c.LastActiveIpAddress != null ) && ( c.KadId == discoveryService.NetworkEventInvoker.OurContact.KadId );
            }

            Contact[] contacts = discoveryService.OnlineContacts.Where( predicateToSelectContacts ).ToArray();

            Random random = new Random();
            Contact randomContact = contacts[ random.Next( contacts.Length ) ];

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
        private static async Task TryExecuteSelectedOperationAsync( Contact remoteContact, IApiClient apiClient, ICurrentUserProvider currentUserProvider, ConsoleKey pressedKey )
        {
            while ( true )
            {
                ClientKadOperation kadOperation = new ClientKadOperation( s_discoveryService.ProtocolVersion );

                switch ( pressedKey )
                {
                    case ConsoleKey.NumPad1:
                    case ConsoleKey.D1:
                    {
                        s_discoveryService.QueryAllServices();

                        AutoResetEvent receivedFindNodeRequest = new AutoResetEvent( initialState: false );
                        //because we listen TCP messages in other threads.
                        s_discoveryService.NetworkEventInvoker.FindNodeReceived += ( sender, eventArgs ) => receivedFindNodeRequest.Set();
                        receivedFindNodeRequest.WaitOne();

                        Thread.Sleep( TimeSpan.FromSeconds( value: 2 ) );

                        return;
                    }

                    case ConsoleKey.NumPad2:
                    case ConsoleKey.D2:
                    {
                        kadOperation.Ping( s_discoveryService.NetworkEventInvoker.OurContact, remoteContact );
                        return;
                    }

                    case ConsoleKey.NumPad3:
                    case ConsoleKey.D3:
                    {
                        kadOperation.Store( s_discoveryService.NetworkEventInvoker.OurContact, s_discoveryService.NetworkEventInvoker.OurContact.KadId,
                            s_discoveryService.MachineId, remoteContact );
                        return;
                    }

                    case ConsoleKey.NumPad4:
                    case ConsoleKey.D4:
                    {
                        kadOperation.FindNode( s_discoveryService.NetworkEventInvoker.OurContact, remoteContact.KadId, remoteContact );
                        return;
                    }

                    case ConsoleKey.NumPad5:
                    case ConsoleKey.D5:
                    {
                        kadOperation.FindValue( s_discoveryService.NetworkEventInvoker.OurContact, s_discoveryService.NetworkEventInvoker.OurContact.KadId, remoteContact );
                        return;
                    }

                    case ConsoleKey.NumPad6:
                    case ConsoleKey.D6:
                    {
                        SendTcpMessage( remoteContact );

                        AutoResetEvent receivedTcpMess = new AutoResetEvent( initialState: false );

                        //because we listen TCP messages in other threads
                        s_discoveryService.NetworkEventInvoker.AnswerReceived += ( sender, eventArgs ) => receivedTcpMess.Set();
                        receivedTcpMess.WaitOne();

                        return;
                    }

                    case ConsoleKey.NumPad7:
                    case ConsoleKey.D7:
                    {
                        try
                        {
                            CountAvailableConnectionsTest();
                        }
                        catch ( Exception ex )
                        {
                            Console.WriteLine( ex.ToString() );
                        }

                        return;
                    }

                    case ConsoleKey.NumPad8:
                    case ConsoleKey.D8:
                    {
                        await DownloadRandomFile( apiClient, currentUserProvider, remoteContact ).ConfigureAwait( false );

                        return;
                    }

                    default:
                    {
                        continue;
                    }
                }
            }
        }

        private static void SendTcpMessage( Contact remoteContact )
        {
            IPEndPoint remoteEndPoint = new IPEndPoint( remoteContact.LastActiveIpAddress, remoteContact.TcpPort );
            UdpMessageEventArgs eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = remoteEndPoint
            };

            Random random = new Random();
            UInt32 messageId = (UInt32)random.Next( maxValue: Int32.MaxValue );

            eventArgs.SetMessage( new UdpMessage( messageId, s_discoveryService.ProtocolVersion,
                remoteContact.TcpPort, machineId: s_discoveryService.MachineId ) );
            s_discoveryService.SendTcpMessageAsync( s_discoveryService, eventArgs ).GetAwaiter().GetResult();
        }

        private static void CountAvailableConnectionsTest()
        {
            using ( PowerShell powerShell = PowerShell.Create() )
            {
                WebClient webClient = new WebClient();
                String pathToPowercatScript = "https://raw.githubusercontent.com/besimorhino/powercat/master/powercat.ps1";
                String script = webClient.DownloadString( pathToPowercatScript );

                try
                {
                    powerShell.AddScript( script ).AddScript( "Invoke-Method" ).Invoke();
                }
                catch ( ParseException )
                {
                    throw;
                }
                powerShell.AddCommand( cmdlet: "powercat" );

                Dictionary<String, Object> parameters = PowercatParameters();
                powerShell.AddParameters( parameters );

                Int32 countConnection;
                do
                {
                    Console.Write( $"Input count times you want to send the file: " );
                }
                while ( !Int32.TryParse( Console.ReadLine(), out countConnection ) );

                for ( Int32 numConnection = 1; numConnection <= countConnection; numConnection++ )
                {
                    Console.WriteLine( $"{nameof( numConnection )} = {numConnection}" );

                    powerShell.BeginInvoke();

                    TimeSpan waitToServerRead = TimeSpan.FromSeconds( 0.5 );
                    Thread.Sleep( waitToServerRead );

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
                Console.Write( "Input IP-address of a Discovery Service TCP server to connect: " );

                try
                {
                    serverIpAddress = IPAddress.Parse( Console.ReadLine() );
                }
                catch ( FormatException ex )
                {
                    Console.WriteLine( ex.Message );
                }
            }
            while ( serverIpAddress == null );

            parameters.Add( key: "c", value: serverIpAddress.ToString() );
            parameters.Add( "p", s_discoveryService.RunningTcpPort.ToString() );

            String pathToFileToSend;
            do
            {
                Console.Write( $"Input full path to file which you want to send to server: " );
                pathToFileToSend = Console.ReadLine();
            }
            while ( !File.Exists( pathToFileToSend ) );

            parameters.Add( "i", pathToFileToSend );

            return parameters;
        }

        /// <summary>
        /// Will download random file which isn't in current PC if you aren't only in network. Otherwise it will download file which is
        /// </summary>
        /// <returns></returns>
        private async static Task DownloadRandomFile( IApiClient apiClient,
            ICurrentUserProvider currentUserProvider, Contact remoteContact )
        {
            Download download = new Download( s_discoveryService, IOBehavior.Asynchronous );

            String filePrefix = String.Empty;
            (ObjectDescriptionModel fileDescription, String bucketName, String localFolderPath) = await RandomFileToDownloadAsync( apiClient, currentUserProvider, remoteContact, filePrefix ).ConfigureAwait( false );

            await download.DownloadFileAsync( localFolderPath, bucketName,
                filePrefix, fileDescription.OriginalName, fileDescription.Bytes,
                fileDescription.Version, s_cancellationTokenSource.Token )
                .ConfigureAwait( false );
        }

        private async static Task<(ObjectDescriptionModel randomFileToDownload, String serverBucketName, String localFolderPath)> RandomFileToDownloadAsync( IApiClient apiClient, ICurrentUserProvider currentUserProvider, Contact remoteContact, String filePrefix )
        {
            IList<String> bucketDirectoryPathes = currentUserProvider.ProvideBucketDirectoryPathes();

            Random random = new Random();
            String rndBcktDrctrPth = bucketDirectoryPathes[ random.Next( bucketDirectoryPathes.Count ) ];
            String serverBucketName = currentUserProvider.GetBucketNameByDirectoryPath( rndBcktDrctrPth ).ServerName;

            ObjectsListResponse objectsListResponse = await apiClient.ListAsync( serverBucketName, filePrefix ).ConfigureAwait( continueOnCapturedContext: false );
            ObjectsListModel objectsListModel = objectsListResponse.ToObjectsListModel();
            ObjectDescriptionModel[] undeletedObjectsListModel = objectsListModel.ObjectDescriptions.Where( c => !c.IsDeleted ).ToArray();

            Boolean isOnlyInNetwork = s_discoveryService.ContactId == remoteContact.KadId;

            //select from undeletedObjectsListModel files which exist in current PC if tester is only in network. 
            //If the last one is not, select files which don't exist in current PC
            List<ObjectDescriptionModel> bjctDscrptnsFrDwnld = undeletedObjectsListModel.Where( cachedFileInServer =>
             {
                 String fullPathToFile = Path.Combine( rndBcktDrctrPth, filePrefix, cachedFileInServer.OriginalName );
                 Boolean isFileInCurrentPc = File.Exists( fullPathToFile );

                 Boolean shouldBeDownloaded;
                 if ( isOnlyInNetwork )
                 {
                     shouldBeDownloaded = isFileInCurrentPc;
                 }
                 else
                 {
                     shouldBeDownloaded = !isFileInCurrentPc;
                 }

                 return shouldBeDownloaded;
             } ).ToList();

            ObjectDescriptionModel randomFileToDownload;

            //It will download random file at local PC if you don't have any file to download, 
            //server has any file which isn't deleted and you are only in network
            Boolean canWeDownloadAnything = undeletedObjectsListModel.Length > 0;
            if ( ( bjctDscrptnsFrDwnld.Count == 0 ) && ( canWeDownloadAnything ) && ( isOnlyInNetwork ) )
            {
                randomFileToDownload = undeletedObjectsListModel[ random.Next( undeletedObjectsListModel.Length ) ];
                await apiClient.DownloadFileAsync( serverBucketName, filePrefix, rndBcktDrctrPth, randomFileToDownload.OriginalName, randomFileToDownload ).ConfigureAwait( false );
            }
            else if ( bjctDscrptnsFrDwnld.Count > 0 )
            {
                randomFileToDownload = bjctDscrptnsFrDwnld[ random.Next( bjctDscrptnsFrDwnld.Count ) ];
            }
            else
            {
                Console.WriteLine( $"You should put few files in {rndBcktDrctrPth} and using WpfClient, upload it" );
                throw new InvalidOperationException();
            }

            return (randomFileToDownload, serverBucketName, rndBcktDrctrPth);
        }
    }
}
