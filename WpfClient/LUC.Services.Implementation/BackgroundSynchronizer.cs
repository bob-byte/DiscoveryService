using LUC.Common.PrismEvents;
using LUC.Globalization;
using LUC.Interfaces;
using LUC.Interfaces.Abstract;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Exceptions;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

using Prism.Events;

using Serilog;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Threading;
using Nito.AsyncEx;
using System.Windows;

namespace LUC.Services.Implementation
{
    [Export( typeof( IBackgroundSynchronizer ) )]
    public partial class BackgroundSynchronizer : WebRestorable, IBackgroundSynchronizer
    {
        #region Constants

        public const Int32 ADDED_LENGTH_TO_DELETED_NAME = 19;

        #endregion

        #region Fields

        private static TimeSpan s_timeWaitCheckSyncToServerStopped = TimeSpan.FromSeconds( value: 1 );

        private Boolean m_isTickSyncFromServerStarted;
        private Boolean m_isSyncStopped;
        private static DispatcherTimer s_timerSyncFromServer;

        private readonly IEventAggregator m_eventAggregator;
        private readonly IPathFiltrator m_pathFiltrator;
        private CancellationTokenSource m_cancelSyncToServer;


        #endregion

        #region Constructors

        [ImportingConstructor]
        public BackgroundSynchronizer( IEventAggregator eventAggregator, IPathFiltrator pathFiltrator )
        {
            StopOperation = StopAllSync;
            RerunOperation = () => RunPeriodicSyncFromServer( whetherRunImmediatelySyncProcess: true );

            m_eventAggregator = eventAggregator;
            m_pathFiltrator = pathFiltrator;

            _ = eventAggregator.GetEvent<IsSyncToServerChangedEvent>().Subscribe( param => IsSyncToServerNow = param );

            LockNotifyAndCheckSyncStart = new AsyncLock();
        }

        #endregion

        #region Properties

        [Import( typeof( ISyncingObjectsList ) )]
        public ISyncingObjectsList SyncingObjectsList { get; set; }

        [Import( typeof( IApiClient ) )]
        public IApiClient ApiClient { get; set; }

        [Import( typeof( ICurrentUserProvider ) )]
        public ICurrentUserProvider CurrentUserProvider { get; set; }

        [Import( typeof( INotifyService ) )]
        public INotifyService NotifyService { get; set; }

        [Import( typeof( ILoggingService ) )]
        public ILoggingService LoggingService { get; set; }

        [Import( typeof( IFileChangesQueue ) )]
        public IFileChangesQueue FileChangesQueue { get; set; }

        [Import( typeof( ISettingsService ) )]
        public ISettingsService SettingsService { get; set; }

        [Import( typeof( INavigationManager ) )]
        public INavigationManager NavigationManager { get; set; }

        public AsyncLock LockNotifyAndCheckSyncStart { get; }

        public Boolean IsSyncToServerNow { get; private set; }

        public CancellationTokenSource SourceToCancelSyncToServer => m_cancelSyncToServer;

        protected override Action StopOperation { get; }

        protected override Action RerunOperation { get; }

        Boolean IBackgroundSynchronizer.IsTickSyncFromServerStarted => m_isTickSyncFromServerStarted;
        DispatcherTimer IBackgroundSynchronizer.TimerSyncFromServer => s_timerSyncFromServer;

        #endregion

        #region Methods

        public async Task RunAsync()
        {
            ResetSyncToServerCancellation();

            m_isSyncStopped = false;
            try
            {
                await RunOnceSyncToServer();
            }
            catch ( OperationCanceledException ex )
            {
                LoggingService.LogCriticalError( ex );
            }

            RunPeriodicSyncFromServer();
        }

        // TODO Release 2.0 Should be events from server about changes.
        public void RunPeriodicSyncFromServer( Boolean whetherRunImmediatelySyncProcess = true )
        {
            m_isSyncStopped = false;
            String intervalSyncInMin = ConfigurationManager.AppSettings[ "SyncFromServerIntervalInMinutes" ];

            if ( !Int32.TryParse( intervalSyncInMin, out Int32 parsedIntervalSyncInMin ) )
            {
                throw new ArgumentNullException( String.Empty, @"SyncFromServerIntervalInMinutes Can't find in config." );
            }

            LoggingService.LogInfo( $@"SyncFromServerIntervalInMinutes = {parsedIntervalSyncInMin}" );

            StartTimer( TimeSpan.FromMinutes( parsedIntervalSyncInMin ), TickSyncFromServer, ref s_timerSyncFromServer );
            
            if ( whetherRunImmediatelySyncProcess )
            {
                TrySyncAllFromServerAsync().ConfigureAwait( continueOnCapturedContext: false );
            }
        }

        private void StartTimer( TimeSpan interval, EventHandler methodToCallEachTick, ref DispatcherTimer timer )
        {
            if ( timer == null )
            {
                timer = new DispatcherTimer
                {
                    Interval = interval,
                    IsEnabled = true
                };
            }

            timer.Tick += methodToCallEachTick;
            timer.Start();
        }

