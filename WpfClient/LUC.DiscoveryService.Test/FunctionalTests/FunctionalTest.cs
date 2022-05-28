using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Extensions;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;
using LUC.Services.Implementation;
using LUC.Services.Implementation.Helpers;

using Nito.AsyncEx.Synchronous;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Unity;

namespace LUC.DiscoveryServices.Test.FunctionalTests
{
    partial class FunctionalTest
    {
        private static DiscoveryService s_discoveryService;
        private static ISettingsService s_settingsService;
        private static CancellationTokenSource s_cancellationTokenSource;

        static FunctionalTest()
        {
            s_cancellationTokenSource = new CancellationTokenSource();

            //call static constructor of DsSetUpTests class
            _ = new DsSetUpTests();
        }

        ~FunctionalTest()
        {
            s_discoveryService?.Stop();
            s_cancellationTokenSource.Cancel();
        }

        static async Task Main( String[] args )
        {
            SentryHelper.InitAppSentry();

            String containerId = String.Empty;
            String argName = "-containerId";

            if ( args.Length > 0 )
            {
                if ( ( args.Length == 2 ) && ( args[ 0 ] == argName ) && ( !String.IsNullOrWhiteSpace( args[ 1 ] ) ) )
                {
                    var builderContainerId = new StringBuilder( argName );
                    builderContainerId.Append( args[ 1 ] );

                    containerId = builderContainerId.ToString();
                }
                else
                {
                    Console.WriteLine( $"Only 1 command line argument is available: {argName}" );
                    return;
                }
            }

            Console.CancelKeyPress += StopDs;

            String warning = "Before test DS(Discovery Service) with container you should run DS.Test.exe without them or \n" +
                "set DS.Test/bin/integrationTests/DownloadTest/{anyname} as LUC root folder and\n" +
                "put there all directories (files isn't required) from previous LUC root folder.\n" +
                "If you did that you can test with all options, if not, you can't normally observe changes in root folder and\n" +
                "set bytes of files which another contacts want to download";
            Console.WriteLine( warning.WithAttention() );

            s_settingsService = DsSetUpTests.SettingsService;
            IApiClient apiClient = DsSetUpTests.ApiClient;
            ICurrentUserProvider currentUserProvider = DsSetUpTests.CurrentUserProvider;
            ILoggingService loggingService = DsSetUpTests.LoggingService;

            String fileNameWithMachineId = $"{DsConstants.FILE_WITH_MACHINE_ID}{containerId}{DsConstants.FILE_WITH_MACHINE_ID_EXTENSION}";

            Boolean deleteMachineId = UserIntersectionInConsole.NormalResposeFromUserAtClosedQuestion( closedQuestion: $"Do you want to update Machine ID (it is always needed if you run firstly container)?" );
            if ( deleteMachineId )
            {
                File.Delete( fileNameWithMachineId );
            }

            String machineId = MachineId.Create( fileNameWithMachineId );
            s_settingsService.WriteMachineId( machineId );

            s_discoveryService = DiscoveryServiceFacade.InitWithoutForceToStart( currentUserProvider, s_settingsService ) as DiscoveryService;
            DsSetUpTests.UnityContainer.RegisterInstance<IDiscoveryService>( s_discoveryService );

            Console.WriteLine( $"Your machine Id: {s_discoveryService.MachineId}" );

            LoginResponse loginResponse = null;
            String login = "integration1";
            String password = "integration1";

            do
            {
                loginResponse = await apiClient.LoginAsync( login, password ).ConfigureAwait(continueOnCapturedContext: false);

                if ( loginResponse.IsSuccess )
                {
                    String loggedUserAsStr = currentUserProvider.LoggedUser.ToString(String.Empty);
                    loggingService.LogInfo( loggedUserAsStr );
                }
                else
                {
                    Console.WriteLine( "Check your connection to Internet, because you cannot login\n" +
                        "If you did that, press any keyboard key to try login again" );

                    //intercept: true says that pressed key won't be shown in console
                    Console.ReadKey( intercept: true );
                }
            }
            while ( !loginResponse.IsSuccess );

            s_discoveryService.Start();
            s_discoveryService.NetworkEventInvoker.NetworkInterfaceDiscovered += ( s, e ) =>
            {
                foreach ( System.Net.NetworkInformation.NetworkInterface nic in e.NetworkInterfaces )
                {
                    Console.WriteLine( $"discovered NIC '{nic.Name}'" );
                }
            };

            await s_discoveryService.NetworkEventInvoker.WaitHandleAllTcpEvents.WaitAsync(/*DsConstants.TimeWaitSocketReturnedToPool*/).ConfigureAwait( false );

            Boolean wantToObserveChageInDownloadTestFolder = UserIntersectionInConsole.NormalResposeFromUserAtClosedQuestion( $"Do you want to observe changes in {DsConstants.DOWNLOAD_TEST_NAME_FOLDER} folder" );

            if ( wantToObserveChageInDownloadTestFolder )
            {
                InitWatcherForIntegrationTests( apiClient, currentUserProvider, machineId );
            }
            else
            {
                UpdateLucRootFolder( machineId, currentUserProvider, newLucFullFolderName: out _ );
            }           

            Boolean isCountConnectionsAvailableTest = UserIntersectionInConsole.NormalResposeFromUserAtClosedQuestion( "Do you want to test count available connections?" );

            IContact contact = null;
            Boolean isFirstTest = true;
            while ( true )
            {
                try
                {
                    ConsoleKey pressedKey;

                    if ( isCountConnectionsAvailableTest && isFirstTest )
                    {
                        pressedKey = ConsoleKey.D7;
                    }
                    else
                    {
                        if ( isFirstTest )
                        {
                            try
                            {
                                contact = RandomContact( s_discoveryService );
                            }
                            catch ( ArgumentOutOfRangeException ) //if knowContacts.Count == 0
                            {
                                ;//do nothing
                            }

                            if ( contact == null )
                            {
                                GetContact( s_discoveryService, out contact );
                            }

                            await s_discoveryService.NetworkEventInvoker.WaitHandleAllTcpEvents.WaitAsync().ConfigureAwait( false );
                        }
                        else
                        {
                            await s_discoveryService.NetworkEventInvoker.WaitHandleAllTcpEvents.WaitAsync().ConfigureAwait( false );

                            Boolean whetherFindAnyContacts = UserIntersectionInConsole.NormalResposeFromUserAtClosedQuestion( "Do you want to find any new contacts?" );

                            if ( whetherFindAnyContacts )
                            {
                                GetContact( s_discoveryService, out contact );
                            }
                        }

                        lock ( UserIntersectionInConsole.Lock )
                        {
                            ShowAvailableUserOptions(contact);
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

        private static void StopDs( Object sender, ConsoleCancelEventArgs eventArgs ) => s_discoveryService?.Stop();

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

                MulticastMessage message = e.Message<MulticastMessage>( whetherReadMessage: false );
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

        private static void ShowAvailableUserOptions(IContact contact = null)
        {
            if(contact == null)
            {
                Console.WriteLine("You didn\'t find any contact, so some options is not available");
            }

            String options = $"Select an operation:\n" +
                $"1 - send multicast\n";
            if(contact != null)
            {
                options += $"2 - send {typeof( PingRequest ).Name}\n" +
                    $"3 - send {typeof( StoreRequest ).Name}\n" +
                    $"4 - send {typeof( FindNodeRequest ).Name}\n" +
                    $"5 - send {typeof( FindValueRequest ).Name}\n" +
                    $"6 - send {typeof( AcknowledgeTcpMessage ).Name}\n";
            }

            options += $"7 - test count available connections\n" +
                $"8 - download random file from another contact(-s)\n" +
                $"9 - create file with random bytes";
            Console.WriteLine( options );
        }

        private static void GetContact( DiscoveryService discoveryService, out IContact contact )
        {
            contact = null;

            while ( contact == null )
            {
                discoveryService.TryFindAllNodes();//to get any contacts

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

            //s_discoveryService.NetworkEventInvoker.WaitHandleAllTcpEvents.Wait();
            //var receivedFindNodeRequest = new AutoResetEvent( initialState: false );
            //discoveryService.NetworkEventInvoker.FindNodeReceived += ( sender, eventArgs ) => receivedFindNodeRequest.Set();

            ////copy this value, because it can be changed
            //Int32 ipAddressesCount = contact.IpAddressesCount;
            //TimeSpan timeExecutionKadOp = DsConstants.TimeWaitSocketReturnedToPool;

            //for ( Int32 numIp = 0; numIp < ipAddressesCount; numIp++ )
            //{
            //    receivedFindNodeRequest.WaitOne( timeExecutionKadOp );
            //}

            ////wait again, because contact should be added to NetworkEventInvoker.Dht.Node.BucketList after Find Node Kademlia operation
            //Thread.Sleep( TimeSpan.FromSeconds( 5 ) );
        }

        private static IContact RandomContact( DiscoveryService discoveryService )
        {
            List<IContact> contacts = discoveryService.OnlineContacts();

            var random = new Random();
            IContact randomContact = contacts[ random.Next( maxValue: contacts.Count ) ];

            return randomContact;
        }

        /// <summary>
        /// Try execute selected operation while key is invalid
        /// </summary>
        private static async Task TryExecuteSelectedOperationAsync( IContact remoteContact, IApiClient apiClient, ICurrentUserProvider currentUserProvider, ConsoleKey pressedKey )
        {
            while ( true )
            {
                var kadOperation = new KadLocalTcp( s_discoveryService.ProtocolVersion );
                TimeSpan timeExecutionKadOp = DsConstants.TimeWaitSocketReturnedToPool;

                switch ( pressedKey )
                {
                    case ConsoleKey.NumPad1:
                    case ConsoleKey.D1:
                    {
                        s_discoveryService.TryFindAllNodes();

                        var receivedFindNodeRequest = new AutoResetEvent( initialState: false );

                        //because we listen TCP messages in other threads.
                        s_discoveryService.NetworkEventInvoker.AnswerReceived += ( sender, eventArgs ) => receivedFindNodeRequest.Set();
                        receivedFindNodeRequest.WaitOne( timeExecutionKadOp );

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

                        var receivedTcpMess = new AutoResetEvent( initialState: false );

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
                        await DownloadRandomFileAsync( apiClient, currentUserProvider ).ConfigureAwait( false );

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
                        Console.WriteLine( "Inputted wrong key" );
                        return;
                    }
                }
            }
        }

        private static void SendTcpMessage( IContact remoteContact )
        {
            var remoteEndPoint = new IPEndPoint( remoteContact.LastActiveIpAddress, remoteContact.TcpPort );
            var eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = remoteEndPoint
            };

            var random = new Random();
            UInt32 messageId = (UInt32)random.Next( maxValue: Int32.MaxValue );

            eventArgs.SetMessage( new MulticastMessage( messageId, s_discoveryService.ProtocolVersion,
                remoteContact.TcpPort, machineId: s_discoveryService.MachineId ) );
            s_discoveryService.SendAcknowledgeTcpMessageAsync( eventArgs, IoBehavior.Synchronous ).GetAwaiter().GetResult();
        }

        private static void CountAvailableConnectionsTest()
        {
            using ( var powerShell = PowerShell.Create() )
            {
                String script;
                using (var webClient = new WebClient())
                {
                    String pathToPowercatScript = "https://raw.githubusercontent.com/besimorhino/powercat/master/powercat.ps1";
                    script = webClient.DownloadString(pathToPowercatScript);
                }

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

                    var waitToServerRead = TimeSpan.FromSeconds( 0.5 );
                    Thread.Sleep( waitToServerRead );

                    powerShell.Stop();
                }
            }
        }

        private static Dictionary<String, Object> PowercatParameters()
        {
            var parameters = new Dictionary<String, Object>();

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
