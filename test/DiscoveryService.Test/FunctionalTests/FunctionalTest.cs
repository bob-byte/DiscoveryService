using LUC.ApiClient;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Kademlia.ClientPool;
using LUC.DiscoveryServices.Kademlia.Downloads;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Test.Extensions;
using LUC.Interfaces;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;
using LUC.Services.Implementation;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Test.FunctionalTests
{
    partial class FunctionalTest
    {
        private static DiscoveryService s_discoveryService;
        private static readonly SettingsService s_settingsService;
        private static CancellationTokenSource s_cancellationTokenSource;

        static FunctionalTest()
        {
            s_settingsService = new SettingsService();
            s_cancellationTokenSource = new CancellationTokenSource();
        }

        ~FunctionalTest()
        {
            s_discoveryService?.Stop();
            s_cancellationTokenSource.Cancel();
        }

        static async Task Main(String[] args)
        {
            String containerId = String.Empty;
            String argName = "-containerId";

            if (args.Length > 0)
            {
                if ( ( args.Length == 2 ) && ( args[ 0 ] == argName ) && ( !String.IsNullOrWhiteSpace( args[ 1 ] ) ) )
                {
                    StringBuilder builderContainerId = new StringBuilder( argName );
                    builderContainerId.Append( args[ 1 ] );

                    containerId = builderContainerId.ToString();
                }
                else
                {
                    Console.WriteLine($"Only 1 command line argument is available: {argName}");
                    return;
                }
            }

            Console.CancelKeyPress += StopDs;

            String warning = Display.StringWithAttention( "Before test DS(Discovery Service) with container you should run DS.Test.ext without them or \n" +
                "set DS.Test/bin/integrationTests/DownloadTest/{anyname} as LUC root folder and\n" +
                "put there all directories (files isn't required) from previous LUC root folder.\n" +
                "If you did that you can test with all options, if not, you can't normally observe changes in root folder and\n" +
                "set bytes of files which another contacts want to download" );
            Console.WriteLine( warning );

            ApiClient.ApiClient apiClient;

            String login = "integration1";
            String password = "integration1";

            LoginResponse loginResponse = null;
            do
            {
                (apiClient, loginResponse, s_settingsService.CurrentUserProvider) = await SetUpTests.LoginAsync( login, password ).ConfigureAwait( continueOnCapturedContext: false );

                if ( !loginResponse.IsSuccess )
                {
                    Console.WriteLine( "Check your connection to Internet, because you cannot login\n" +
                        "If you did that, press any keyboard key to try login again" );

                    //intercept: true says that pressed key won't be shown in console
                    Console.ReadKey( intercept: true );
                }
            }
            while ( !loginResponse.IsSuccess );

            SetUpTests.CurrentUserProvider = s_settingsService.CurrentUserProvider;
            ConcurrentDictionary<String, String> bucketsSupported = new ConcurrentDictionary<String, String>();

            String fileNameWithMachineId = $"{Constants.FILE_WITH_MACHINE_ID}{containerId}{Constants.FILE_WITH_MACHINE_ID_EXTENSION}";

            Boolean deleteMachineId = UserIntersectionInConsole.NormalResposeFromUserAtClosedQuestion( closedQuestion: $"Do you want to update Machine ID (it is always needed if you run firstly container)?");
            if ( deleteMachineId )
            {
                File.Delete( fileNameWithMachineId );
            }

            MachineId.Create( fileNameWithMachineId, out String machineId );

            Boolean wantToObserveChageInDownloadTestFolder = UserIntersectionInConsole.NormalResposeFromUserAtClosedQuestion( $"Do you want to observe changes in {Constants.DOWNLOAD_TEST_NAME_FOLDER} folder" );

            if ( wantToObserveChageInDownloadTestFolder )
            {
                InitWatcherForIntegrationTests( apiClient, machineId );
            }
            else
            {
                UpdateLucRootFolder( apiClient, machineId, newLucFullFolderName: out _ );
            }

            DsBucketsSupported.Define( s_settingsService.CurrentUserProvider, out bucketsSupported );

            s_discoveryService = new DiscoveryService( new ServiceProfile( machineId, useIpv4: true, useIpv6: true, protocolVersion: 1, bucketsSupported ), s_settingsService.CurrentUserProvider );
            Console.WriteLine($"Your machine Id: {s_discoveryService.MachineId}");

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

            Boolean isCountConnectionsAvailableTest = UserIntersectionInConsole.NormalResposeFromUserAtClosedQuestion( "Do you want to test count available connections?" );

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
                        if ( ( isFirstTest ) || ( contact == null ) )
                        {
                            GetContact( s_discoveryService, out contact );
                        }
                        else
                        {
                            Boolean whetherFindAnyContacts = UserIntersectionInConsole.NormalResposeFromUserAtClosedQuestion( "Do you want to find any new contacts?" );

                            if ( whetherFindAnyContacts )
                            {
                                GetContact( s_discoveryService, out contact );
                            }
                        }

                        lock(UserIntersectionInConsole.Lock)
                        {
                            ShowAvailableUserOptions();
                            pressedKey = Console.ReadKey().Key;
                        }
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

        private static void StopDs(Object sender, ConsoleCancelEventArgs eventArgs)
        {
            s_discoveryService?.Stop();
        }

        private static void OnGoodTcpMessage( Object sender, TcpMessageEventArgs e )
        {
            lock ( UserIntersectionInConsole.Lock )
            {
                Console.WriteLine( "=== TCP {0:O} ===", DateTime.Now );

                AcknowledgeTcpMessage tcpMessage = e.Message<AcknowledgeTcpMessage>( whetherReadMessage: false );
                Console.WriteLine( tcpMessage.ToString() );
            }
        }

        private static void OnGoodUdpMessage( Object sender, UdpMessageEventArgs e )
        {
            lock ( UserIntersectionInConsole.Lock )
            {
                Console.WriteLine( "=== UDP {0:O} ===", DateTime.Now );

                UdpMessage message = e.Message<UdpMessage>( whetherReadMessage: false );
                Console.WriteLine( message.ToString() );
            }
        }

        private static void OnPingReceived( Object sender, TcpMessageEventArgs e )
        {
            lock ( UserIntersectionInConsole.Lock )
            {
                Console.WriteLine( "=== Kad PING received {0:O} ===", DateTime.Now );

                PingRequest message = e.Message<PingRequest>( whetherReadMessage: false );
                Console.WriteLine( message.ToString() );
            }
        }

        private static void OnStoreReceived( Object sender, TcpMessageEventArgs e )
        {
            lock ( UserIntersectionInConsole.Lock )
            {
                Console.WriteLine( "=== Kad STORE received {0:O} ===", DateTime.Now );

                StoreRequest message = e.Message<StoreRequest>( whetherReadMessage: false );
                Console.WriteLine( message.ToString() );
            }
        }

        private static void OnFindNodeReceived( Object sender, TcpMessageEventArgs e )
        {
            lock ( UserIntersectionInConsole.Lock )
            {
                Console.WriteLine( "=== Kad FindNode received {0:O} ===", DateTime.Now );

                FindNodeRequest message = e.Message<FindNodeRequest>( whetherReadMessage: false );
                Console.WriteLine( message.ToString() );
            }
        }

        private static void OnFindValueReceived( Object sender, TcpMessageEventArgs e )
        {
            lock ( UserIntersectionInConsole.Lock )
            {
                Console.WriteLine( "=== Kad FindValue received {0:O} ===", DateTime.Now );

                FindValueRequest message = e.Message<FindValueRequest>( whetherReadMessage: false );
                Console.WriteLine( message.ToString() );
            }
        }

        private static void ShowAvailableUserOptions() =>
            Console.WriteLine( 
                $"Select an operation:\n" +
                $"1 - send multicast\n" +
                $"2 - send {typeof( PingRequest ).Name}\n" +
                $"3 - send {typeof( StoreRequest ).Name}\n" +
                $"4 - send {typeof( FindNodeRequest ).Name}\n" +
                $"5 - send {typeof( FindValueRequest ).Name}\n" +
                $"6 - send {typeof( AcknowledgeTcpMessage ).Name}\n" +
                $"7 - test count available connections\n" +
                $"8 - download random file from another contact(-s)\n" +
                $"9 - create file with random bytes"
            );

        private static void GetContact( DiscoveryService discoveryService, out Contact contact )
        {
            contact = null;

            while ( contact == null )
            {
                discoveryService.QueryAllServices();//to get any contacts
                AutoResetEvent receivedFindNodeRequest = new AutoResetEvent( initialState: false );

                discoveryService.NetworkEventInvoker.FindNodeReceived += ( sender, eventArgs ) => receivedFindNodeRequest.Set();

                TimeSpan timeExecutionKadOp = Constants.TimeWaitSocketReturnedToPool;
                receivedFindNodeRequest.WaitOne(timeExecutionKadOp);

                //wait again, because contact should be added to NetworkEventInvoker.Dht.Node.BucketList after Find Node Kademlia operation
                Thread.Sleep( TimeSpan.FromSeconds( value: 5 ) );
                try
                {
                    contact = RandomContact( discoveryService );
                }
                catch ( ArgumentOutOfRangeException ) //if knowContacts.Count == 0
                {
                    ;//do nothing
                }
            }
        }

        private static Contact RandomContact( DiscoveryService discoveryService )
        {
            List<Contact> contacts = discoveryService.OnlineContacts();

            Random random = new Random();
            Contact randomContact = contacts[ random.Next( maxValue: contacts.Count ) ];

            return randomContact;
        }

        /// <summary>
        /// Try execute selected operation while key is invalid
        /// </summary>
        private static async Task TryExecuteSelectedOperationAsync( Contact remoteContact, IApiClient apiClient, ICurrentUserProvider currentUserProvider, ConsoleKey pressedKey )
        {
            while ( true )
            {
                ClientKadOperation kadOperation = new ClientKadOperation( s_discoveryService.ProtocolVersion );
                TimeSpan timeExecutionKadOp = Constants.TimeWaitSocketReturnedToPool;

                switch ( pressedKey )
                {
                    case ConsoleKey.NumPad1:
                    case ConsoleKey.D1:
                    {
                        s_discoveryService.QueryAllServices();

                        AutoResetEvent receivedFindNodeRequest = new AutoResetEvent( initialState: false );

                        //because we listen TCP messages in other threads.
                        s_discoveryService.NetworkEventInvoker.AnswerReceived += ( sender, eventArgs ) => receivedFindNodeRequest.Set();
                        receivedFindNodeRequest.WaitOne(timeExecutionKadOp);

                        //because we receive response from different contacts and IP-addresses
                        Thread.Sleep( TimeSpan.FromSeconds( value: 5 ) );

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

                        s_discoveryService.NetworkEventInvoker.AnswerReceived += ( sender, eventArgs ) => receivedTcpMess.Set();

                        //After revecing AcknowledgeTcpMessage we will receive FindNodeResponse
                        receivedTcpMess.WaitOne( (Int32)timeExecutionKadOp.TotalMilliseconds * 2 );

                        await Task.Delay( TimeSpan.FromSeconds( value: 1.5 ) );

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
                        await DownloadRandomFileAsync( apiClient, currentUserProvider, remoteContact ).ConfigureAwait( false );

                        return;
                    }

                    case ConsoleKey.NumPad9:
                    case ConsoleKey.D9:
                    {
                        CreateFileWithRndBytes();

                        //wait while FileSystemWatcher is recognising file 
                        await Task.Delay( TimeSpan.FromSeconds( 2 ) );

                        return;
                    }

                        default:
                    {
                        Console.WriteLine("Inputted wrong key");
                        return;
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

        
    }
}