        private async Task RunOnceSyncToServer()
        {
            await Task.Run(
                async () =>
                {
                    using ( await LockNotifyAndCheckSyncStart.LockAsync().ConfigureAwait( continueOnCapturedContext: false ) )
                    {
                        while ( IsSyncToServerNow )
                        {
                            await Task.Delay( s_timeWaitCheckSyncToServerStopped ).ConfigureAwait( false );
                        }

                        LoggingService.LogInfo( "SyncToServer started..." );

                        m_eventAggregator.GetEvent<IsSyncToServerChangedEvent>().Publish( payload: true );
                    }

                    DateTime lastSyncDateTime = SettingsService.ReadLastSyncDateTime();

                    List<String> subDirectories = GetValidBucketDirectories();

                    try
                    {
                        foreach ( String bucketDirectory in subDirectories )
                        {
                            IBucketName bucketName = CurrentUserProvider.GetBucketNameByDirectoryPath( bucketDirectory );

                            if ( bucketName.IsSuccess )
                            {
                                try
                                {
                                    await SyncToServer(
                                        bucketDirectory,
                                        bucketName,
                                        hexPrefix: String.Empty,
                                        lastSyncDateTime,
                                        SourceToCancelSyncToServer.Token,
                                        previousUserSelectResults: new Dictionary<String, MessageBoxResult>()
                                    ).ConfigureAwait( false );
                                }
                                catch ( OperationCanceledException )
                                {
                                    break;
                                }
                                catch ( Exception ex )
                                {
                                    LoggingService.LogCriticalError( ex );
                                }
                            }
                        }
                    }
                    catch ( Exception ex )
                    {
                        LoggingService.LogCriticalError( ex );
                    }
                    finally
                    {
                        m_eventAggregator.GetEvent<IsSyncToServerChangedEvent>().Publish( false );
                        LoggingService.LogInfo( "...finished SyncToServer" );
                    }
                }, SourceToCancelSyncToServer.Token );
        }

        ///<inheritdoc/>
        public void StopAllSync()
        {
            SourceToCancelSyncToServer.Cancel();
            StopSyncFromServer();
        }

        public void StopSyncFromServer()
        {
            //boolean value can be changed in any tick, so we shouldn't pass m_isTickSyncFromServerStarted
            //in m_eventAggregator.GetEvent<IsSyncFromServerChangedEvent>().Publish
            m_isTickSyncFromServerStarted = false;
            m_eventAggregator.GetEvent<IsSyncFromServerChangedEvent>().Publish( payload: false );

            m_isSyncStopped = true;

            if ( s_timerSyncFromServer != null )
            {
                s_timerSyncFromServer.Tick -= TickSyncFromServer;
                s_timerSyncFromServer.Stop();
                s_timerSyncFromServer.IsEnabled = false;
                m_isTickSyncFromServerStarted = false;
            }

            SyncingObjectsList.Clear();

            LoggingService.LogInfo( "Sync is stopped." );
        }

        private async void TickSyncFromServer( Object sender, EventArgs e )
        {
            try
            {
                await TrySyncAllFromServerAsync();
            }
            catch ( Exception ex )
            {
                LoggingService.LogError( ex, ex.Message );

                StopSyncFromServer();
                RunPeriodicSyncFromServer( whetherRunImmediatelySyncProcess: false );
            }
        }

        public async Task TrySyncAllFromServerAsync()
        {
            Boolean shouldSyncAllFromServer;

            using ( await LockNotifyAndCheckSyncStart.LockAsync().ConfigureAwait( continueOnCapturedContext: false ) )
            {
                if ( !m_isTickSyncFromServerStarted && !IsSyncToServerNow )
                {
                    m_isSyncStopped = false;

                    //boolean value can be changed in any tick, so we shouldn't pass m_isTickSyncFromServerStarted
                    //in m_eventAggregator.GetEvent<IsSyncFromServerChangedEvent>().Publish
                    m_isTickSyncFromServerStarted = true;
                    m_eventAggregator.GetEvent<IsSyncFromServerChangedEvent>().Publish( payload: true );

                    shouldSyncAllFromServer = true;
                }
                else
                {
                    shouldSyncAllFromServer = false;
                    Log.Warning( "Is not allow start method SyncAllFromServer." );
                }
            }

            if ( shouldSyncAllFromServer )
            {
                await Task.Run( SyncAllFromServer ).ConfigureAwait( false );
            }
        }

        private void ResetSyncToServerCancellation()
        {
            //thread-safe reset m_cancelSyncToServer.IsCancellationRequested to false
            //TODO: dispose previous m_cancelSyncToServer value
            Interlocked.Exchange( ref m_cancelSyncToServer, value: new CancellationTokenSource() );
        }

        // Info: User may create custom folders inside root folder. We do not need them.
        private List<String> GetValidBucketDirectories()
        {
            var subDirectories = Directory.GetDirectories( CurrentUserProvider.RootFolderPath ).ToList();

            var result = new List<String>();

            foreach ( String subDirectory in subDirectories )
            {
                String directoryName = new DirectoryInfo( subDirectory ).Name;
                //Здесь фильтруются подпапки, синхронизируются только group1, group2, group3.
                //TODO: Удалить в финальном варианте
                if ( CurrentUserProvider.LoggedUser.Groups.Any( group => group.Name.ToLowerInvariant() == directoryName.ToLowerInvariant() ) )
                {
                    result.Add( subDirectory );
                }
            }

            return result;
        }

