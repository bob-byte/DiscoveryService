using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Test.Extensions;
using LUC.Interfaces;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.OutputContracts;

namespace LUC.DiscoveryServices.Test.FunctionalTests
{
    partial class FunctionalTest
    {
        private static FileSystemWatcher s_fileSystemWatcher;

        private static String DownloadTestFolderFullName( String downloadTestFolderName )
        {
            String fullDllFileName = Assembly.GetEntryAssembly().Location;
            String pathToDllFileName = Path.GetDirectoryName( fullDllFileName );

            String downloadTestFolderFullName = Path.Combine( pathToDllFileName, downloadTestFolderName );

            return downloadTestFolderFullName;
        }

        private static void UpdateLucRootFolder( ApiClient.ApiClient apiClient, String machineId, out String newLucFullFolderName )
        {
            String pathFromExeFileToRootFolder = Path.Combine( Constants.DOWNLOAD_TEST_NAME_FOLDER, machineId );

            String rootFolder = RootFolder();

            newLucFullFolderName = DownloadTestFolderFullName( pathFromExeFileToRootFolder );

            if ( rootFolder != null )
            {
                try
                {
                    DirectoryExtension.CopyDirsAndSubdirs( rootFolder, newLucFullFolderName );
                }
                catch ( Exception ex )
                {
                    Console.WriteLine( ex.ToString() );
                }
            }

            UpdateRootFolderPath( newLucFullFolderName, apiClient );
        }

        /// <summary>
        /// Root folder which also is available for containers
        /// </summary>
        private static String RootFolder()
        {
            String rootFolder = s_settingsService.ReadUserRootFolderPath();

            if ( !String.IsNullOrWhiteSpace( rootFolder ) )
            {
                //get index of b
                String bin = "bin";
                Int32 indexOfBin = rootFolder.IndexOf( bin );
                Boolean isBinInRootFolder = indexOfBin != -1;

                if ( isBinInRootFolder )
                {
                    //change path to bin
                    Int32 startIndex = indexOfBin + bin.Length + 1;

                    //excluded value
                    String pathFromBin = rootFolder.Substring( startIndex, length: rootFolder.Length - startIndex );

                    String pathToExeFile = Extensions.PathExtensions.PathToExeFile();
                    Int32 previousIndexOfCurrentConfFolder = pathToExeFile.LastIndexOf( "\\" );

                    //excluded value
                    String pathToConfFolder = pathToExeFile.Substring( startIndex: 0, previousIndexOfCurrentConfFolder + 1 );

                    rootFolder = Path.Combine( pathToConfFolder, pathFromBin );
                }
            }

            return rootFolder;
        }

        [PermissionSet( SecurityAction.Demand, Name = "FullTrust" )]
        private static void InitWatcherForIntegrationTests( ApiClient.ApiClient apiClient, String machineId )
        {
            UpdateLucRootFolder( apiClient, machineId, out String newLucFullFolderName );

            s_fileSystemWatcher = new FileSystemWatcher( newLucFullFolderName )
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

            String lucSettingsFilePath = Display.VariableWithValue( nameof( s_settingsService.AppSettingsFilePath ), s_settingsService.AppSettingsFilePath, useTab: false );
            Console.WriteLine(lucSettingsFilePath);

            s_settingsService.WriteUserRootFolderPath( downloadTestFolderFullName );

            Console.WriteLine($"Full root folder name is updated to {s_settingsService.ReadUserRootFolderPath()}");

            if ( apiClient != null )
            {
                apiClient.CurrentUserProvider.RootFolderPath = downloadTestFolderFullName;
            }
        }

        private static void OnChanged( Object sender, FileSystemEventArgs eventArgs ) =>
            TryUploadFile( (IApiClient)sender, eventArgs );

        private static void TryUploadFile( IApiClient apiClient, FileSystemEventArgs eventArgs )
        {
            lock ( UserIntersectionInConsole.Lock )
            {
                Boolean whetherTryUpload = UserIntersectionInConsole.NormalResposeFromUserAtClosedQuestion( closedQuestion: $"Do you want to upload on server file {eventArgs.Name}. It was {Enum.GetName( typeof( WatcherChangeTypes ), eventArgs.ChangeType ).ToLowerInvariant()}" );
                if ( whetherTryUpload )
                {
                    try
                    {
                        apiClient.TryUploadAsync( new FileInfo( eventArgs.FullPath ) ).GetAwaiter().GetResult();
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
        }
    }
}
