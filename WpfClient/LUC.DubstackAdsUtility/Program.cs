using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace LUC.DubstackAdsUtility
{
    public class Program
    {
        public static void Main( String[] args )
        {
            Encoding encoding = Console.OutputEncoding;
            TextWriter inConsoleWriter = Console.Out;

            MessageBox.Show( String.Join( "-", args ) );
            foreach ( String item in args )
            {
                Console.WriteLine( item );
            }

            if ( !args.Any() )
            {
                return;
            }

            String path = args[ 0 ];

            if ( args.Contains( AdsLockState.ReadyToLock.ToString() ) )
            {
                try
                {
                    if ( new FileInfo( path ).IsReadOnly )
                    {
                        _ = MessageBox.Show( $"File {path} is ReadOnly. Please change it." );

                        return;
                    }

                    AdsExtensions.WriteLockDescription( path, new LockDescription( AdsLockState.ReadyToLock ) );
                }
                catch ( Exception )
                {
                    _ = MessageBox.Show( $"Can't lock file {path}" );
                }
            }
            else if ( args.Contains( AdsLockState.ReadyToUnlock.ToString() ) )
            {
                try
                {
                    if ( new FileInfo( path ).IsReadOnly )
                    {
                        _ = MessageBox.Show( $"File {path} is ReadOnly. Please change it." );

                        return;
                    }

                    AdsExtensions.WriteLockDescription( path, new LockDescription( AdsLockState.ReadyToUnlock ) );
                }
                catch ( Exception )
                {
                    _ = MessageBox.Show( $"Can't unlock file {path}" );
                }
            }
        }

        // Release 3.0 Check app is online and notify if lock is not done on server side.
        private void NotifyIfSyncAppIsNotRunning()
        {
            Process[] pname = Process.GetProcessesByName( "wpfclient" ); // TODO Release 3.0 Rename to production name.

            switch ( pname.Length )
            {
                case 0:
                    _ = MessageBox.Show( "Please run Light Sync application." );
                    break;
                default:
                    _ = Process.Start( Environment.CurrentDirectory + "wpfclient.exe" + " " + "NotifyIfNotLoggedOn." );
                    break;
            }
        }
    }
}
