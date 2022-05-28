using Avalon.Windows.Dialogs;

using LUC.Common.PrismEvents;
using LUC.Interfaces;
using LUC.Interfaces.Abstract;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Models;

using Nito.AsyncEx.Synchronous;

using Prism.Commands;
using Prism.Events;
using Prism.Regions;

using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LUC.ViewModels
{
    //TODO: delete using WaitAndUnwrapException
    [Export]
    public class DesktopViewModel : WebRestorable, INavigationAware, INotifyPropertyChanged
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Fields

#if DEBUG
        private static Boolean s_isRunOptionToCancelDownload;

        private static readonly Object s_lock;

        private static CancellationTokenSource s_cancelOptionToStopDownload;
#endif

        private Boolean m_isChangeFolderDialogShowing = false;

        // Imported from constructor for immidiate subscribing to events.
        private ICurrentUserProvider CurrentUserProvider { get; set; }

        [Import( typeof( IBackgroundSynchronizer ) )]
        private IBackgroundSynchronizer BackgroundSynchronizer { get; set; }

        [Import( typeof( IFileSystemFacade ) )]
        private IFileSystemFacade FileSystemFacade { get; set; }

        [Import( typeof( ISettingsService ) )]
        private ISettingsService SettingsService { get; set; }

        [Import( typeof( INotifyService ) )]
        private INotifyService NotifyService { get; set; }

        [Import( typeof( INavigationManager ) )]
        private INavigationManager NavigationManager { get; set; }

        [Import( typeof( ILoggingService ) )]
        private ILoggingService LoggingService { get; set; }

#if DEBUG
        static DesktopViewModel()
        {
            s_lock = new Object();

            s_cancelOptionToStopDownload = new CancellationTokenSource();
            s_isRunOptionToCancelDownload = false;
        }
#endif

        #endregion

        [ImportingConstructor]
        public DesktopViewModel( ICurrentUserProvider currentUserProvider, IEventAggregator eventAggregator )
        {
            CurrentUserProvider = currentUserProvider;
            CurrentUserProvider.RootFolderPathChanged += CurrentUserProviderRootFolderPathChanged;

            _ = eventAggregator.GetEvent<RequestChangeFolderForMonitoringEvent>().Subscribe( ( Boolean param ) => ChangeFolderForMonitoringCommand.Execute() );
        }

#if DEBUG
        ~DesktopViewModel()
        {
            s_cancelOptionToStopDownload.Cancel();
            s_cancelOptionToStopDownload.Dispose();
        }
#endif

        protected void OnPropertyChanged( String name ) => PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( name ) );

        private void CurrentUserProviderRootFolderPathChanged( Object sender, RootFolderPathChangedEventArgs eventArgs )
        {
            if ( RootFolderPath == null )
            {
                ExecuteChangeFolderForMonitoringCommand();
            }

            if ( eventArgs.NewRootFolder is null || !eventArgs.NewRootFolder.Equals( eventArgs.OldRootFolder, StringComparison.Ordinal ) )
            {
                try
                {
                    SettingsService.WriteUserRootFolderPath( eventArgs.NewRootFolder );
                }
                catch ( InvalidOperationException )
                {
                    ;//do nothing
                }

                OnPropertyChanged( nameof( RootFolderPath ) );
            }

#if DEBUG
            if ( !s_isRunOptionToCancelDownload )
            {
                lock ( s_lock )
                {
                    if ( !s_isRunOptionToCancelDownload )
                    {
                        RunOptionToCancelDownload();
                    }
                }
            }
#endif
        }

        #region Properties

        private DelegateCommand m_changeFolderForMonitoringCommand;
        public DelegateCommand ChangeFolderForMonitoringCommand
        {
            get
            {
                if ( m_changeFolderForMonitoringCommand == null )
                {
                    m_changeFolderForMonitoringCommand = new DelegateCommand( ExecuteChangeFolderForMonitoringCommand );
                }

                return m_changeFolderForMonitoringCommand;
            }
        }

        private DelegateCommand m_selectFoldersForIgnore;
        public DelegateCommand SelectFoldersForIgnore
        {
            get
            {
                if ( m_selectFoldersForIgnore == null )
                {
                    m_selectFoldersForIgnore = new DelegateCommand( () => NavigationManager.NavigateToSelectFoldersForIgnoreView() );
                }

                return m_selectFoldersForIgnore;
            }
        }

        public String RootFolderPath => CurrentUserProvider.RootFolderPath;

        protected override Action StopOperation => BackgroundSynchronizer.StopAllSync;

        protected override Action RerunOperation => BackgroundSynchronizer.RunAsync().WaitAndUnwrapException;

        #endregion

        public Boolean IsNavigationTarget( NavigationContext navigationContext ) => true;

        public void OnNavigatedFrom( NavigationContext navigationContext ) => StopAll();

        public async void OnNavigatedTo( NavigationContext navigationContext )
        {
            if ( RootFolderPath == null )
            {
                ExecuteChangeFolderForMonitoringCommand();
                throw new ArgumentNullException( String.Empty, nameof( RootFolderPath ) );
            }

            OnPropertyChanged( nameof( RootFolderPath ) );

            if ( CurrentUserProvider.IsLoggedIn )
            {
                try
                {
                    FileSystemFacade.RunMonitoring(); // It should be run earlier than BackgroundSynchronizer.SyncToServer

                    await BackgroundSynchronizer.RunAsync();
                }
                catch ( Exception exception )
                {
                    StopAll();

                    String understandable = exception.Message;
                    String understandable2 = exception.Message;
                    LoggingService.LogError( understandable );
                    LoggingService.LogError( understandable2 );
                    LoggingService.LogError( exception.Message );
                    LoggingService.LogError( exception.StackTrace );
                    LoggingService.LogError( exception, "Not network exception." );

                    //TODO: remove next 2 rows and test change result
                    FileSystemFacade.StopMonitoring();
                    BackgroundSynchronizer.StopAllSync();
                }
            }
        }

        private void StopAll()
        {
            BackgroundSynchronizer.StopAllSync();
            FileSystemFacade.StopMonitoring();
        }

        private void ExecuteChangeFolderForMonitoringCommand()
        {
            if ( m_isChangeFolderDialogShowing )
            {
                return;
            }

            m_isChangeFolderDialogShowing = true;

            // TODO Release 2.0 Title of dialog. Try another dialog. Like https://www.nuget.org/packages/Ookii.Dialogs.Wpf/
            var dialog = new FolderBrowserDialog
            {
                BrowseFiles = false,
                BrowseShares = false,
                Title = "Please select sync folder",
                ShowStatusText = false
            };

            if ( dialog.ShowDialog( Application.Current.MainWindow ) == true )
            {
                String driveLetter = Path.GetPathRoot( dialog.SelectedPath );

                if ( new DriveInfo( driveLetter ).DriveFormat == GeneralConstants.NTFS_FILE_SYSTEM )
                {
                    // stop sync by old path
                    StopAll();

                    // remember new path
                    CurrentUserProvider.RootFolderPath = dialog.SelectedPath;
                    OnPropertyChanged( nameof( RootFolderPath ) );

                    // start sync by new path
                    FileSystemFacade.RunMonitoring();
                    BackgroundSynchronizer.RunPeriodicSyncFromServer();
                }
                else
                {
                    NotifyService.NotifyInfo( $"Please select drive with {GeneralConstants.NTFS_FILE_SYSTEM} file system." );
                }
            }
            else
            {
                // do nothing. User did not selected any new path.
            }

            m_isChangeFolderDialogShowing = false;
        }

#if DEBUG
        private void RunOptionToCancelDownload()
        {
            Task.Factory.StartNew( () =>
            {
                try
                {
                    while ( !s_cancelOptionToStopDownload.IsCancellationRequested )
                    {
                        LoggingService.LogInfo( "Press C key to cancel download already started files" );

                        //intercept: true to not show pressed key
                        ConsoleKey pressedKey = Console.ReadKey( intercept: true ).Key;
                        if ( pressedKey == ConsoleKey.C )
                        {
                            if ( RootFolderPath != null )
                            {
                                FileSystemFacade?.SyncingObjectsList?.CancelDownloadingAllFilesWhichBelongPath( RootFolderPath );
                            }
                            else
                            {
                                LoggingService.LogInfo( $"{nameof( RootFolderPath )} is null, so it is not available to cancel download" );
                            }
                        }
                    }
                }
                //console is not run
                catch ( InvalidOperationException )
                {
                    ;//do nothing
                }
            } ).ConfigureAwait( continueOnCapturedContext: false );

            s_isRunOptionToCancelDownload = true;
        }
#endif
    }
}
