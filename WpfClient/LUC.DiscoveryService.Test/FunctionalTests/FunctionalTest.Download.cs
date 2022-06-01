using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia.Downloads;
using LUC.DiscoveryServices.Messages;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

namespace LUC.DiscoveryServices.Test.FunctionalTests
{
    partial class FunctionalTest
    {
        /// <summary>
        /// Downloads random file which isn't in current PC if you aren't only in network. Otherwise it will download file which is
        /// </summary>
        private async static Task DownloadRandomFileAsync( IApiClient apiClient, ICurrentUserProvider currentUserProvider )
        {
            var download = new DownloaderFromLocalNetwork( s_discoveryService, IoBehavior.Asynchronous );
            download.FileSuccessfullyDownloaded += OnFileDownloaded;

            String filePrefix = UserIntersectionInConsole.ValidValueInputtedByUser( requestToUser: "Input file prefix where new file can exist on the server: ", ( userInput ) =>
             {
                 Boolean isValidInput = Extensions.PathExtensions.IsValidPath( userInput ) && ( !userInput.Contains( ":" ) );

                 return isValidInput;
             } );

            (ObjectDescriptionModel fileDescription, IBucketName bucketName, String localFolderPath, String hexFilePrefix) = await RandomFileToDownloadAsync( apiClient, currentUserProvider, filePrefix ).ConfigureAwait( false );

            lock ( UserIntersectionInConsole.Lock )
            {
                Console.WriteLine( "Press any key to start download process. \n" +
                "After that if you want to cancel download, press C key. If you do not want to cancel, press any other button." );
                Console.ReadKey( intercept: true );//true says that pressed key will not show in console

                var downloadTask = Task.Run( async () =>
                 {
                     var downloadedChunks = new List<ChunkRange>();
                     IProgress<FileDownloadProgressArgs> downloadProgress = new Progress<FileDownloadProgressArgs>( ( progressArgs ) => downloadedChunks.Add( progressArgs.ChunkRange ) );

                     download.FilePartiallyDownloaded += ( sender, eventArgs ) =>
                     {
                         IEnumerable<ChunkRange> undownloadedChunks = eventArgs.UndownloadedRanges;

                         Console.WriteLine( "Undownloaded chunks: " );
                         ShowChunkRanges( undownloadedChunks );
                     };

                     String targetFullFileName = Path.Combine( localFolderPath, filePrefix, fileDescription.OriginalName );
                     //#if RECEIVE_UDP_FROM_OURSELF
                     //                     String directoryPath = Directory.GetParent( Path.GetDirectoryName( targetFullFileName ) ).FullName;
                     //                     targetFullFileName = Path.Combine( directoryPath, fileDescription.OriginalName );
                     //#endif

                     var downloadingFileInfo = fileDescription.ToDownloadingFileInfo(
                         bucketName.ServerName,
                         targetFullFileName,
                         hexFilePrefix
                     );

                     try
                     {
                         var startTimeOfDownload = DateTime.Now;

                         await download.DownloadFileAsync(
                             downloadingFileInfo,
                             fileChangesQueue: null,
                             downloadProgress
                         ).ConfigureAwait( false );

                         TimeSpan downloadTime = DateTime.Now.Subtract( startTimeOfDownload );
                         Console.WriteLine( $"Download time is {downloadTime} of file with size {(Double)downloadingFileInfo.ByteCount / GeneralConstants.BYTES_IN_ONE_MEGABYTE} MB" );
                     }
                     catch ( Exception ex )
                     {
                         Console.WriteLine( ex.ToString() );
                     }

                     if ( downloadedChunks.Count > 0 )
                     {
                         Console.WriteLine( "Downloaded chunks: " );
                         ShowChunkRanges( downloadedChunks );
                     }

                     Console.WriteLine( "If you have not pressed any button, do so to continue testing" );
                 } );

                ConsoleKey pressedKey = Console.ReadKey().Key;
                Console.WriteLine();
                if ( pressedKey == ConsoleKey.C )
                {
                    s_cancellationTokenSource.Cancel();
                }

                downloadTask.GetAwaiter().GetResult();
            }

            s_cancellationTokenSource = new CancellationTokenSource();
        }

