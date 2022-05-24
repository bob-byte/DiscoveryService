using System;
using System.IO;
using System.Reflection;
using System.Security.Permissions;
using System.Threading;

using DiscoveryServices.Common;
using DiscoveryServices.Test.Extensions;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

namespace DiscoveryServices.Test.FunctionalTests
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

        private static void UpdateLucRootFolder( String machineId, ICurrentUserProvider currentUserProvider, out String newLucFullFolderName )
        {
            Int32 countLettersOfNewFolder = 5;
            String pathFromExeFileToRootFolder = Path.Combine( DsConstants.DOWNLOAD_TEST_NAME_FOLDER, machineId.Substring( startIndex: machineId.Length - countLettersOfNewFolder ) );

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

            UpdateRootFolderPath( newLucFullFolderName, currentUserProvider );
        }

        /// <summary>
        /// Root folder which also is available for containers
        /// </summary>
        private static String RootFolder()
        {
            String rootFolder = s_settingsService.ReadUserRootFolderPath();

            if ( !String.IsNullOrWhiteSpace( rootFolder ) )
            {
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
        private static void InitWatcherForIntegrationTests( IApiClient apiClient, ICurrentUserProvider currentUserProvider, String machineId )
        {
            UpdateLucRootFolder( machineId, currentUserProvider, out String newLucFullFolderName );

            s_fileSystemWatcher = new FileSystemWatcher( newLucFullFolderName )
            {
                IncludeSubdirectories = true,
                Filter = "*.*"
            };

            s_fileSystemWatcher.Created += ( sender, eventArgs ) => OnChanged( apiClient, eventArgs );
            //s_fileSystemWatcher.Changed += ( sender, eventArgs ) => OnChanged( apiClient, eventArgs );

            s_fileSystemWatcher.EnableRaisingEvents = true;
        }

        private static void UpdateRootFolderPath( String downloadTestFolderFullName, ICurrentUserProvider currentUserProvider )
        {
            currentUserProvider.RootFolderPath = downloadTestFolderFullName;

            String lucSettingsFilePath = Display.VariableWithValue( nameof( AppSettings.FilePath ), AppSettings.FilePath, useTab: false );
            Console.WriteLine( lucSettingsFilePath );

            String currentRootFolder = s_settingsService.ReadUserRootFolderPath();
            if ( currentRootFolder != downloadTestFolderFullName )
            {
                s_settingsService.WriteUserRootFolderPath( downloadTestFolderFullName );

                Console.WriteLine( $"Full root folder name is updated to {s_settingsService.ReadUserRootFolderPath()}" );
            }
        }

        private static void OnChanged( Object sender, FileSystemEventArgs eventArgs )
        {
            //wait while shows
            Thread.Sleep( TimeSpan.FromSeconds( value: 0.5 ) );
            TryUploadFile( (IApiClient)sender, eventArgs );
        }

        private static void TryUploadFile( IApiClient apiClient, FileSystemEventArgs eventArgs )
        {
            lock ( UserIntersectionInConsole.Lock )
            {
                Boolean whetherTryUpload = UserIntersectionInConsole.NormalResposeFromUserAtClosedQuestion( closedQuestion: $"Do you want to upload on server file {eventArgs.Name}. It was {Enum.GetName( typeof( WatcherChangeTypes ), eventArgs.ChangeType ).ToLowerInvariant()}" );
                if ( whetherTryUpload )
                {
                    try
                    {
                        FileUploadResponse response = apiClient.TryUploadAsync( new FileInfo( eventArgs.FullPath ) ).GetAwaiter().GetResult();
                    }
                    catch ( NullReferenceException )
                    {
                        ;//file is not changed in any group
                    }
                    catch ( Exception ex )
                    {
                        DsSetUpTests.LoggingService.LogCriticalError( ex );
                    }
                }
            }
        }
    }
}
