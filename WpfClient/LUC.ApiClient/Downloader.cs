using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia.Downloads;
using LUC.DiscoveryServices.Kademlia.Exceptions;
using LUC.DiscoveryServices.Messages;
using LUC.Interfaces;
using LUC.Interfaces.Abstract;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Exceptions;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using LUC.Interfaces.Models;

using Serilog;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Constants;

namespace LUC.ApiClient
{
    class Downloader : SyncServicesProvider
    {
        /// <summary>
        /// <a href="http://www.regatta.cs.msu.su/doc/usr/share/man/info/ru_RU/a_doc_lib/aixbman/prftungd/2365c93.htm">
        /// Performance tuning guide
        /// </a>
        /// </summary>
        private const Int32 BUFFER_SIZE_TO_READ_FROM_SERVER = 4096;

        private static TimeSpan s_maxTimeToDownloadChunk = TimeSpan.FromMinutes( value: 2 );

        private readonly DownloaderFromLocalNetwork m_downloaderFromLan;

        private readonly IFileChangesQueue m_fileChangesQueue;

        public Downloader( ApiClient apiClient ) 
            : base( apiClient, apiClient.ObjectNameProvider )
        {
            m_downloaderFromLan = new DownloaderFromLocalNetwork( apiClient.DiscoveryService, IoBehavior.Asynchronous );

            NotifyService = apiClient.NotifyService;

            m_fileChangesQueue = AppSettings.ExportedValue<IFileChangesQueue>();
        }

        public INotifyService NotifyService { get; set; }

        public async Task DownloadFileAsync(
            String serverBucketName,
            String hexFilePrefix,
            String localFolderPath,
            String localOriginalName,
            ObjectDescriptionModel objectDescription,
            CancellationToken cancellationToken = default
        )
        {
            #region Check input data
            if ( String.IsNullOrWhiteSpace(CurrentUserProvider.RootFolderPath))
            {
                return;
            }
            #endregion

            //create directory if it doesn't exist
            _ = DirectoryExtensions.DirectoryForDownloadingTempFiles( CurrentUserProvider.RootFolderPath );

            LoggingService.LogInfo( $"Start download {localOriginalName} with server last modified time {objectDescription.LastModifiedDateTimeUtc.ToLongTimeString()}" );

            String fullLocalFilePath = Path.Combine( localFolderPath, localOriginalName );

            if ( Directory.Exists( fullLocalFilePath ) )
            {
                Log.Warning( $"Cant' download. Directory {fullLocalFilePath} already exists." );
                // TODO Release 2.0 Add UI with conflict names. Ignore downloading at the moment.
                return;
            }

            if ( !Directory.Exists( localFolderPath ) )
            {
                return;
                // TODO Directory.CreateDirectoryOnServer(localFolderPath); // TODO Release 2.0 Do server able to create several prefixes by one request? Also check UnauthorizedAccessException.
            }

            var downloadingFileInfo = objectDescription.ToDownloadingFileInfo( 
                serverBucketName, 
                fullLocalFilePath, 
                hexFilePrefix
            );

            if ( cancellationToken != default )
            {
                downloadingFileInfo.CancellationToken = cancellationToken;
            }

            FileInfo fi = FileInfoHelper.TryGetFileInfo( downloadingFileInfo.LocalFilePath );

            if ( fi != null )
            {
                LoggingService.LogInfo( $"LastWriteTimeUtc of existed one = {fi.LastWriteTimeUtc.ToLongTimeString()}" );
            }
            else
            {
                LoggingService.LogInfo( $"{downloadingFileInfo.LocalFilePath} dosn't exist before download operation. {DateTime.UtcNow.ToLongTimeString()}" );
            }

#if ENABLE_DS_DOWNLOAD_FROM_CURRENT_PC
            String localFilePathInRootFolder = Path.Combine( CurrentUserProvider.RootFolderPath, localOriginalName );

            String fileVersion = AdsExtensions.ReadLastSeenVersion( localFilePathInRootFolder );

            if (!File.Exists( localFilePathInRootFolder ))
            {
                downloadingFileInfo.LocalFilePath = localFilePathInRootFolder;

                await DownloadFileProcessAsync( downloadingFileInfo, objectDescription ).ConfigureAwait( continueOnCapturedContext: false );

                downloadingFileInfo.LocalFilePath = fullLocalFilePath;
            }
            else if (fileVersion != objectDescription.Version)
            {
                AdsExtensions.WriteLastSeenVersion( localFilePathInRootFolder, objectDescription.Version );
            }
#endif

            await DownloadFileInternalAsync( downloadingFileInfo ).ConfigureAwait( continueOnCapturedContext: false );
        }