        private static void ShowChunkRanges( IEnumerable<ChunkRange> chunkRanges )
        {
            foreach ( ChunkRange chunk in chunkRanges )
            {
                Console.WriteLine( chunk.ToString() );
            }
        }

        private static void OnFileDownloaded( Object sender, FileDownloadedEventArgs eventArgs )
        {
            try
            {
                AdsExtensions.WriteInfoAboutNewFileVersion( new FileInfo( eventArgs.FullFileName ), eventArgs.Version, eventArgs.Guid );
            }
            catch ( Exception ex )
            {
                DsLoggerSet.DefaultLogger.LogError( ex, logRecord: $"Cannot write version ({eventArgs.Version}) or guid ({eventArgs.Guid}) for {eventArgs.FullFileName}" );
            }
        }

        private async static Task<(ObjectDescriptionModel randomFileToDownload, IBucketName bucketName, String localFolderPath, String hexFilePrefix)> RandomFileToDownloadAsync( IApiClient apiClient, ICurrentUserProvider currentUserProvider, String filePrefix )
        {
            IList<String> bucketDirectoryPathes = currentUserProvider.ProvideBucketDirectoryPaths();

            String bucketPath = null;
            UserIntersectionInConsole.ValidValueInputtedByUser(
                requestToUser: "Input bucket directory path or it name: ",
                ( userInput ) =>
                {
                    bucketPath = bucketDirectoryPathes.SingleOrDefault( ( path ) => path.Contains( userInput ) );
                    return bucketPath != null;
                }
            );

            IBucketName bucketName = currentUserProvider.GetBucketNameByDirectoryPath( bucketPath );
            String serverBucketName = bucketName.ServerName;

            String hexFilePrefix = filePrefix.ToHexPrefix();

            ObjectsListResponse objectsListResponse = await apiClient.ListAsync( serverBucketName, hexFilePrefix ).ConfigureAwait( continueOnCapturedContext: false );
            var objectsListModel = objectsListResponse.ToObjectsListModel();
            ObjectDescriptionModel[] undeletedObjectsListModel = objectsListModel.ObjectDescriptions.Where( c => !c.IsDeleted ).ToArray();

            //select from undeletedObjectsListModel files which exist in current PC if tester is only in network. 
            //If the last one is not, select files which don't exist in current PC
            var bjctDscrptnsFrDwnld = undeletedObjectsListModel.Where( cachedFileInServer =>
             {
                 String fullPathToFile = Path.Combine( bucketPath, filePrefix, cachedFileInServer.OriginalName );
                 Boolean isFileInCurrentPc = File.Exists( fullPathToFile );

                 Boolean shouldBeDownloaded;
#if RECEIVE_UDP_FROM_OURSELF
                shouldBeDownloaded = isFileInCurrentPc;
#else
                 shouldBeDownloaded = !isFileInCurrentPc;
#endif

                 return shouldBeDownloaded;
             } ).ToList();

            ObjectDescriptionModel randomFileToDownload;
            var random = new Random();

            //It will download random file at local PC if you don't have any file to download, 
            //server has any file which isn't deleted and you are only in network
            Boolean canWeDownloadAnything = undeletedObjectsListModel.Length > 0;
            if ( ( bjctDscrptnsFrDwnld.Count == 0 ) && canWeDownloadAnything )
            {
                //TODO add possibility to upload file to server and run this method again
#if RECEIVE_UDP_FROM_OURSELF
                randomFileToDownload = undeletedObjectsListModel[ random.Next( undeletedObjectsListModel.Length ) ];
                await apiClient.DownloadFileAsync( serverBucketName, hexFilePrefix, bucketPath, randomFileToDownload.OriginalName, randomFileToDownload ).ConfigureAwait( false );
#else
                Console.WriteLine( $"You should put few files in {bucketPath} and using WpfClient, upload it" );
                throw new InvalidOperationException();
#endif
            }
            else if ( bjctDscrptnsFrDwnld.Count > 0 )
            {
                randomFileToDownload = bjctDscrptnsFrDwnld[ random.Next( bjctDscrptnsFrDwnld.Count ) ];
            }
            else
            {
                Console.WriteLine( $"You should put few files in {bucketPath} and using WpfClient, upload it" );
                throw new InvalidOperationException();
            }

            return (randomFileToDownload, bucketName, bucketPath, hexFilePrefix);
        }
    }
}
