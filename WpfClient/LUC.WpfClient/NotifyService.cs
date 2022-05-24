using LUC.Interfaces;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Models;
using LUC.WpfClient.Views;

using Nito.AsyncEx;

using Serilog;

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace LUC.WpfClient
{
    [Export( typeof( INotifyService ) )]
    public class NotifyService : INotifyService
    {
        private ShellView m_shellView;

        private ShellView ShellView
        {
            get
            {
                if ( m_shellView == null )
                {
                    foreach ( Window window in Application.Current.Windows )
                    {
                        if ( window.GetType() == typeof( ShellView ) )
                        {
                            m_shellView = window as ShellView;
                        }
                    }
                }

                return m_shellView;
            }
        }

        public void NotifyInfo( String message ) => ShellView?.notifyIcon.ShowBalloonTip( "Success", message, Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info );

        public void NotifyError( String message ) => ShellView?.notifyIcon.ShowBalloonTip( "Error", message, Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Error );

        public void Notify( INotificationResult notificationResult )
        {
            String notify = notificationResult.Message ?? notificationResult.ToString();

            ShellView?.notifyIcon.ShowBalloonTip( "Notify", notify, Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info );
        }

        public void NotifyStaticMessage( String message ) =>
            ShellView.Dispatcher.Invoke( callback: () =>
            {
                ( ShellView.DataContext as ShellViewModel ).StaticMessages.Clear();
                ( ShellView.DataContext as ShellViewModel ).StaticMessages.Add( message );
            } );

        public void ClearStaticMessages() =>
            ShellView.Dispatcher.Invoke( callback: () => ( ShellView.DataContext as ShellViewModel ).StaticMessages.Clear() );

        public MessageBoxResult ShowMessageBox( String message, String caption, MessageBoxButton buttons )
        {
            var myResourceDictionary = new ResourceDictionary
            {
                Source = new Uri( "/LUC.WpfClient;component/StylesDictionary.xaml", UriKind.RelativeOrAbsolute )
            };

            var defaultStyle = myResourceDictionary[ "DefaultMessageBoxStyle" ] as Style;

            MessageBoxResult result = Xceed.Wpf.Toolkit.MessageBox.Show( message, caption, buttons, defaultStyle );
            return result;
        }

        public async Task<MessageBoxResult> ShowMessageBoxAsync( System.String message, System.String caption, MessageBoxButton buttons )
        {
            MessageBoxResult result = default;
            var userClosedWindow = new AsyncAutoResetEvent( set: false );

            var newWindowThread = new Thread( () =>
             {
                 SynchronizationContext.SetSynchronizationContext( new DispatcherSynchronizationContext( ShellView.Dispatcher ) );

                 try
                 {
                     result = MessageBox.Show( message, caption, buttons );
                 }
                 catch(Exception ex)
                 {
                     Console.WriteLine(ex.ToString());
                 }

                 userClosedWindow.Set();
             } );
            newWindowThread.SetApartmentState( ApartmentState.STA );
            newWindowThread.Priority = ThreadPriority.Highest;
            newWindowThread.Start();

            await userClosedWindow.WaitAsync();

            return result;
        }
    }
}