        private async Task SyncAllFromServer()
        {
            var checkServerEventArgsCollection = new CheckServerChangesEventArgsCollection();
            var period = TimeSpan.FromMinutes( value: 0.5 );

            var checkFileOnServerTimer = new Timer( CheckFilesOnServerAsync, checkServerEventArgsCollection, dueTime: period, period );

            try
            {
                await FileChangesQueue.HandleLockedFilesAsync();
                await FileChangesQueue.HandleDownloadedNotMovedFilesAsync().ConfigureAwait( continueOnCapturedContext: false );

#if DEBUG
                Log.Debug( $"{nameof( IsSyncFromServerChangedEvent )} = {m_isTickSyncFromServerStarted}" );
#endif

                LoggingService.LogInfo( DateTime.UtcNow.ToLongTimeString() + " Sync from server started..." );

                Boolean shouldSyncBeStopped = false;

                do
                {
                    List<String> subDirectories = GetValidBucketDirectories();

                    //set in true to don't create perpetual synchronization when subDirectories.Count == 0
                    Boolean isSuccessSync = true;

                    foreach ( String bucketDirectory in subDirectories )
                    {
                        IBucketName bucketName = CurrentUserProvider.GetBucketNameByDirectoryPath( bucketDirectory );

                        if ( !bucketName.IsSuccess )
                        {
                            continue;
                        }

                        //if we need to update UI, then we will delete ConfigureAwait( continueOnCapturedContext: false )
                        isSuccessSync = await SyncDirectoryFromServerAsync( bucketName, String.Empty, bucketDirectory, checkServerEventArgsCollection );

                        if ( !isSuccessSync )
                        {
                            shouldSyncBeStopped = m_isSyncStopped;
                            if ( shouldSyncBeStopped )
                            {
                                break;
                            }
                        }
                    }

                    checkServerEventArgsCollection.Clear();

                    if ( isSuccessSync )
                    {
                        shouldSyncBeStopped = true;
                    }
                }
                while ( !shouldSyncBeStopped );
            }
            catch ( Exception ex )
            {
                LoggingService.LogCriticalError( ex );

                StopSyncFromServer();
                RunPeriodicSyncFromServer( whetherRunImmediatelySyncProcess: false );
            }
            finally
            {
                //In order not to load the server
                checkFileOnServerTimer.Dispose();

                try
                {
                    SettingsService.WriteLastSyncDateTime();
                }
                catch ( InvalidOperationException )
                {
                    LoggingService.LogError( "Can't save LastSyncDateTime to file" );
                }

                m_isTickSyncFromServerStarted = false;

                m_eventAggregator.GetEvent<IsSyncFromServerChangedEvent>().Publish( m_isTickSyncFromServerStarted );

#if DEBUG
                Log.Debug( $"{nameof( IsSyncFromServerChangedEvent )} = {m_isTickSyncFromServerStarted}" );
#endif

                LoggingService.LogInfo( "...finished sync from server " + DateTime.UtcNow.ToLongTimeString() );
            }
        }

