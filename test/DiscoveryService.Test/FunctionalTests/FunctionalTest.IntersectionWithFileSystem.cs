using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Test.Extensions;
using LUC.Interfaces;

namespace LUC.DiscoveryService.Test.FunctionalTests
{
    partial class FunctionalTest
    {
#if DEBUG
        private static FileSystemWatcher s_fileSystemWatcher;

        //[PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        private static void InitWatcherForIntegrationTests( ApiClient.ApiClient apiClient )
        {
            String downloadTestFolderFullName = DownloadTestFolderFullName( Constants.DOWNLOAD_TEST_NAME_FOLDER );
            String rootFolder = s_settingsService.ReadUserRootFolderPath();

            if ( rootFolder != null )
            {
                try
                {
                    DirectoryExtension.CopyDirsAndSubdirs( rootFolder, downloadTestFolderFullName );
                }
                catch ( Exception ex )
                {
                    Console.WriteLine( ex.ToString() );
                }
            }

            UpdateRootFolderPath( downloadTestFolderFullName, apiClient );

            s_fileSystemWatcher = new FileSystemWatcher( downloadTestFolderFullName )
            {
                IncludeSubdirectories = true,
                Filter = "*.*"
            };

            s_fileSystemWatcher.Created += ( sender, eventArgs ) => OnChanged( apiClient, eventArgs );
            //s_fileSystemWatcher.Changed += ( sender, eventArgs ) => OnChanged( apiClient, eventArgs );

            s_fileSystemWatcher.EnableRaisingEvents = true;
        }

        private static void UpdateRootFolderPath( String downloadTestFolderFullName, ApiClient.ApiClient apiClient = null )
        {
            s_settingsService.CurrentUserProvider.RootFolderPath = downloadTestFolderFullName;
            SetUpTests.LoggingService.SettingsService = s_settingsService;
            s_settingsService.WriteUserRootFolderPath( downloadTestFolderFullName );

            Console.WriteLine($"Full root folder name is updated to {downloadTestFolderFullName}");

            if ( apiClient != null )
            {
                apiClient.CurrentUserProvider.RootFolderPath = downloadTestFolderFullName;
            }
        }

        private static async void OnChanged( Object sender, FileSystemEventArgs eventArgs ) =>
            await TryUploadFileAsync( (IApiClient)sender, eventArgs ).ConfigureAwait( continueOnCapturedContext: false );

        private static async Task TryUploadFileAsync( IApiClient apiClient, FileSystemEventArgs eventArgs )
        {
            //to signal that file was changed in Constants.DOWNLOAD_TEST_NAME_FOLDER
            Console.Beep();

            Boolean whetherTryUpload = UserIntersectionInConsole.NormalResposeFromUserAtClosedQuestion( closedQuestion: $"Do you want to upload on server file {eventArgs.Name}. It was {Enum.GetName( typeof( WatcherChangeTypes ), eventArgs.ChangeType )}" );
            if ( whetherTryUpload )
            {
                try
                {
                    await apiClient.TryUploadAsync( new FileInfo( eventArgs.FullPath ) ).ConfigureAwait( continueOnCapturedContext: false );
                }
                catch ( NullReferenceException )
                {
                    ;//file is not changed in any group
                }
                catch ( Exception ex )
                {
                    String logRecord = Display.StringWithAttention( ex.ToString() );
                    SetUpTests.LoggingService.LogInfo( logRecord );
                    //Debug.Fail( ex.Message, detailMessage: ex.ToString() );
                }
            }
        }
#endif
    }
}