        internal void DownloadNecessaryChunksFromServer( IEnumerable<ChunkRange> undownloadedChunks, DownloadingFileInfo downloadingFileInfo, String fullFileName )
        {
            downloadingFileInfo.CancellationToken.ThrowIfCancellationRequested();

            if ( File.Exists( fullFileName ) )
            {
                FileExtensions.UpdateAttributesOfBeforeDownloadingFile( fullFileName );
                //we set file length before download, so we don't need to check disk free space(method LucDownloader.VerifyAbilityToDownloadFile)
            }
            else
            {
                VerifyAbilityToDownloadFile( fullFileName, downloadingFileInfo.ByteCount, out String bestPlaceWhereDownloadFile );
                fullFileName = bestPlaceWhereDownloadFile;
            }

            //we don't need to use FileExtensions.FileStreamForDownload, because in this case we will delete all writen bytes
            using ( FileStream fileStream = File.OpenWrite( fullFileName ) )
            {
                FileExtensions.SetAttributesToTempDownloadingFile( fullFileName );

                if ( fileStream.Length != downloadingFileInfo.ByteCount )
                {
                    //set size in order to be sure that we will download all file
                    //(user can fill disk and we cannot have necessary drive space)
                    fileStream.SetLength( downloadingFileInfo.ByteCount );
                }

                Int64 numReadingServerResponse = 0;

                String requestUri = BuildUriExtensions.DownloadUri( m_apiSettings.Host, downloadingFileInfo.ServerBucketName, downloadingFileInfo.FileHexPrefix, downloadingFileInfo.ObjectKey );

                foreach ( ChunkRange chunkRange in undownloadedChunks )
                {
                    Int64 requiredPositionInStream = (Int64)chunkRange.Start;
                    if ( fileStream.Position != requiredPositionInStream )
                    {
                        fileStream.Seek( requiredPositionInStream, SeekOrigin.Begin );
                    }

                    var webRequest = HttpWebRequest.Create( requestUri ) as HttpWebRequest;
                    ConfigureRequestWithDefaultValues( webRequest );
                    webRequest.AddRange( (Int64)chunkRange.Start, (Int64)chunkRange.End );

                    downloadingFileInfo.CancellationToken.ThrowIfCancellationRequested();

                    WebResponse webResponse = webRequest.GetResponse();
                    using ( Stream responseStream = webResponse.GetResponseStream() )
                    {
                        WriteResponseBytes(
                            fileStream,
                            responseStream,
                            ref numReadingServerResponse,
                            cancellationToken: downloadingFileInfo.CancellationToken,
                            logPartOfDownloadWithWatchUpdate: ( numReading, watch ) =>
                            {
                                LogPartOfDownloadWithWatchUpdate(
                                    numReading,
                                    BUFFER_SIZE_TO_READ_FROM_SERVER,
                                    downloadingFileInfo.ByteCount,
                                    fullFileName,
                                    ref watch
                                );

                                return watch;
                            }
                         );
                    }
                }
            }
        }

