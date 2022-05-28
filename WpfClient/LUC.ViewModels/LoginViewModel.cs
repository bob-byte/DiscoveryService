using LUC.Common.PrismEvents;
using LUC.DiscoveryServices;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;

using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace LUC.ViewModels
{
    [Export]
    public class LoginViewModel : BindableBase, INavigationAware
    {
        #region Constructors

        [ImportingConstructor]
        public LoginViewModel(
            IRegionManager regionManager,
            IEventAggregator eventAggregator,
            ISettingsService settingsService,
            IPathFiltrator pathFiltrator,
            IApiClient apiClient,
            INavigationManager navigationManager )
        {
            this.m_regionManager = regionManager;
            this.m_eventAggregator = eventAggregator;
            this.m_settingsService = settingsService;
            this.m_pathFiltrator = pathFiltrator;
            this.m_apiClient = apiClient;
            this.m_navigationManager = navigationManager;
        }

        #endregion

        #region Fields

        [Import( typeof( ISettingsService ) )]
        private ISettingsService SettingService { get; set; }

        [Import( typeof( IApiClient ) )]
        private IApiClient ApiClient { get; set; }

        [Import( typeof( ICurrentUserProvider ) )]
        private ICurrentUserProvider CurrentUserProvider { get; set; }

        [Import( typeof( INotifyService ) )]
        private INotifyService NotifyService { get; set; }

        [Import( typeof( ILoggingService ) )]
        private ILoggingService LoggingService { get; set; }

        [Import( typeof( INavigationManager ) )]
        private INavigationManager NavigationManager { get; set; }

        private readonly IRegionManager m_regionManager;
        private readonly IEventAggregator m_eventAggregator;
        private readonly ISettingsService m_settingsService;
        private readonly IPathFiltrator m_pathFiltrator;
        private readonly IApiClient m_apiClient;
        private readonly INavigationManager m_navigationManager;

        #endregion

        #region Properties

        private Boolean m_isBusy;

        public Boolean IsBusy
        {
            get => m_isBusy;
            set
            {
                m_isBusy = value;
                RaisePropertyChanged( nameof( IsBusy ) );
            }
        }

        private String m_login;

        public String Login
        {
            get => m_login;
            set
            {
                m_login = value;
                RaisePropertyChanged( nameof( Login ) );
            }
        }

        private Boolean m_isRememberPassword;

        public Boolean IsRememberPassword
        {
            get => m_isRememberPassword;
            set
            {
                m_isRememberPassword = value;
                RaisePropertyChanged( nameof( IsRememberPassword ) );
            }
        }

        private String m_password;

        public String Password
        {
            get => m_password;
            set
            {
                m_password = value;
                RaisePropertyChanged( nameof( Password ) );
            }
        }

        private DelegateCommand m_loginCommand;

        public DelegateCommand LoginCommand => m_loginCommand ?? ( m_loginCommand = new DelegateCommand( ExecuteLoginCommand ) );

        #endregion

        #region Methods

        // TODO Maybe try another Autoupdater https://winsparkle.org/
        private async void ExecuteLoginCommand()
        {
            IsBusy = true;

            Interfaces.OutputContracts.LoginResponse response = await ApiClient.LoginAsync( Login, Password );

            //wait successful start of monitoring events in root folder and start of sync to and from the server
            await Task.Delay( 6000 );
            IsBusy = false;

            if ( response.IsSuccess )
            {
                SettingService.ReadSettingsFromFile();

                m_settingsService.WriteIsRememberPassword( m_isRememberPassword, Password.Base64Encode() );

                m_eventAggregator.GetEvent<IsUserLoggedChangedEvent>().Publish( new IsUserLoggedChangedEventArgs
                { UserName = response.Login } );

                m_pathFiltrator.ReadFromSettings();

                String base64EncryptionKey = m_settingsService.ReadBase64EncryptionKey();

                String syncFolder = SettingService.ReadUserRootFolderPath();
                if ( String.IsNullOrWhiteSpace( syncFolder ) )
                {
                    Boolean isUserSelectedRightPath;
                    do
                    {
                        NavigationManager.TrySelectSyncFolder( out isUserSelectedRightPath, out syncFolder );
                    }
                    while ( !isUserSelectedRightPath );

                    SettingService.WriteUserRootFolderPath( syncFolder );
                    CurrentUserProvider.RootFolderPath = syncFolder;
                }
                else
                {
                    CurrentUserProvider.RootFolderPath = syncFolder;
                }

                if ( System.String.IsNullOrEmpty( base64EncryptionKey ) )
                {
                    var parameters = new NavigationParameters();

                    if ( response.IsAdmin )
                    {
                        parameters.Add( NavigationParameterNames.PASSWORD_FOR_ENCRYPTION_MODE, PasswordForEncryptionMode.GenerateAndUploadToServer );
                        // navigate to enter password view
                        //type passsword
                        // if ok - generate key, apply password, send to server, if ok - write to settings base64EncryptionKey
                    }
                    else
                    {
                        parameters.Add( NavigationParameterNames.PASSWORD_FOR_ENCRYPTION_MODE, PasswordForEncryptionMode.DownloadFromServer );
                        // try download from server
                        // if ok - save base64 and continue.
                    }

                    m_regionManager.RequestNavigate( RegionNames.SHELL_REGION, ViewNames.PASSWORD_FOR_ENCRYPTION_KEY_VIEW_NAME, parameters );
                }
                else
                {
                    m_apiClient.EncryptionKey = System.Text.Encoding.Unicode.GetBytes( base64EncryptionKey.Base64Decode() );

                    m_navigationManager.TryNavigateToDesktopView();
                }

                NavigationManager.TryNavigateToDesktopView();

                Password = null;
                IsRememberPassword = false;

                //update DS
                _ = DiscoveryServiceFacade.FullyInitialized( CurrentUserProvider, SettingService );
            }
            else
            {
                LoggingService.LogInfo( response.Message );
                NotifyService.NotifyStaticMessage( response.Message );
            }
        }

        public void OnNavigatedTo( NavigationContext navigationContext )
        {
            if ( navigationContext.Parameters[ NavigationParameterNames.IS_NAVIGATION_FROM_MAIN_MODULE ] != null )
            {
                String rememberedPassword = m_settingsService.ReadBase64Password();

                if ( !System.String.IsNullOrEmpty( rememberedPassword ) )
                {
                    String rememberedLogin = m_settingsService.ReadRememberedLogin();
                    Login = rememberedLogin;

                    m_isRememberPassword = true;
                    Password = rememberedPassword.Base64Decode();
                    LoginCommand.Execute();
                }
            }
        }

        public Boolean IsNavigationTarget( NavigationContext navigationContext ) => true;

        public void OnNavigatedFrom( NavigationContext navigationContext )
        {
            Login = System.String.Empty;
            Password = System.String.Empty;
            NotifyService.ClearStaticMessages();
        }

        #endregion

        // TODO Release 2.0 Add support renaming of volume letter.
    }
}
