using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;

using Nito.AsyncEx.Synchronous;

using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;

using System;
using System.ComponentModel.Composition;
using System.Text;
using System.Threading.Tasks;

namespace LUC.ViewModels
{
    [Export]
    public class PasswordForEncryptionKeyViewModel : BindableBase, INavigationAware
    {
        [Import( typeof( IAesCryptographyService ) )]
        private IAesCryptographyService AesCryptographyService { get; set; }

        [Import( typeof( IApiClient ) )]
        private IApiClient ApiClient { get; set; }

        [Import( typeof( INotifyService ) )]
        private INotifyService NotifyService { get; set; }

        [Import( typeof( ISettingsService ) )]
        private ISettingsService SettingsService { get; set; }

        [Import( typeof( INavigationManager ) )]
        private INavigationManager NavigationManager { get; set; }

        public PasswordForEncryptionMode Mode { get; private set; }

        private String m_passwordForKey;
        public String PasswordForKey
        {
            get => m_passwordForKey;
            set
            {
                m_passwordForKey = value;
                RaisePropertyChanged( nameof( PasswordForKey ) );
            }
        }

        private DelegateCommand m_okCommand;
        public DelegateCommand OkCommand => m_okCommand ?? ( m_okCommand = new DelegateCommand( ExecuteOkCommand ) );

        private async Task DownloadEncryptionKey()
        {
            Interfaces.OutputContracts.FileUploadResponse uploadResult = await ApiClient.TryUploadAsync( null ); // TODO RR How download encrypted by password key?

            if ( uploadResult.IsSuccess )
            {
                String encryptedStringKeyAsString = String.Empty; // TODO RR

                Byte[] passwordForKeyAsBytesArray = Encoding.UTF8.GetBytes( PasswordForKey );

                String decryptedStringKeyAsString = AesCryptographyService.Decrypt( encryptedStringKeyAsString, passwordForKeyAsBytesArray, passwordForKeyAsBytesArray );

                Byte[] decryptedKey = Encoding.UTF8.GetBytes( decryptedStringKeyAsString );

                String decryptedStringKey = Encoding.UTF8.GetString( decryptedKey );
                String base64Key = decryptedStringKey.Base64Encode();

                ApiClient.EncryptionKey = decryptedKey;
                SettingsService.WriteBase64EncryptionKey( base64Key );

                NavigationManager.TryNavigateToDesktopView();
            }
            else
            {
                NotifyService.NotifyStaticMessage( "Error during downloading key. Please try again later or contact us." );
            }
        }

        //TODO
        private async Task GenerateEncryptionKeyAndUpload()
        {
            Byte[] randomKey = AesCryptographyService.GenerateRandomKey();

            String randomStringKey = Encoding.UTF8.GetString( randomKey );

            Byte[] passwordForKeyAsBytesArray = Encoding.UTF8.GetBytes( PasswordForKey );

            String encryptedStringKeyAsString = AesCryptographyService.Encrypt( randomStringKey, passwordForKeyAsBytesArray, passwordForKeyAsBytesArray );

            Interfaces.OutputContracts.FileUploadResponse uploadResult = await ApiClient.TryUploadAsync( null ); // TODO RR How upload encrypted by password key?

            // if ok - generate key, apply password, send to server, if ok - write to settings base64EncryptionKey

            Boolean isServerOk = true;

#pragma warning disable S2583 // Conditionally executed code should be reachable
            if ( isServerOk )
#pragma warning restore S2583 // Conditionally executed code should be reachable
            {
                String base64Key = randomStringKey.Base64Encode();
                SettingsService.WriteBase64EncryptionKey( base64Key );
                ApiClient.EncryptionKey = randomKey;
                //TODO finish normally message
                _ = NotifyService.ShowMessageBox( "Please share typed password with all team members. If you forgot one - bla bla bla.", "Attention", System.Windows.MessageBoxButton.OK );
                NavigationManager.TryNavigateToDesktopView();
            }
            else
            {
                NotifyService.NotifyStaticMessage( "Error during generation key. Please try again later or contact us." );
            }
        }

        private void ExecuteOkCommand()
        {
            if ( ApiClient.EncryptionKey == null )
            {
                // TODO Release 2.0 Add Password validation.

                if ( Mode == PasswordForEncryptionMode.GenerateAndUploadToServer )
                {
                    GenerateEncryptionKeyAndUpload().WaitAndUnwrapException();
                }
                else
                {
                    DownloadEncryptionKey().WaitAndUnwrapException();
                }
            }
            else
            {
                NavigationManager.TryNavigateToDesktopView();
            }
        }

        public Boolean IsNavigationTarget( NavigationContext navigationContext ) => true;

        public void OnNavigatedFrom( NavigationContext navigationContext ) => PasswordForKey = null;

        public void OnNavigatedTo( NavigationContext navigationContext )
        {
            Object modeAsObject = navigationContext.Parameters[ NavigationParameterNames.PASSWORD_FOR_ENCRYPTION_MODE ];
            if ( modeAsObject == null )
            {
                throw new ArgumentNullException( String.Empty, $"{nameof( PasswordForEncryptionKeyViewModel )}: method {nameof( OnNavigatedTo )} expects parameter {nameof( NavigationParameterNames.PASSWORD_FOR_ENCRYPTION_MODE )}" );
            }

            Mode = (PasswordForEncryptionMode)modeAsObject;
        }
    }
}
