using Serilog;

using System;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace LUC.Interfaces.Abstract
{
    public abstract class WebRestorable
    {
        public const Int32 SECONDS_BETWEEN_ATTEMPTS = 300;

        private readonly DispatcherTimer m_restoreSyncTimer;
        public static Boolean IsTokenExpiredOrIncorrectAccessToken { get; set; }

        protected WebRestorable()
        {
            m_restoreSyncTimer = new DispatcherTimer
            {
                Interval = new TimeSpan( 0, 0, 0, SECONDS_BETWEEN_ATTEMPTS, 0 ),
                IsEnabled = false
            };
        }

        protected abstract Action StopOperation { get; }

        protected abstract Action RerunOperation { get; }

        protected async Task ExecuteAndRestoreIfOffline( Func<Task> asyncActionThatMayThrowException )
        {
            try
            {
                if ( IsInternetConnectionAvaliable() )
                {
                    await asyncActionThatMayThrowException();
                }
                else
                {
                    StopAndRunRestoreTimer();
                }
            }
            catch ( TaskCanceledException ex )
            {
                Log.Error( ex, "TaskCanceledException" );
                StopAndRunRestoreTimer();
            }
            catch ( HttpRequestException ex )
            {
                Log.Error( ex, "HttpRequestException" );
                StopAndRunRestoreTimer();
            }
            catch ( WebException ex )
            {
                if ( ex.InnerException != null )
                {
                    Log.Error( ex.InnerException, "InnerException of WebException" );
                }

                Log.Error( ex, "WebException" );
                StopAndRunRestoreTimer();
            }
            catch ( SocketException ex )
            {
                Log.Error( ex, "SocketException" );
                StopAndRunRestoreTimer();
            }
            catch ( Exception exception )
            {
                Log.Error( "Unexpected exception:" + exception.Message );
                StopAndRunRestoreTimer();
            }
        }

        private void StopAndRunRestoreTimer()
        {
            // Case 1: User renamed bucket during uploading file from server.
            Console.WriteLine( "Seems like you are offline or established connection failed..." );
            Console.WriteLine( "Trying to restore sync process..." );

            StopOperation.Invoke();
            m_restoreSyncTimer.Tick += RestoreSyncTimerTick;
            m_restoreSyncTimer.IsEnabled = true;
        }

        private void RestoreSyncTimerTick( Object sender, EventArgs e )
        {
            if ( IsInternetConnectionAvaliable() )
            {
                Console.WriteLine( "You are online! Reconnecting to server..." );

                m_restoreSyncTimer.Tick -= RestoreSyncTimerTick;
                m_restoreSyncTimer.IsEnabled = false;

                Boolean isSuccess;

                do
                {
                    try
                    {
                        RerunOperation.Invoke();
                        isSuccess = true;
                    }
                    catch ( Exception ex )
                    {
                        Log.Error( ex, nameof( RestoreSyncTimerTick ) );
                        isSuccess = false;
                    }
                } while ( !isSuccess );
            }
            else
            {
                Console.WriteLine( "You are still offline..." );
            }
        }

        public static Boolean IsInternetConnectionAvaliable()
        {
            try
            {
                Boolean isNetworkAvaliable = NetworkInterface.GetIsNetworkAvailable();
                return isNetworkAvaliable;
            }
            catch ( Exception )
            {
                return false;
            }
        }
    }
}