        internal IEnumerable<ChunkRange> JoinedChunkRanges(IEnumerable<ChunkRange> chunkRanges)
        {
            IEnumerable<ChunkRange> sortedRanges = chunkRanges.OrderBy( c => c.Start );
            UInt64 start = 0;

            IEnumerator<ChunkRange> enumerator = sortedRanges.GetEnumerator();
            if( enumerator.MoveNext())
            {
                ChunkRange previous = enumerator.Current;

                while ( enumerator.MoveNext() )
                {
                    if ( previous.End != enumerator.Current.Start - 1 )
                    {
                        //every chunk has the same Total property value
                        yield return new ChunkRange( start, previous.End, previous.Total );
                        start = enumerator.Current.Start;
                    }

                    previous = enumerator.Current;
                }

                yield return new ChunkRange( start, previous.End, previous.Total );
            }
        }

        private async Task DownloadFileInternalAsync( DownloadingFileInfo downloadingFileInfo )
        {
            SyncingObjectsList.AddDownloadingFile( downloadingFileInfo );

            try
            {
                DateTime startTimeOfDownload = DateTime.UtcNow;

                try
                {
                    AdsExtensions.WriteThatDownloadProcessIsStarted( downloadingFileInfo.PathWhereDownloadFileFirst );
                    await m_downloaderFromLan.DownloadFileAsync( downloadingFileInfo, m_fileChangesQueue ).ConfigureAwait( continueOnCapturedContext: false );
                }
                //when DS (DiscoveryService) doesn't find any node or node with required file
                catch ( InvalidOperationException )
                {
                    DownloadFullFileFromServer( downloadingFileInfo );
                }
                catch ( FilePartiallyDownloadedException ex )
                {
                    //user can move or delete file
                    if ( File.Exists( ex.TempFullFileName ) )
                    {
                        //check whether file is changed on server
                        await CheckFileChangedOnServerAsync( downloadingFileInfo ).ConfigureAwait( false );

                        //if file on server doesn't changed, then finish download from server
                        Single rateOfUndownloadedSize = ex.UndownloadedRanges.Select( c => (Int64)( c.End - c.Start ) ).Sum() / (Single)downloadingFileInfo.ByteCount;
                        List<ChunkRange> joinedChunkRanges = JoinedChunkRanges( ex.UndownloadedRanges ).ToList();

                        Int32 equatorOfFileSize = 400 * GeneralConstants.BYTES_IN_ONE_MEGABYTE;

                        //const values are received after some investigations 
                        if ( ( 0.1f < rateOfUndownloadedSize ) ||
                             ( joinedChunkRanges.Count <= 2 ) ||
                             ( ( rateOfUndownloadedSize <= 0.7f ) && ( joinedChunkRanges.Count <= DsConstants.MAX_THREADS ) && 80 * GeneralConstants.BYTES_IN_ONE_MEGABYTE <= downloadingFileInfo.ByteCount && downloadingFileInfo.ByteCount <= equatorOfFileSize ) ||
                               ( ( joinedChunkRanges.Count <= 5 ) && ( rateOfUndownloadedSize <= 0.7f ) && ( equatorOfFileSize < downloadingFileInfo.ByteCount ) ) )
                        {
                            DownloadNecessaryChunksFromServer(
                                ex.UndownloadedRanges,
                                downloadingFileInfo,
                                downloadingFileInfo.PathWhereDownloadFileFirst
                            );

                            FileExtensions.SetDownloadedFileToNormal( downloadingFileInfo, m_fileChangesQueue );
                        }
                        else
                        {
                            DownloadFullFileFromServer( downloadingFileInfo );
                        }
                    }
                    else
                    {
                        DownloadFullFileFromServer( downloadingFileInfo );
                    }
                }
                catch ( OperationCanceledException )
                {
                    DeleteUndownloadedFile( downloadingFileInfo );
                    throw;
                }
                catch ( IOException ex )
                {
                    DeleteUndownloadedFile( downloadingFileInfo );

                    NotifyService.Notify( new NotificationResult
                    {
                        IsSuccess = false,
                        Message = ex.Message
                    } );

                    throw;
                }

                DateTime endTimeOfDownload = DateTime.UtcNow;
                LoggingService.LogInfo( logRecord: $"Download process time: {endTimeOfDownload - startTimeOfDownload} for file with size " +
                    $"{downloadingFileInfo.ByteCount / (Double)GeneralConstants.BYTES_IN_ONE_MEGABYTE} MB" );

                HandleDownloadedFile( downloadingFileInfo );
            }
            catch ( Exception ex )
            {
                CancelDownloadFile( downloadingFileInfo );

                LoggingService.LogCriticalError( ex );
            }
        }

