using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.Downloads;
using LUC.DiscoveryService.Test.Extensions;
using LUC.Interfaces;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

namespace LUC.DiscoveryService.Test.FunctionalTests
{
    partial class FunctionalTest
    {
        /// <summary>
        /// Downloads random file which isn't in current PC if you aren't only in network. Otherwise it will download file which is
        /// </summary>
        private async static Task DownloadRandomFileAsync( IApiClient apiClient,
            ICurrentUserProvider currentUserProvider, Contact remoteContact )
        {
            Download download = new Download( s_discoveryService, IOBehavior.Asynchronous );
            download.FileDownloaded += OnFileDownloaded;

            String filePrefix = UserIntersectionInConsole.ValidValueInputtedByUser( requestToUser: "Input file prefix where new file can exist on the server: ", ( userInput ) =>
            {
                Boolean isValidInput = ( Extensions.PathExtensions.IsValidPath( userInput ) ) && ( !userInput.Contains( ":" ) );
                return isValidInput;
            } );

            (ObjectDescriptionModel fileDescription, String bucketName, String localFolderPath) = await RandomFileToDownloadAsync( apiClient, currentUserProvider, remoteContact, filePrefix ).ConfigureAwait( false );

            lock( UserIntersectionInConsole.Lock)
            {
                Console.WriteLine( "Press any key to start download process. \n" +
                "After that if you want to cancel download, press C key. If you do not want to cancel, press any other button." );
                Console.ReadKey( intercept: true );//true says that pressed key will not show in console

                ConfiguredTaskAwaitable downloadTask = Task.Run( async () =>
                {
                    await download.DownloadFileAsync(
                        localFolderPath,
                        bucketName,
                        filePrefix,
                        fileDescription.OriginalName,
                        fileDescription.Bytes,
                        fileDescription.Version,
                        s_cancellationTokenSource.Token
                    ).ConfigureAwait( false );
                } ).ConfigureAwait( false );

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

        private static void OnFileDownloaded(Object sender, FileDownloadedEventArgs eventArgs)
        {
            Console.WriteLine( "If you have not pressed any button, do so to continue testing" );
            AdsExtensions.TryWriteLastSeenVersion( eventArgs.FullFileName, eventArgs.Version );
        }

        private async static Task<(ObjectDescriptionModel randomFileToDownload, String serverBucketName, String localFolderPath)> RandomFileToDownloadAsync( IApiClient apiClient, ICurrentUserProvider currentUserProvider, Contact remoteContact, String filePrefix )
        {
            IList<String> bucketDirectoryPathes = currentUserProvider.ProvideBucketDirectoryPathes();

            String bucketPath = null;
            UserIntersectionInConsole.ValidValueInputtedByUser(
                requestToUser: "Input bucket directory path or it name: ",
                ( userInput ) =>
                {
                    bucketPath = bucketDirectoryPathes.SingleOrDefault( ( path ) => path.Contains( userInput ) );
                    return bucketPath != null;
                }
            );

            String serverBucketName = currentUserProvider.GetBucketNameByDirectoryPath( bucketPath ).ServerName;

            ObjectsListResponse objectsListResponse = await apiClient.ListAsync( serverBucketName, filePrefix ).ConfigureAwait( continueOnCapturedContext: false );
            ObjectsListModel objectsListModel = objectsListResponse.ToObjectsListModel();
            ObjectDescriptionModel[] undeletedObjectsListModel = objectsListModel.ObjectDescriptions.Where( c => !c.IsDeleted ).ToArray();

            //select from undeletedObjectsListModel files which exist in current PC if tester is only in network. 
            //If the last one is not, select files which don't exist in current PC
            List<ObjectDescriptionModel> bjctDscrptnsFrDwnld = undeletedObjectsListModel.Where( cachedFileInServer =>
            {
                String fullPathToFile = Path.Combine( bucketPath, filePrefix, cachedFileInServer.OriginalName );
                Boolean isFileInCurrentPc = File.Exists( fullPathToFile );

                Boolean shouldBeDownloaded;
#if RECEIVE_TCP_FROM_OURSELF
                shouldBeDownloaded = isFileInCurrentPc;
#else
                 shouldBeDownloaded = !isFileInCurrentPc;
#endif

                return shouldBeDownloaded;
            } ).ToList();

            ObjectDescriptionModel randomFileToDownload;
            Random random = new Random();

            //It will download random file at local PC if you don't have any file to download, 
            //server has any file which isn't deleted and you are only in network
            Boolean canWeDownloadAnything = undeletedObjectsListModel.Length > 0;
            if ( ( bjctDscrptnsFrDwnld.Count == 0 ) && ( canWeDownloadAnything ) )
            {
                //TODO add possibility to upload file to server and run this method again
#if RECEIVE_TCP_FROM_OURSELF
                randomFileToDownload = undeletedObjectsListModel[ random.Next( undeletedObjectsListModel.Length ) ];
                await apiClient.DownloadFileAsync( serverBucketName, filePrefix, bucketPath, randomFileToDownload.OriginalName, randomFileToDownload ).ConfigureAwait( false );
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

            return (randomFileToDownload, serverBucketName, bucketPath);
        }
    }
}