        // The method executes from time to time.
        // Return indication that sync process got IsForbidden response from server.
        private async Task<Boolean> SyncDirectoryFromServerAsync( IBucketName bucketName, String hexPrefix, String bucketDirectory, CheckServerChangesEventArgsCollection checkServerEventArgsCollection )
        {
            if ( m_isSyncStopped )
            {
                LoggingService.LogInfo( "Sync is stopped." );
                return false;
            }

            if ( !bucketDirectory.Contains( bucketName.LocalName ) )
            {
                String errorMessage = $"Bucket directory '{bucketDirectory}' does not contain bucket name '{bucketName.LocalName}'";
                var inconsistencyException = new InconsistencyException( errorMessage );
                LoggingService.LogCriticalError( inconsistencyException );

                throw inconsistencyException;
            }

            String relatedToPrefixFolder = String.IsNullOrEmpty( hexPrefix ) ?
                bucketDirectory :
                Path.Combine( bucketDirectory, hexPrefix.FromHexString() );

            if ( relatedToPrefixFolder.IsJunctionDirectory() || !m_pathFiltrator.IsPathPertinent( relatedToPrefixFolder ) )
            {
                return false;
            }

            LoggingService.LogInfo( $"    Syncing from {relatedToPrefixFolder} ..." );

            // Create directory which are on server side, but still absent on client side
            if ( !Directory.Exists( relatedToPrefixFolder ) )
            {
                if ( File.Exists( relatedToPrefixFolder ) )
                {
                    return false;
                    // TODO Release 2.0 Later will be separate UI for conflicting objects. Now we ignoring
                }

                //TODO check whether FileChangesQueue contains deleted folder
                FileChangesQueue.TryGetEventArgs( relatedToPrefixFolder, out Boolean existsInQueue, out FileSystemEventArgs eventArgs );

                if ( existsInQueue && ( eventArgs.ChangeType == WatcherChangeTypes.Deleted ) )
                {
                    return false;
                }
                else
                {
                    SyncingObjectsList.AddCreatingDirectory( relatedToPrefixFolder );

                    //directory deletion from SyncingObjectsList is in FileSystemFacade.Watcher_Created
                    Directory.CreateDirectory( relatedToPrefixFolder );
                    var di = new DirectoryInfo( relatedToPrefixFolder );

                    //remove read-only attribute
                    di.Attributes &= ~FileAttributes.ReadOnly;
                }                
            }
            else  // If directory is present when remove read-only attribute
            {
                var di = new DirectoryInfo( relatedToPrefixFolder );
                if ( di.Exists && di.Attributes.HasFlag( FileAttributes.ReadOnly ) )
                {
                    di.Attributes &= ~FileAttributes.ReadOnly;
                }
            }

            ObjectsListResponse listResponse = await ApiClient.ListAsync( bucketName.ServerName, hexPrefix, true );

            if ( !listResponse.IsSuccess )
            {
                LoggingService.LogError( listResponse.Message );

                return listResponse.IsForbidden;
            }

            checkServerEventArgsCollection.Add( new CheckServerChangesEventArgs( bucketName.ServerName, hexPrefix ) );

            var listModel = listResponse.ToObjectsListModel();

            //  NOTE: This code compares datetime from server and client

            //if ( !IsValidTimeDifferenceWithServer( listModel.ServerUtc ) )
            //{
            //    String message = "Can't sync from server. Local time is not relevant to time on server.";
            //    Log.Warning( message );
            //    StopSyncFromServer();
            //    NavigationManager.NavigateToLoginView();
            //    MessageBoxHelper.ShowMessageBox( String.Format( Strings.MessageTemplate_ServerAndLocalTimeIsDifferent, DateTime.UtcNow.ToLongTimeString(), listModel.ServerUtc.ToLongTimeString() ), Strings.Label_Attention );
            //    return false;
            //}

            // Download files which are on server side, but still absent on client side.
            // Delete files which are deleted on server side, but still exists on client side.
            foreach ( ObjectDescriptionModel serverObjectDescr in listModel.ObjectDescriptions )
            {
                if ( m_isSyncStopped )
                {
                    return false;
                }

                String possibleLocalFilePath = Path.Combine( relatedToPrefixFolder, serverObjectDescr.OriginalName );

                if ( FileChangesQueue.IsPathAvaliableInActiveList( possibleLocalFilePath ) )
                {
                    continue;
                }

                try
                {
                    if ( serverObjectDescr.IsDeleted ) // It should be deleted on local PC if exists.
                    {
                        if ( File.Exists( possibleLocalFilePath ) && SyncingObjectsList.TryDeleteFile( possibleLocalFilePath ) )
                        {
                            Log.Warning( $"File {possibleLocalFilePath} was deleted by client because it is marked as deleted on server" );
                        }
                    }
                    else
                    {
                        FileInfo fileInfo;
                        Boolean isFileAlreadyOnCurrentPc = File.Exists( possibleLocalFilePath );
                        Boolean isFileAlreadyLocked;

                        if ( isFileAlreadyOnCurrentPc )
                        {
                            fileInfo = FileInfoHelper.TryGetFileInfo( possibleLocalFilePath );
                            isFileAlreadyLocked = fileInfo != null;
                        }
                        else
                        {
                            // Try to recognize rename without changes within the same folder
                            fileInfo = AdsExtensions.TryFindFileByGuid( relatedToPrefixFolder, serverObjectDescr.Guid, out Boolean existsToFileWithSameGuid );
                            isFileAlreadyLocked = false;
                        }

                        if ( fileInfo != null )
                        {
                            Boolean shouldBeDownloaded = serverObjectDescr.ShouldBeDownloaded( fileInfo );

                            if ( !isFileAlreadyOnCurrentPc && !shouldBeDownloaded )
                            {
                                SyncingObjectsList.RenameFileOnlyLocal( fileInfo, possibleLocalFilePath, serverObjectDescr.LastModifiedDateTimeUtc, serverObjectDescr.Guid );
                            }
                            else if ( shouldBeDownloaded )
                            {
                                // TODO Ask Upload old file to server - how it is now? the same but vice versa.
                                await ApiClient.DownloadFileAsync( bucketName.ServerName, hexPrefix, relatedToPrefixFolder, serverObjectDescr.OriginalName, serverObjectDescr ).ConfigureAwait( false );
                            }
                        }
                        else
                        {
                            await ApiClient.DownloadFileAsync( bucketName.ServerName, hexPrefix, relatedToPrefixFolder, serverObjectDescr.OriginalName, serverObjectDescr ).ConfigureAwait( false );
                        }

                        //TODO:fix it
                        if ( !isFileAlreadyLocked && serverObjectDescr.IsLocked )
                        {
                            WriteLockedFileInAds( serverObjectDescr, fileInfo.FullName );
                        }
                    }
                }
                catch ( Exception ex )
                {
                    LoggingService.LogCriticalError( ex );
                }
            }

            List<String> localFilePaths;

            try
            {
                localFilePaths = PathExtensions.EnumerateFilesWithFilter( relatedToPrefixFolder );
            }
            catch ( DirectoryNotFoundException )
            {
                HandleDirectoryNotFoundException( relatedToPrefixFolder );
                return false;
            }

            // Delete local files which not exist on server side
            foreach ( String localFilePath in localFilePaths )
            {
                if ( m_isSyncStopped )
                {
                    return false;
                }

                if ( FileChangesQueue.IsPathAvaliableInActiveList( localFilePath ) )
                {
                    continue;
                }

                try
                {
                    FileInfo fileInfo = FileInfoHelper.TryGetFileInfo( localFilePath );

                    if ( fileInfo == null || fileInfo.Length == 0 )
                    {
                        await ApiClient.DeleteAsync( localFilePath );   //delete empty files from server
                        continue;
                    }

                    if ( listModel.ObjectDescriptions.Count( x => x.OriginalName == fileInfo.Name ) > 1 )
                    {
                        LoggingService.LogFatal( $"List have several records with '{fileInfo.Name}' OriginalName" );
                        continue;
                    }

                    ObjectDescriptionModel possibleFileDescription = listModel.ObjectDescriptions.SingleOrDefault( x => x.OriginalName == fileInfo.Name );

                    if ( possibleFileDescription == null )
                    {
                        ILockDescription lockDesc = AdsExtensions.ReadLockDescription( fileInfo.FullName );

                        if ( lockDesc.LockState == AdsLockState.ReadyToLock )
                        {
                            await ApiClient.LockFile( fileInfo.FullName );
                        }
                        else
                        {
                            LoggingService.LogInfo( $"Server does not have record in list for file {fileInfo.Name} - so it starts upload." );
                            await ApiClient.TryUploadAsync( fileInfo );
                        }
                    }
                    else
                    {
                        if ( possibleFileDescription.IsDeleted && File.Exists( localFilePath ) )
                        {
                            if ( SyncingObjectsList.TryDeleteFile( localFilePath ) )
                            {
                                Log.Warning( $"File {localFilePath} was deleted by client because it is marked as deleted on server." );
                            }
                        }
                        else if ( possibleFileDescription.IsLocked )
                        {
                            // TODO Separate method for 3 the same code snippets.
                            WriteLockedFileInAds( possibleFileDescription, fileInfo.FullName );
                        }
                    }
                }
                catch ( Exception ex )
                {
                    LoggingService.LogCriticalError( ex );
                }
            }

            // Note: Delete directories which not exist on server side
            List<String> localDirectories = PathExtensions.EnumerateDirectoriesWithFilter( relatedToPrefixFolder );

            foreach ( String localDirectory in localDirectories )
            {
                if ( m_isSyncStopped )
                {
                    return false;
                }

                if ( FileChangesQueue.IsPathAvaliableInActiveList( localDirectory ) )
                {
                    continue;
                }

                try
                {
                    if ( localDirectory.IsJunctionDirectory() )
                    {
                        // do nothing. Ignore junction directory.
                        continue;
                    }

                    String directoryName = new DirectoryInfo( localDirectory ).Name;

                    if ( listModel.ObjectDescriptions.Any( x => x.IsDeleted is false && x.OriginalName == directoryName ) )
                    {
                        // TODO Release 2.0. Do nothing at the moment. Server has file with the same name. It is related with conflict names.
                        Log.Warning( $"Server has file with the same name {directoryName}. It is related with conflict names." );
                        continue;
                    }

                    DirectoryDescriptionModel possibleDescriptionOnServer = listModel.DirectoryDescriptions.LastOrDefault( x => x.StringName == directoryName );

                    if ( possibleDescriptionOnServer == null && !FileChangesQueue.IsPathAvaliableInActiveList( localDirectory ) )
                    //Directory is renamed on server. TODO need to check other cases
                    {
                        //await ApiClient.CreateDirectoryOnServerAsync( localDirectory );
                        DeleteLocalFolderWithAllSubItemsWithoutServerNotification( localDirectory );
                    }
                    else if ( possibleDescriptionOnServer != null && possibleDescriptionOnServer.IsDeleted )
                    {
                        // If directory should be uploaded to server - it is responsibility of prioritized SyncToServer logic.
                        DeleteLocalFolderWithAllSubItemsWithoutServerNotification( localDirectory );
                        Log.Warning( $"Directory {localDirectory} was deleted by client because it is marked as 'IsDeleted' on server." );
                    }
                }
                catch ( Exception ex )
                {
                    LoggingService.LogCriticalError( ex );
                }
            }

            // Ignore deleted directory descriptions.
            foreach ( DirectoryDescriptionModel item in listModel.DirectoryDescriptions.Where( x => x.IsDeleted is false ) )
            {
                if ( m_isSyncStopped )
                {
                    return false;
                }

                await SyncDirectoryFromServerAsync( bucketName, item.Prefix, bucketDirectory, checkServerEventArgsCollection );
            }

            return true;
        }