        private async Task CheckFileChangedOnServerAsync( DownloadingFileInfo downloadingFileInfo )
        {
            await ApiClient.ListWithCancelDownloadAsync( downloadingFileInfo.ServerBucketName, downloadingFileInfo.FileHexPrefix, showDeleted: true ).ConfigureAwait( continueOnCapturedContext: false );
            downloadingFileInfo.CancellationToken.ThrowIfCancellationRequested();
        }

        private WebRequest WebRequestWithoutContentRange( String requestUri )
        {
            var request = WebRequest.Create( requestUri );
            ConfigureRequestWithDefaultValues( request );

            return request;
        }

        private void ConfigureRequestWithDefaultValues( WebRequest request )
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            request.Headers.Add( HttpRequestHeader.Authorization, "Token " + m_apiSettings.AccessToken );
            request.Method = "GET";
        }

        private void DownloadFullFileFromServer(
            DownloadingFileInfo downloadingFileInfo
        )
        {
            String requestUri = BuildUriExtensions.DownloadUri( m_apiSettings.Host, downloadingFileInfo.ServerBucketName, downloadingFileInfo.FileHexPrefix, downloadingFileInfo.ObjectKey );

            // TODO Release 2.0 Track last downloaded chunk. if internet is off -> next download should start from last. See Seek method.
            DateTime startDownloadTime = DateTime.UtcNow;
            WebRequest request = WebRequestWithoutContentRange( requestUri );

            downloadingFileInfo.CancellationToken.ThrowIfCancellationRequested();

            WebResponse response = request.GetResponse();

            Int64 numReadingServerResponse = 0;

            using ( Stream responseStream = response.GetResponseStream() )
            {
                downloadingFileInfo.CancellationToken.ThrowIfCancellationRequested();

                try
                {
                    VerifyAbilityToDownloadFile( downloadingFileInfo.PathWhereDownloadFileFirst, downloadingFileInfo.ByteCount, out String bestPlaceWhereDownloadFile );
                    downloadingFileInfo.PathWhereDownloadFileFirst = bestPlaceWhereDownloadFile;

                    using ( FileStream fileStream = FileExtensions.FileStreamForDownload( downloadingFileInfo.PathWhereDownloadFileFirst ) )
                    {
                        fileStream.SetLength( downloadingFileInfo.ByteCount );

                        WriteResponseBytes(
                            fileStream,
                            responseStream,
                            ref numReadingServerResponse,
                            cancellationToken: downloadingFileInfo.CancellationToken,
                            logPartOfDownloadWithWatchUpdate: ( numReading, watch ) =>
                            {
                                LogPartOfDownloadWithWatchUpdate( numReading, BUFFER_SIZE_TO_READ_FROM_SERVER, downloadingFileInfo.ByteCount, downloadingFileInfo.PathWhereDownloadFileFirst, ref watch );
                                return watch;
                            }
                         );
                    }

                    FileExtensions.SetDownloadedFileToNormal( downloadingFileInfo, m_fileChangesQueue );
                }
                catch ( IOException ex )
                {
                    NotifyService.Notify( new NotificationResult
                    {
                        IsSuccess = false,
                        Message = ex.Message
                    } );

                    throw;
                }
            }
        }

