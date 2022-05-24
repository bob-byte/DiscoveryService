using LUC.Common.PrismEvents;
using LUC.Globalization;
using LUC.Interfaces;
using LUC.Interfaces.Enums;

using Prism.Events;
using Prism.Regions;

using System;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;

namespace LUC.WpfClient.Views
{
    [Export]
    public partial class ShellView : Window
    {
        #region Constructors

        [ImportingConstructor]
        public ShellView( IEventAggregator eventAggregator,
                         ISettingsService settingsService,
                         IRegionManager regionManager )
        {
            InitializeComponent();
            notifyIcon.Icon = new Icon( @"LightSquareIcon32x32.ico" );
            IconAnimator = new NotifyIconAnimator( notifyIcon );
            RegionManager = regionManager;
            //RegionManager.RegisterViewWithRegion( Interfaces.Constants.RegionNames.SHELL_REGION, typeof( ShellView ) );
            SettingsService = settingsService;

            _ = eventAggregator.GetEvent<IsUserLoggedChangedEvent>().Subscribe( args =>
               {
                   ChangeFolderMenuItem.Visibility = Visibility.Visible;
                   LogoutMenuItem.Visibility = Visibility.Visible;
                   Title = $"lightup.cloud - {args.UserName} - {Assembly.GetEntryAssembly().GetName().Version}";
               } );

            _ = eventAggregator.GetEvent<IsSyncFromServerChangedEvent>().Subscribe( args =>
             {
                 IconAnimator.RunIconAnimation(
                     args ? NotifyIconAnimationType.Download : NotifyIconAnimationType.Default );
             } );

            _ = eventAggregator.GetEvent<IsSyncToServerChangedEvent>().Subscribe( args => IconAnimator.RunIconAnimation( args ? NotifyIconAnimationType.Upload : NotifyIconAnimationType.Default ) );

            Version version = Assembly.GetEntryAssembly().GetName().Version;
            Title = $"lightup.cloud {version}";
        }

        #endregion

        #region Properties

        [Import]
        public INotifyService NotifyService { get; set; }

        [Import]
        public ILoggingService LoggingService { get; set; }

        [Import]
        public IApiClient ApiClient { get; set; }

        [Import]
        public INavigationManager NavigationManager { get; set; }

        [Import]
        public ShellViewModel ViewModel
        {
            set => DataContext = value;
        }
        public IRegionManager RegionManager { get; set; }
        public ISettingsService SettingsService { get; set; }
        public NotifyIconAnimator IconAnimator { get; set; }

        #endregion

        #region Methods
        
        private void NotifyIcon_MouseDoubleClick( Object sender, RoutedEventArgs e )
        {
            Application.Current.MainWindow.WindowState = WindowState.Normal;
            Application.Current.MainWindow.ShowInTaskbar = true;
            _ = Application.Current.MainWindow.Activate();
        }

        private void ChangeFolderMenuItem_PreviewMouseLeftButtonUp( Object sender, MouseButtonEventArgs e ) => ( (ShellViewModel)DataContext ).ChangeFolderForMonitoringCommand.Execute();

        private void ExitMenuItem_PreviewMouseLeftButtonUp( Object sender, MouseButtonEventArgs e )
        {
            _ = Application.Current.MainWindow.Activate();

            if ( NotifyService.ShowMessageBox( Strings.Message_DoYouWantExitNow, Strings.Label_ExitConfirmation, MessageBoxButton.YesNo ) == MessageBoxResult.Yes )
            {
                Application.Current.Shutdown();
            }
        }

        private async void LogoutMenuItem_PreviewMouseLeftButtonUp( Object sender, MouseButtonEventArgs e )
        {
            _ = Application.Current.MainWindow.Activate();

            if ( NotifyService.ShowMessageBox( Strings.Message_DoYouWantLogoutNow, Strings.Label_LogoutConfirmation, MessageBoxButton.YesNo ) == MessageBoxResult.Yes )
            {
                Interfaces.OutputContracts.LogoutResponse logoutResponse = await ApiClient.LogoutAsync();

                ChangeFolderMenuItem.Visibility = Visibility.Collapsed;
                LogoutMenuItem.Visibility = Visibility.Collapsed;
                Title = "lightup.cloud";
                NavigationManager.NavigateToLoginView();
                Application.Current.MainWindow.WindowState = WindowState.Normal;
                Application.Current.MainWindow.ShowInTaskbar = true;
                _ = Application.Current.MainWindow.Activate();

                if ( !logoutResponse.IsSuccess )
                {
                    LoggingService.LogError( "Not success logout." );
                }
            }
        }

        #endregion

        private void Eng_PreviewMouseLeftButtonUp( Object sender, MouseButtonEventArgs e ) => UpdateCurrentCulture( "en" );

        private void Ukr_PreviewMouseLeftButtonUp( Object sender, MouseButtonEventArgs e ) => UpdateCurrentCulture( "uk" );

        private void Rus_PreviewMouseLeftButtonUp( Object sender, MouseButtonEventArgs e ) => UpdateCurrentCulture( "ru" );

        private void UpdateCurrentCulture( String culture )
        {
            SettingsService.WriteLanguageCulture( culture );

            var cultureInfo = new CultureInfo( culture );
            Application.Current.MainWindow.Language = XmlLanguage.GetLanguage( cultureInfo.IetfLanguageTag );
            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
            TranslationSource.Instance.CurrentCulture = cultureInfo;
        }
    }
}