        private void WriteLockedFileInAds( ObjectDescriptionModel objectDescriptionModel, String localFullFileName )
        {
            ILockDescription description = new LockDescription( AdsLockState.LockedOnServer )
            {
                LockTimeUtc = objectDescriptionModel.LockModifiedDateTimeUtc,
                LockUserId = objectDescriptionModel.LockUserId,
                LockUserTel = objectDescriptionModel.LockUserTel,
                LockUserName = objectDescriptionModel.LockUserName
            };

            AdsExtensions.WriteLockDescription( localFullFileName, description );
        }

        private async void CheckFilesOnServerAsync( Object timerState )
        {
            LoggingService.LogInfo( logRecord: $"{nameof( CheckFilesOnServerAsync )} is started at {DateTime.UtcNow}" );

            var checkEventArgsCollection = timerState as CheckServerChangesEventArgsCollection;
            foreach ( CheckServerChangesEventArgs checkEventArgs in checkEventArgsCollection )
            {
                await ApiClient.ListWithCancelDownloadAsync( checkEventArgs.ServerBucketName, checkEventArgs.HexPrefix, showDeleted: true ).ConfigureAwait( continueOnCapturedContext: false );
                checkEventArgsCollection.TryRemoveFirstItem( isRemoved: out _ );
            }
        }

        private void DeleteLocalFolderWithAllSubItemsWithoutServerNotification( String directoryPath )
        {
            String[] dirs = Directory.GetDirectories( directoryPath, "*", SearchOption.AllDirectories );
            String[] files = Directory.GetFiles( directoryPath, "*", SearchOption.AllDirectories );

            var alreadyDeleted = new List<String>();

            foreach ( String file in files )
            {
                try
                {
                    File.SetAttributes( file, FileAttributes.Normal );
                }
                catch ( FileNotFoundException )
                {
                    alreadyDeleted.Add( file );
                }
            }

            SyncingObjectsList.AddDeletingDirectories( dirs );
            SyncingObjectsList.AddDeletingFiles( files.Where( file => !alreadyDeleted.Contains( file ) ).ToArray() );

            SyncingObjectsList.AddDeletingDirectory( directoryPath );
            try
            {
                Directory.Delete( directoryPath, true );
            }
            catch ( Exception ex )
            {
                LoggingService.LogError( ex.Message );
            }
        }

