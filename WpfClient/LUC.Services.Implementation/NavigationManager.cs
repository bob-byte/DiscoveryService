using Avalon.Windows.Dialogs;

using LUC.Common.PrismEvents;
using LUC.Interfaces;
using LUC.Interfaces.Constants;

using Prism.Events;
using Prism.Regions;

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace LUC.Services.Implementation
{
    [Export( typeof( INavigationManager ) )]
    public class NavigationManager : INavigationManager
    {
        private Dispatcher m_appCurrentDispatcher;

        private readonly IRegionManager m_regionManager;
        private readonly IEventAggregator m_eventAggregator;

        [ImportingConstructor]
        public NavigationManager( IRegionManager regionManager, IEventAggregator eventAggregator )
        {
            this.m_regionManager = regionManager;
            this.m_eventAggregator = eventAggregator;
        }

        [Import( typeof( ISettingsService ) )]
        private ISettingsService SettingsService { get; set; }

        [Import( typeof( INotifyService ) )]
        private INotifyService NotifyService { get; set; }

        [Import( typeof( ICurrentUserProvider ) )]
        private ICurrentUserProvider CurrentUserProvider { get; set; }

        public void SetAppCurrentDispatcher( Dispatcher dispatcher ) => m_appCurrentDispatcher = dispatcher;

        public void NavigateToLoginView() => m_appCurrentDispatcher.Invoke( () =>
                                {
                                    m_regionManager.RequestNavigate( RegionNames.SHELL_REGION, ViewNames.LOGIN_VIEW_NAME ); // TODO Try to move outside of curr dispatcher
                                    Application.Current.MainWindow.WindowState = WindowState.Normal;
                                    Application.Current.MainWindow.ShowInTaskbar = true;
                                    _ = Application.Current.MainWindow.Activate();
                                } );

        public void TryNavigateToDesktopView()
        {
            String rootFolderPath = SettingsService.ReadUserRootFolderPath();

            if ( rootFolderPath == null )
            {
                TrySelectSyncFolderAndNavigateToDesktopView();
            }
            else
            {
                if ( !rootFolderPath.Equals( CurrentUserProvider.RootFolderPath ) )
                {
                    CurrentUserProvider.RootFolderPath = rootFolderPath;
                }

                if ( !Directory.Exists( rootFolderPath ) )
                {
                    _ = Directory.CreateDirectory( rootFolderPath );
                }

                NavigateToDesktopView( rootFolderPath );
            }
        }

        public void TrySelectSyncFolder( out Boolean isUserSelectedRightPath, out String syncFolder )
        {
            isUserSelectedRightPath = false;
            syncFolder = String.Empty;

            var dialog = new FolderBrowserDialog
            {
                BrowseFiles = false,
                BrowseShares = false,
                Title = "Please select sync folder",
                ShowStatusText = false
            };

            if ( dialog.ShowDialog() == true )
            {
                String driveLetter = Path.GetPathRoot( dialog.SelectedPath );
                var driveInfo = new DriveInfo( driveLetter );

                if ( driveInfo.DriveFormat == GeneralConstants.NTFS_FILE_SYSTEM )
                {
                    isUserSelectedRightPath = true;
                    syncFolder = dialog.SelectedPath;
                }
                else
                {
                    NotifyService.NotifyInfo( $"Please select drive with {GeneralConstants.NTFS_FILE_SYSTEM} file system." );
                }
            }
            else
            {
                NotifyService.NotifyInfo( "You should select some folder for starting sync process. You may change it at any time." );
            }
        }

        private void TrySelectSyncFolderAndNavigateToDesktopView()
        {
            var dialog = new FolderBrowserDialog
            {
                BrowseFiles = false,
                BrowseShares = false,
                Title = "Please select sync folder",
                ShowStatusText = false
            };

            if ( dialog.ShowDialog() == true )
            {
                String driveLetter = Path.GetPathRoot( dialog.SelectedPath );

                if ( new DriveInfo( driveLetter ).DriveFormat == GeneralConstants.NTFS_FILE_SYSTEM )
                {
                    NavigateToDesktopView( dialog.SelectedPath );
                }
                else
                {
                    NotifyService.NotifyInfo( $"Please select drive with {GeneralConstants.NTFS_FILE_SYSTEM} file system." );
                }
            }
            else
            {
                NotifyService.NotifyInfo( "You should select some folder for starting sync process. You may change it at any time." );
            }
        }

        private void NavigateToDesktopView( String rootFolderPath )
        {
            CurrentUserProvider.RootFolderPath = rootFolderPath;
            NavigateToDesktopView();
        }

        public void NavigateToDesktopView()
        {
            m_regionManager.RequestNavigate( RegionNames.SHELL_REGION, ViewNames.DESKTOP_VIEW_NAME );
            m_eventAggregator.GetEvent<NeedsToBeMinimizedEvent>().Publish( true );
        }

        public void NavigateToSelectFoldersForIgnoreView() => m_regionManager.RequestNavigate( RegionNames.SHELL_REGION, ViewNames.SELECT_FOLDERS_FOR_IGNORE_VIEW_NAME );
    }
}
