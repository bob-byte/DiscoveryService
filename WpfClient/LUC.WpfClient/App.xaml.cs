using AutoUpdaterDotNET;

using LUC.DiscoveryServices;
using LUC.Services.Implementation;

using Serilog;

using System;
using System.ComponentModel.Composition;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using LUC.Services.Implementation.Helpers;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces;
using LUC.Interfaces.Models;
using LUC.Interfaces.Extensions;

namespace LUC.WpfClient
{
    [Export]
    public partial class App : Application
    {
        #region Constants

        private const String UNIQUE_MUTEX_NAME = "2F395EBE-36D7-48DA-81AB-4ECC58641D9F";

        #endregion

        #region Fields

        private Mutex m_mutex;
        private ILoggingService m_loggingService;

        #endregion

        #region Methods

        private void Application_Startup( Object sender, StartupEventArgs e )
        {
            m_mutex = new Mutex( true, UNIQUE_MUTEX_NAME, out Boolean isOwned );

            // So, R# would not give a warning that this variable is not used.
            GC.KeepAlive( m_mutex );

            var splashScreen = new SplashScreen( "Views/dubstack_splash.png" );

            if ( isOwned )
            {
                String username = "rr@lightupon.cloud";
                String password = "blaH1234";
                String updateUrl = ConfigurationManager.AppSettings[ "UpdateUrl" ];
                var basicAuthentication = new BasicAuthentication( username, password );

                AutoUpdater.DownloadPath = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
                AutoUpdater.BasicAuthXML = basicAuthentication;
                AutoUpdater.BasicAuthDownload = basicAuthentication;
                AutoUpdater.RunUpdateAsAdmin = true;
                AutoUpdater.ShowSkipButton = true;
                AutoUpdater.Mandatory = true;
                AutoUpdater.UpdateMode = Mode.ForcedDownload;
                AutoUpdater.HttpUserAgent = "AutoUpdater";
                AutoUpdater.ReportErrors = true;
                AutoUpdater.AppTitle = "Light Upon Cloud Update";
                AutoUpdater.AppCastURL = updateUrl;
                AutoUpdater.CheckForUpdateEvent += AutoUpdaterCheckForUpdateEvent;
                AutoUpdater.Start( updateUrl ); // TODO Add logic to check updates each day.

                splashScreen.Show( false, false );
            }
            else
            {
                // Terminate this instance.
                Shutdown();
            }

            try
            {
                var bootstrapper = new CustomBootstrapper();
                bootstrapper.Run();

                m_loggingService = new LoggingService
                {
                    SettingsService = AppSettings.ExportedValue<ISettingsService>(),
                    NotifyService = AppSettings.ExportedValue<INotifyService>()
                };

                ConfigureFirewall();
            }
            catch ( ReflectionTypeLoadException ex )
            {
                var sb = new StringBuilder();
                foreach ( Exception exSub in ex.LoaderExceptions )
                {
                    _ = sb.AppendLine( exSub.Message );
                    if ( exSub is FileNotFoundException exFileNotFound && !String.IsNullOrEmpty( exFileNotFound.FusionLog ) )
                    {
                        _ = sb.AppendLine( "Fusion Log:" );
                        _ = sb.AppendLine( exFileNotFound.FusionLog );
                    }

                    _ = sb.AppendLine();
                }

                String errorMessage = sb.ToString();
                _ = MessageBox.Show( errorMessage );
                Log.Error( errorMessage );
            }
            catch ( Exception ex )
            {
                _ = MessageBox.Show( ex.Message );
                Log.Error( ex.Message );
            }
            finally
            {
                splashScreen.Close( TimeSpan.FromMilliseconds( 0 ) );
            }
        }

        private void ConfigureFirewall()
        {
            var entryAssembly = Assembly.GetEntryAssembly();

            String pathToExeFile = entryAssembly.Location;

            String appVersion = entryAssembly.GetName().Version.ToString();
            String appName = $"Light Upon Cloud {appVersion}";

            try
            {
                FirewallHelper firewallHelper = FirewallHelper.Instance;

                firewallHelper.GrantAppAuthInAnyNetworksInAllPorts( pathToExeFile, appName );
                m_loggingService.LogInfo( logRecord: $"{appName} successfully granted in private networks" );
            }
            catch ( Exception ex )
            {
                m_loggingService.LogCriticalError( message: $"{appName} cannot be granted in private networks", ex );

                MessageBox.Show( ex.ToString() );
                Shutdown( exitCode: -1 );
            }
        }

        private void AutoUpdaterCheckForUpdateEvent( UpdateInfoEventArgs args )
        {
            if ( args != null )
            {
                if ( args.IsUpdateAvailable )
                {
                    try
                    {
                        if ( AutoUpdater.DownloadUpdate( args ) )
                        {
                            Current.Shutdown();
                        }
                        else
                        {
                            // Attempt #2
                            if ( AutoUpdater.DownloadUpdate( args ) )
                            {
                                Current.Shutdown();
                            }
                            else
                            {
                                // Attempt #3
                                if ( AutoUpdater.DownloadUpdate( args ) )
                                {
                                    Current.Shutdown();
                                }
                                else
                                {
                                    // Attempt #4
                                    if ( AutoUpdater.DownloadUpdate( args ) )
                                    {
                                        Current.Shutdown();
                                    }
                                    else
                                    {
                                        // TODO Could not work from first time. Investigate.
                                        Log.Error( "Can't download update for 4 attempts." );
                                    }
                                }
                            }
                        }
                    }
                    catch ( Exception exception )
                    {
                        _ = MessageBox.Show( exception.Message );
                        File.WriteAllText( "update.exception.log", exception.Message );
                    }
                }
            }
            else
            {
                Log.Error( "Cant update somewhy..." );
            }
        }

        private void Application_DispatcherUnhandledException( Object sender, DispatcherUnhandledExceptionEventArgs e )
        {
            _ = MessageBox.Show( e.Exception.Message );
            _ = MessageBox.Show( e.Exception.StackTrace );
            Log.Error( e.Exception.Message );
            Log.Error( e.Exception.StackTrace );
            Current.Shutdown();
        }

        // TODO Release 2.0 change sync folder try set default path
        private void Application_Exit( Object sender, ExitEventArgs e )
        {
            String localAppData = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );

            String logsDirectory = Path.Combine( localAppData, "LightUponCloudLogs" );

            if ( Directory.Exists( logsDirectory ) )
            {
                System.Collections.Generic.IEnumerable<String> logFiles = Directory.EnumerateFiles( logsDirectory );

                foreach ( String path in logFiles )
                {
                    if ( ( DateTime.UtcNow - new FileInfo( path ).LastWriteTimeUtc ).TotalHours > 72 )
                    {
                        try
                        {
                            File.Delete( path );
                        }
                        catch ( Exception )
                        {
                            m_loggingService.LogInfo( $"Old log files can`t delete" );
                        }
                    }
                }
            }

            try
            {
                //ServiceDiscovery.Start() is in DefaultServicesFactory.DiscoveryService (DiscoveryService assembly)
                var discoveryService = DiscoveryService.BeforeCreatedInstance( GeneralConstants.PROTOCOL_VERSION );
                discoveryService.Stop();

                //TODO: Set large fields to null here...
            }
            catch ( ArgumentException )
            {
                ;//do nothing
            }

            m_loggingService.LogInfo( $"Application exit with code {e.ApplicationExitCode}" );
        }

        #endregion
    }
}