        // we do it once, because queues will support online sync to server
        //TODO: allow empty files
        /// <param name="previousUserSelectResults">
        /// All user selections what to do with defunct folders on the server during current sync all to server.
        /// Key is full path on a local PC to defunct directory on the server.
        /// Value is user selection what to do with this folder.
        /// This parameter is used in order not to ask each time what to do with subfolders, whose parent already has user selection
        /// </param>
        private async Task SyncToServer( String currentDirectory, IBucketName bucketName, String hexPrefix, DateTime lastSyncDateTime, CancellationToken cancellationToken, Dictionary<String, MessageBoxResult> previousUserSelectResults ) // TODO Disscuss second last parameter.
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ( m_isSyncStopped || currentDirectory.IsJunctionDirectory() )
            {
                return;
            }

            if ( !m_pathFiltrator.IsPathPertinent( currentDirectory ) )
            {
                return;
            }

            LoggingService.LogInfo( $"    Syncing to {currentDirectory} ..." );

            String combinedBucketNameAndPrefix = Path.Combine( bucketName.LocalName, hexPrefix.FromHexString() );
            if ( !currentDirectory.Contains( combinedBucketNameAndPrefix ) )
            {
                String errorMessage = $"Directory '{currentDirectory}' does not contain bucketname '{bucketName.LocalName}' and prefix '{hexPrefix}'";

                var inconsistencyException = new InconsistencyException( errorMessage );
                LoggingService.LogCriticalError( inconsistencyException );

                throw inconsistencyException;
            }

            ObjectsListResponse listResponse = await ApiClient.ListAsync( bucketName.ServerName, hexPrefix, true ).ConfigureAwait( continueOnCapturedContext: false );