        private void VerifyAbilityToDownloadFile( String fullFileName, Int64 bytesCountOfFile, out String bestPlaceWhereDownloadFile ) =>
            m_downloaderFromLan.VerifyAbilityToDownloadFile( fullFileName, bytesCountOfFile, out bestPlaceWhereDownloadFile );

        private void WriteResponseBytes(
            FileStream fileStream,
            Stream responseStream,
            ref Int64 numReading,
            Int32 countOfBytesToReadPerTime = BUFFER_SIZE_TO_READ_FROM_SERVER,
            CancellationToken cancellationToken = default,
            Func<Int64, Stopwatch, Stopwatch> logPartOfDownloadWithWatchUpdate = null
        )
        {
            Byte[] buffer = new Byte[ countOfBytesToReadPerTime ];
            Int32 bytesRead = responseStream.Read( buffer, offset: 0, countOfBytesToReadPerTime );

#if DEBUG
            var watch = Stopwatch.StartNew();
#endif

            String fullLocalFilePath = fileStream.Name;

            while ( bytesRead > 0 )
            {
                cancellationToken.ThrowIfCancellationRequested();

                numReading++;
                fileStream.Write( buffer, offset: 0, bytesRead );

                DateTime startDownloadChunkTime = DateTime.UtcNow;
                bytesRead = responseStream.Read( buffer, 0, countOfBytesToReadPerTime );
                DateTime endDownloadChunkTime = DateTime.UtcNow;

                if ( endDownloadChunkTime - startDownloadChunkTime > s_maxTimeToDownloadChunk )
                {
                    throw new InconsistencyException( message: $"During downloading of file {fullLocalFilePath} 4096 bytes was read more 5 minutes." );
                }

                if ( logPartOfDownloadWithWatchUpdate != null )
                {
#if DEBUG
                    watch = logPartOfDownloadWithWatchUpdate( numReading, watch );
#else
                    logPartOfDownloadWithWatchUpdate(numReading, null);
#endif
                }
            }

#if DEBUG
            watch.Stop();
#endif
        }

        private void LogPartOfDownloadWithWatchUpdate(
            Int64 numReading,
            Int32 countOfBytesToReadPerTime,
            Double fullFileSize,
            String fullLocalFilePath,
            ref Stopwatch watch
        )
        {
            Int32 howOftenExecuteLog = 3000;

            if ( numReading % howOftenExecuteLog == 0 )
            {
                Int64 bytesProceeded = howOftenExecuteLog * countOfBytesToReadPerTime;
                Double percents = countOfBytesToReadPerTime * numReading / fullFileSize * 100;

                String localOriginalName = Path.GetFileName( fullLocalFilePath );
                LoggingService.LogInfo( $"    {localOriginalName}: downloading {percents.ToString( "0.00", CultureInfo.InvariantCulture )}%. Now is {DateTime.UtcNow.ToLongTimeString()}" );

#if DEBUG
                watch.Stop();
                Int64 elapsedMs = watch.ElapsedMilliseconds;

                if ( elapsedMs >= 100 )
                {
                    Log.Debug( Speed.ToString( bytesProceeded, elapsedMs ) );
                }

                watch = Stopwatch.StartNew();
#endif
            }
        }