            if ( !listResponse.IsSuccess )
            {
                LoggingService.LogError( listResponse.Message );
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var listModel = listResponse.ToObjectsListModel();

            var localFilePathes = new List<String>();

            try
            {
                localFilePathes = PathExtensions.EnumerateFilesWithFilter( currentDirectory );
            }
            catch ( DirectoryNotFoundException )
            {
                HandleDirectoryNotFoundException( currentDirectory );
                return;
            }

            foreach ( String localFilePath in localFilePathes )
            {
                cancellationToken.ThrowIfCancellationRequested();

                if ( m_isSyncStopped )
                {
                    return;
                }

                FileInfo fileInfo = FileInfoHelper.TryGetFileInfo( localFilePath );

                if ( fileInfo == null || fileInfo.Length == 0 )
                {
                    //_ = await ApiClient.DeleteAsync(localFilePath);   //delete empty files from server
                    if ( File.Exists( localFilePath ) ) //maybe file was deleted
                    {
                        File.Delete( localFilePath ); //empty files exists in client only and should be deleted
                    }

                    continue;
                }

                if ( listModel.ObjectDescriptions.Count( x => x.OriginalName == fileInfo.Name ) > 1 )
                {
                    LoggingService.LogFatal( $"List have several records with '{fileInfo.Name}' OriginalName." );
                    continue;
                }

                ObjectDescriptionModel possibleDescriptionOnServer = listModel.ObjectDescriptions.SingleOrDefault( x => x.OriginalName == fileInfo.Name );

                // Possible cases:
                // - new file should be uploaded
                // - it is just rename of the same file within the same directory
                // - copy file (if copy failed - just upload)
                // - move file (if copy failed - just upload)
                if ( possibleDescriptionOnServer == null )
                {
                    String possibleGuid = AdsExtensions.Read( localFilePath, AdsExtensions.Stream.Guid );
                    INotificationResult result = default;

                    LoggingService.LogInfo( localFilePath + ", guid " + possibleGuid );

                    if ( String.IsNullOrEmpty( possibleGuid ) )
                    {
                        Log.Warning( $"Upload file {fileInfo.Name}" );
                        result = await ApiClient.TryUploadAsync( fileInfo );
                    }
                    else
                    {
                        if ( listModel.ObjectDescriptions.Count( x => x.Guid == possibleGuid ) > 1 )
                        {
                            LoggingService.LogFatal( $"List have several records with '{possibleGuid}' Guid." );
                            continue;
                        }

                        ObjectDescriptionModel possibleAnotherDescWithTheSameGuid = listModel.ObjectDescriptions.SingleOrDefault( x => x.Guid.Equals( possibleGuid, StringComparison.Ordinal ) );

                        var comparisonResult = ComparationLocalAndServerFileResult.None;
                        possibleAnotherDescWithTheSameGuid?.CompareFileOnServerAndLocal( fileInfo, out comparisonResult );

                        if ( ( possibleAnotherDescWithTheSameGuid != null ) && ( comparisonResult == ComparationLocalAndServerFileResult.Equal ) )
                        {
                            String destinationFileName = Path.Combine( fileInfo.Directory.FullName, possibleAnotherDescWithTheSameGuid.OriginalName );

                            result = SyncingObjectsList.RenameFileOnlyLocal( fileInfo, destinationFileName, fileInfo.LastWriteTimeUtc, possibleAnotherDescWithTheSameGuid.Guid );
                        }
                        else
                        {
                            String localPathMarker = AdsExtensions.Read( localFilePath, AdsExtensions.Stream.LocalPathMarker );

                            if ( String.IsNullOrEmpty( localPathMarker ) )
                            {
                                throw new InconsistencyException( "Both markers - guid and local path - should exist." );
                            }

                            if ( File.Exists( localPathMarker ) )
                            {
                                // TODO 1.0 How about local path marker from another user?
                                if ( localPathMarker != localFilePath &&
                                    File.GetLastWriteTimeUtc( localPathMarker ).AssumeIsTheSame( File.GetLastWriteTimeUtc( localFilePath ) ) )
                                {
                                    LoggingService.LogInfo( $"API Copy from {localPathMarker} to {fileInfo.FullName}" );
                                    result = await ApiClient.CopyAsync( localPathMarker, fileInfo.FullName );

                                    if ( !result.IsSuccess )
                                    {
                                        Log.Warning( $"API Copy from {localPathMarker} to {fileInfo.FullName} is not success, try upload {fileInfo.FullName}" );
                                        result = await ApiClient.TryUploadAsync( fileInfo );
                                    }
                                }
                                else
                                {
                                    LoggingService.LogInfo( $"Upload file {fileInfo.Name}" );
                                    result = await ApiClient.TryUploadAsync( fileInfo );
                                }
                            }
                            else
                            {
                                ServerObjectDescription markerDescription = await ApiClient.GetExistingObjectDescription( localPathMarker ).ConfigureAwait( false );
                                Boolean isFileHandled = false;

                                if ( markerDescription.IsSuccess )
                                {
                                    markerDescription.CompareFileOnServerAndLocal( fileInfo, out comparisonResult );
                                    if ( comparisonResult == ComparationLocalAndServerFileResult.Equal )
                                    {
                                        LoggingService.LogInfo( $"API Move from {localPathMarker} to {fileInfo.FullName}" );
                                        result = await ApiClient.MoveAsync( localPathMarker, fileInfo.FullName ).ConfigureAwait( false );

                                        isFileHandled = result.IsSuccess;
                                    }
                                }

                                if ( !isFileHandled )
                                {
                                    LoggingService.LogInfo( $"Upload file {fileInfo.Name}" );
                                    result = await ApiClient.TryUploadAsync( fileInfo ).ConfigureAwait( false );
                                }
                            }
                        }
                    }

                    if ( result?.IsSuccess == true )
                    {
                        ILockDescription lockDescription = AdsExtensions.ReadLockDescription( localFilePath );

                        if ( lockDescription.LockState == AdsLockState.ReadyToLock )
                        {
                            await ApiClient.LockFile( localFilePath );
                        }
                        else if ( lockDescription.LockState == AdsLockState.ReadyToUnlock )
                        {
                            await ApiClient.UnlockFile( localFilePath );
                        }
                    }
                }
                else if ( possibleDescriptionOnServer.IsLocked )
                {
                    WriteLockedFileInAds( possibleDescriptionOnServer, fileInfo.FullName );
                }
                else
                {
                    if ( possibleDescriptionOnServer.IsDeleted )
                    {
                        if ( SyncingObjectsList.TryDeleteFile( localFilePath ) )
                        {
                            Log.Warning( $"File {localFilePath} was deleted by client because it is marked as deleted on server." );
                        }
                    }
                    else
                    {
                        ComparationLocalAndServerFileResult comparationResult = ComparationLocalAndServerFileResult.None;
                        try
                        {
                            ServerObjectDescription objectDescrOnServer = await ApiClient.GetExistingObjectDescription( localFilePath );

                            FileInfo localFileInfo = FileInfoHelper.TryGetFileInfo( localFilePath );
                            objectDescrOnServer.CompareFileOnServerAndLocal( localFileInfo, out comparationResult );
                        }
                        catch ( IOException ex )
                        {
                            LoggingService.LogInfo( ex.Message );
                        }
                        catch ( ArgumentException ex )
                        {
                            LoggingService.LogInfo( ex.Message );
                        }

                        Boolean shouldFileBeUploaded = comparationResult.ShouldFileBeUploaded();
                        if ( shouldFileBeUploaded )
                        {
                            _ = await ApiClient.TryUploadAsync( fileInfo ).ConfigureAwait( false );
                        }
                        //else if(comparationResult == ComparationLocalAndServerFileResult.NewerOnServer)
                        //{
                        //    await ApiClient.DownloadFileAsync( 
                        //        bucketName.ServerName, 
                        //        hexPrefix, 
                        //        currentDirectory, 
                        //        Path.GetFileName( localFilePath ), 
                        //        possibleDescriptionOnServer 
                        //    ).ConfigureAwait( false );
                        //}
                        //string localMd5;

                        //try
                        //{
                        //    localMd5 = ByteArrayExtensions.CalculateTempCustomHash(localFilePath, possibleDescriptionOnServer.Md5);
                        //    //string localMd5 = ByteArrayExtensions.CalculateAmazonMd5Hash(localFilePath);
                        //}
                        //catch (IOException ex)
                        //{
                        //    #if DEBUG
                        //    Console.WriteLine(ex.Message);
                        //    #endif
                        //    // Case when file is opened already and locked by, for example, MS Word.
                        //    continue;
                        //}

                        //if (string.IsNullOrEmpty(localMd5))
                        //{
                        //    continue;
                        //}

                        //var areFilesIntegral = localMd5.Equals(possibleDescriptionOnServer.Md5);
                        //if (!areFilesIntegral)
                        //{
                        //    VectorClock vectorClock = new VectorClock();
                        //    var currentVersionOfFile = vectorClock.CurrentFileVectorVersion(localFilePath, CurrentUserProvider.LoggedUser.Id);

                        //    if (!currentVersionOfFile.Equals(possibleDescriptionOnServer.Version))
                        //    {
                        //        Console.WriteLine();
                        //        Console.WriteLine($"localWriteTimeUtc for file {fileInfo.Name} is bigger than such date on server - so it will be deleted and upload to server.");

                        //        var deleteResponse = await ApiClient.DeleteAsync(localFilePath);
                        //        //NotifyService.Notify(deleteResponse);

                        //        var uploadResponse = await ApiClient.TryUploadAsync(fileInfo);
                        //    }
                        //}
                    }
                }
            }

            List<String> localDirectories = PathExtensions.EnumerateDirectoriesWithFilter( currentDirectory );

            foreach ( String localDirectoryPath in localDirectories )
            {
                cancellationToken.ThrowIfCancellationRequested();

                if ( m_isSyncStopped )
                {
                    return;
                }

                ServerObjectDescription possibleDescriptionOnServer = null;

                //we check whether localDirectoryPath.Contains( pathToFolder ), because before we
                //add folders in previousUserSelectResults which is higher in directory tree 
                MessageBoxResult previousUserActionSelect = previousUserSelectResults.FirstOrDefault( pathToFolderAndUserSelection => localDirectoryPath.Contains( pathToFolderAndUserSelection.Key ) ).Value;

                if ( previousUserActionSelect == default )
                {
                    possibleDescriptionOnServer = await ApiClient.GetExistingObjectDescription( localDirectoryPath ).ConfigureAwait( false );
                }
                else
                {
                    //user already selected what to do with the folder
                }

                if ( ( possibleDescriptionOnServer == null ) || !possibleDescriptionOnServer.IsSuccess )
                {
                    // user may delete directories during sync to server operation. So it is better to check whether directory exists
                    // TODO Document last sync date. It is for deleting folder, which not exists on server and should not exist on server.
                    if ( Directory.Exists( localDirectoryPath ) )
                    {
                        MessageBoxResult messageBoxResult;
                        if ( previousUserActionSelect == default )
                        {
                            String messageToUser = String.Format( Strings.Message_CreatedFolderOffline, localDirectoryPath );
                            messageBoxResult = MessageBoxHelper.ShowMessageBox( messageToUser, caption: Strings.Title_SelectAction, MessageBoxButton.YesNoCancel );

                            previousUserSelectResults.Add( localDirectoryPath, messageBoxResult );
                        }
                        else
                        {
                            messageBoxResult = previousUserActionSelect;
                        }

                        switch ( messageBoxResult )
                        {
                            case MessageBoxResult.Cancel:
                            {
                                m_pathFiltrator.AddNewFoldersToIgnore( localDirectoryPath );
                                break;
                            }

                            case MessageBoxResult.No:
                            {
                                DeleteLocalFolderWithAllSubItemsWithoutServerNotification( localDirectoryPath );
                                Log.Warning( $"Directory {localDirectoryPath} was deleted by client in method SyncToServer" );

                                break;
                            }

                            case MessageBoxResult.None:
                            case MessageBoxResult.Yes:
                            {
                                var directoryInfo = new DirectoryInfo( localDirectoryPath );

                                DateTime newestLastWriteTimeUtc = directoryInfo.LastWriteTimeUtc;
                                DateTime newestCreatingTimeUtc = directoryInfo.CreationTimeUtc;

                                String[] files = Directory.GetFiles( localDirectoryPath, "*", SearchOption.AllDirectories );
                                var realFiles = files.Where( x => !PathExtensions.IsIgnorableExtension( x ) &&
                                                                  !PathExtensions.IsTemporaryFileName( x ) ).
                                                                          ToList();
                                foreach ( String curFile in realFiles )
                                {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    FileInfo curFileInfo = FileInfoHelper.TryGetFileInfo( curFile );

                                    if ( curFileInfo == null )
                                    {
                                        continue;
                                    }

                                    if ( newestLastWriteTimeUtc < curFileInfo.LastWriteTimeUtc )
                                    {
                                        newestLastWriteTimeUtc = curFileInfo.LastWriteTimeUtc;
                                    }

                                    if ( newestCreatingTimeUtc < curFileInfo.CreationTimeUtc )
                                    {
                                        newestCreatingTimeUtc = curFileInfo.CreationTimeUtc;
                                    }
                                }

                                CreateDirectoryResponse createResponse = await ApiClient.CreateDirectoryOnServerAsync( localDirectoryPath );

                                if ( createResponse.IsSuccess )
                                {
                                    possibleDescriptionOnServer = await ApiClient.GetExistingObjectDescription( localDirectoryPath );

                                    if ( possibleDescriptionOnServer.IsSuccess )
                                    {
                                        await SyncToServer( localDirectoryPath, bucketName, possibleDescriptionOnServer.ObjectPrefix, lastSyncDateTime, cancellationToken, previousUserSelectResults );
                                    }
                                    else
                                    {
                                        LoggingService.LogError( possibleDescriptionOnServer.Message );
                                    }
                                }
                                else
                                {
                                    LoggingService.LogError( createResponse.Message );
                                }

                                break;
                            }
                        }
                    }
                }
                else
                {
                    await SyncToServer( localDirectoryPath, bucketName, possibleDescriptionOnServer.ObjectPrefix, lastSyncDateTime, cancellationToken, previousUserSelectResults );
                }
            }
        }

        private void ExamineCurrentToken()
        {
            if ( !IsTokenExpiredOrIncorrectAccessToken )
            {
                return;
            }

            Log.Error( @"Token expired or Incorrect access token" );
            StopAllSync();

            MessageBoxHelper.ShowMessageBox( String.Format( Strings.Label_TokenExpired ), Strings.Label_Attention );

            NavigationManager.NavigateToLoginView();

            IsTokenExpiredOrIncorrectAccessToken = false;

            RunPeriodicSyncFromServer();
        }


        private void HandleDirectoryNotFoundException( String directoryPath )
        {
            if ( Directory.Exists( CurrentUserProvider.RootFolderPath ) )
            {
                Log.Warning( $"{directoryPath} does not exist." );
            }
            else
            {
                NavigationManager.NavigateToLoginView();
                MessageBoxHelper.ShowMessageBox( Strings.Message_RootFolderDoesNotExist, Strings.Label_Attention );
            }
        }

        #endregion
    }
}