        private void HandleDownloadedFile(
            DownloadingFileInfo downloadingFileInfo
        )
        {
            FileInfo realFileInfo = FileInfoHelper.TryGetFileInfo( downloadingFileInfo.LocalFilePath );

            if ( realFileInfo == null )
            {
                String errorMessage = String.Format( FileInfoHelper.ERROR_DESCRIPTION, downloadingFileInfo.LocalFilePath ) + " during DownloadFileAsync";
                LoggingService.LogFatal( errorMessage );
            }
            else if ( realFileInfo.Length == downloadingFileInfo.ByteCount )
            {
                try
                {
                    File.SetCreationTimeUtc( realFileInfo.FullName, downloadingFileInfo.LastModifiedDateTimeUtc );
                    File.SetLastWriteTimeUtc( realFileInfo.FullName, downloadingFileInfo.LastModifiedDateTimeUtc );
                }
                catch ( Exception ex )
                {
                    Log.Error( ex, ex.Message );
                    //ignore. It is not very important whether time was set, because it isn't used in code
                }

                try
                {
                    AdsExtensions.WriteInfoAboutNewFileVersion( realFileInfo, downloadingFileInfo.Version, downloadingFileInfo.Guid );
                }
                catch ( Exception ex )
                {
                    LoggingService.LogError( ex, logRecord: $"Cannot write version ({downloadingFileInfo.Version}) or guid ({downloadingFileInfo.Guid}) for {downloadingFileInfo.LocalFilePath}" );
                }

                LoggingService.LogInfo( $"{downloadingFileInfo.LocalFilePath} was downloaded." );

                downloadingFileInfo.CancellationToken.ThrowIfCancellationRequested();

                downloadingFileInfo.IsDownloaded = true;
                LoggingService.LogInfo( $"IsDownloaded = true => {DateTime.UtcNow.ToLongTimeString()} => {downloadingFileInfo.LocalFilePath}" );

                //we don't write in ADS that file is downloaded, because we need it only when file wasn't moved to required bucket folder
            }
            else
            {
                LoggingService.LogFatal( $"File '{downloadingFileInfo.LocalFilePath}' was not downloaded. realFileInfo.Length != downloadingFileInfo.ExpectedBytes" );

                if ( SyncingObjectsList.TryDeleteFile( downloadingFileInfo.LocalFilePath ) )
                {
                    Log.Warning( $"File {downloadingFileInfo.LocalFilePath} was deleted by client because realFileInfo.Length != downloadingFileInfo.ExpectedBytes" );
                }
            }

            if ( realFileInfo != null )
            {
                SyncingObjectsList.RemoveDownloadingFile( downloadingFileInfo );
            }
        }

        /// <summary>
        /// Deletes undownloaded file if <seealso cref="DownloadingFileInfo.LocalFilePath"/> is equal to <seealso cref="DownloadingFileInfo.PathWhereDownloadFileFirst"/>
        /// </summary>
        /// <param name="downloadingFileInfo"></param>
        private void DeleteUndownloadedFile( DownloadingFileInfo downloadingFileInfo )
        {
            ObjectStateType objectStateType = PathExtensions.GetObjectState( downloadingFileInfo.LocalFilePath );

            //we should delete file only if it is downloaded in root folder
            if ( downloadingFileInfo.PathWhereDownloadFileFirst.IsEqualFilePathesInCurrentOs( downloadingFileInfo.LocalFilePath ) &&
                ( objectStateType != ObjectStateType.Locked ) &&
                ( objectStateType != ObjectStateType.Deleted ) )
            {
                try
                {
                    FileExtensions.UpdateAttributesOfBeforeDownloadingFile( downloadingFileInfo.PathWhereDownloadFileFirst );
                    SyncingObjectsList.TryDeleteFile( downloadingFileInfo.PathWhereDownloadFileFirst );
                }
                catch ( IOException )
                {
                    ;//do nothing
                }
            }
        }

        private void CancelDownloadFile( DownloadingFileInfo downloadingFileInfo )
        {
            FileExtensions.UpdateAttributesOfBeforeDownloadingFile( downloadingFileInfo.PathWhereDownloadFileFirst );
            SyncingObjectsList.TryDeleteFile( downloadingFileInfo.PathWhereDownloadFileFirst );

            downloadingFileInfo.IsDownloaded = true;
            SyncingObjectsList.RemoveDownloadingFile( downloadingFileInfo );
        }
    }
}