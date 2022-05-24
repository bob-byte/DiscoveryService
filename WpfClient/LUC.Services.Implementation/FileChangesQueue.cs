using LUC.Common.PrismEvents;
using LUC.Interfaces;
using LUC.Interfaces.Abstract;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Exceptions;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;

using Prism.Events;

using Serilog;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Threading;

namespace LUC.Services.Implementation
{
    [Export(typeof(IFileChangesQueue))]
    public class FileChangesQueue : IFileChangesQueue
    {
        #region Constants

        private const Int32 TIMER_TO_TRY_ADD_TO_ACT_LIST_IN_SEC = 1;
        private const Int32 SWITCH_CHANGES_TIMER_IN_SEC = 1;

        #endregion

        #region Fields

        private readonly AsyncLock m_lockHandleLockedFiles;

        /// <summary>
        /// Key is ObjectChangeDescription.Id
        /// </summary>
        private readonly ConcurrentDictionary<Int64, ObjectChangeDescription> m_activeList;

        ///<summary>
        ///Contains pathes, which is received after raising rename event,
        ///but new full path( path, file/directory name, extension(if it
        ///is file) is equal to old full path. Key is the same as value
        /// </summary> 
        private readonly ConcurrentDictionary<String, String> m_renamedPathesToIgnore;

        /// <summary>
        /// Locked, but changed files
        /// </summary>
        private readonly ConcurrentDictionary<Int64, ObjectChangeDescription> m_lockedFiles;

        /// <summary>
        /// Key is path where file was downloaded
        /// </summary>
        private readonly ConcurrentDictionary<String, DownloadingFileInfo> m_downloadedNotMovedFile;

        private readonly IEventAggregator m_eventAggregator;

        ///<summary>
        ///Time wait while file is processed by <seealso cref="IFileSystemFacade"/>
        /// </summary>
        private readonly TimeSpan m_timeWaitWhileFileIsProcessed;
        private readonly TimeSpan m_timeWaitWhileFileSystemFacadeIgnoreFileChange;

        private DateTime m_timeOfLastAddedChangeType;

        private UInt32 m_countSyncToServer;

        private Boolean m_isSyncToServerNow;

        #endregion

        #region Constructors

        [ImportingConstructor]
        public FileChangesQueue(IEventAggregator eventAggregator, ILoggingService loggingService, ICurrentUserProvider currentUserProvider, IApiClient apiClient)
        {
            m_timeWaitWhileFileIsProcessed = TimeSpan.FromSeconds(value: 2);
            m_timeWaitWhileFileSystemFacadeIgnoreFileChange = TimeSpan.FromSeconds(2);

            ApiClient = apiClient;
            BackgroundSynchronizer = AppSettings.ExportedValue<IBackgroundSynchronizer>();
            m_eventAggregator = eventAggregator;
            LoggingService = loggingService;
            CurrentUserProvider = currentUserProvider;

            m_lockedFiles = new ConcurrentDictionary<Int64, ObjectChangeDescription>();
            m_lockHandleLockedFiles = new AsyncLock();

            m_downloadedNotMovedFile = new ConcurrentDictionary<String, DownloadingFileInfo>();

            m_renamedPathesToIgnore = new ConcurrentDictionary<String, String>();

            m_activeList = new ConcurrentDictionary<Int64, ObjectChangeDescription>();

            var timer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, SWITCH_CHANGES_TIMER_IN_SEC)
            };

            timer.Tick += TrySwitchQueueTick;
            timer.Start();

            _ = eventAggregator.GetEvent<IsSyncFromServerChangedEvent>().Subscribe(param => IsSyncFromServerNow = param);

            eventAggregator.GetEvent<IsSyncToServerChangedEvent>().Subscribe(isSyncToServerStarted =>
            {
                Boolean isSyncToServerFinished = !isSyncToServerStarted;

                if (isSyncToServerFinished)
                {
                    m_countSyncToServer++;
                }
            });

            if (!String.IsNullOrWhiteSpace(CurrentUserProvider.RootFolderPath))
            {
                FindDownloadedNotMovedFilesAsync(CurrentUserProvider.RootFolderPath).ConfigureAwait(continueOnCapturedContext: false);
            }

            CurrentUserProvider.RootFolderPathChanged += OnSyncFolderChange;
        }

        private IBackgroundSynchronizer BackgroundSynchronizer { get; }

        #endregion

        #region Properties

        public Boolean IsSyncFromServerNow { get; set; }

        [Import(typeof(ISyncingObjectsList))]
        public ISyncingObjectsList SyncingObjectsList { get; set; }

        [Import(typeof(IApiClient))]
        private IApiClient ApiClient { get; set; }

        [Import(typeof(ILoggingService))]
        public ILoggingService LoggingService { get; set; }

        public ICurrentUserProvider CurrentUserProvider { get; set; }

        #endregion

        public Boolean IsPathAvaliableInActiveList( String path ) =>
            m_activeList.Values.Any( x =>
             ( x.OriginalFullPath == path ) ||
             //user deleted directory during sync to the server
             ( path.Contains( x.OriginalFullPath ) && ( x.Change.ChangeType == WatcherChangeTypes.Deleted ) && !Directory.Exists( x.OriginalFullPath ) ) ||
             ( ( x.Change.ChangeType == WatcherChangeTypes.Renamed ) && ( (RenamedEventArgs)x.Change ).FullPath == path ) );

        public void AddDownloadedNotMovedFile(DownloadingFileInfo downloadingFileInfo)
        {
            if (downloadingFileInfo != null)
            {
#if !DEBUG
                if ( downloadingFileInfo.ByteCount > DownloadConstants.TOO_SMALL_FILE_SIZE_TO_SAVE_IN_CHANGES_QUEUE)
                {
#endif
                    //remove older file with downloadingFileInfo.LocalFilePath already exists(case when app was closed, file with newer version was downloaded,
                    //but it is still being used by another process and after program restart it continue to use. Then
                    //file with newer version was downloaded from server, closed by user, so it should be updated in SyncFolder with newer version)
                    TryRemoveDownloadedNotMovedFile(downloadingFileInfo, isRemoved: out _);

                    AdsExtensions.WriteThatFileIsDownloaded(downloadingFileInfo);
                    SimpleAddDownloadedNotMovedFile( downloadingFileInfo );
#if !DEBUG
                }
#endif
            }
            else
            {
                throw new ArgumentNullException(nameof(downloadingFileInfo));
            }
        }

        public void TryRemoveDownloadedNotMovedFile(DownloadingFileInfo downloadingFileInfo, out Boolean isRemoved)
        {
            if (downloadingFileInfo != null)
            {
                DownloadingFileInfo foundOldDownloadingFile = m_downloadedNotMovedFile.Values.AsParallel().FirstOrDefault(fileInfo => fileInfo.Equals(downloadingFileInfo));
                if (foundOldDownloadingFile != null)
                {
                    foundOldDownloadingFile.Dispose();
                    isRemoved = m_downloadedNotMovedFile.TryRemove( foundOldDownloadingFile.PathWhereDownloadFileFirst );
                }
                else
                {
                    isRemoved = false;
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(downloadingFileInfo));
            }
        }

        public void AddEvent(FileSystemEventArgs args)
        {
            ChangeFileListsAccordingToNewStates();

            switch (args.ChangeType)
            {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Deleted:
                    {
                        // Delete all previous changes except renaming.
                        m_activeList.RemoveAll(x => x.Value.OriginalFullPath == args.FullPath && x.Value.Change.ChangeType != WatcherChangeTypes.Renamed);
                        TryAddToActiveList(new ObjectChangeDescription(args.FullPath, args));
                    }

                    break;
                case WatcherChangeTypes.Changed: //TODO Change logic; no need delete old activelist, just not add new Changed event
                    {
                        if (m_activeList.Any(x => x.Value.OriginalFullPath == args.FullPath && x.Value.Change.ChangeType == WatcherChangeTypes.Created))
                        {
                            // do nothing. File will be uploaded anyway because it was just created.
                        }
                        else if (SyncingObjectsList.TryFindDownloadingFile(args.FullPath) != null)
                        {
                            //ignore change of downloading file
                        }
                        else
                        {
                            // Delete all previuos changed file states.
                            _ = m_activeList.RemoveAll(x => x.Value.OriginalFullPath == args.FullPath && x.Value.Change.ChangeType != WatcherChangeTypes.Renamed);
                            _ = TryAddToActiveList(new ObjectChangeDescription(args.FullPath, args));
                            LoggingService.LogInfo($"Queue-> Added changed event: {DateTime.Now} {args.FullPath}");
                        }
                    }

                    break;
                case WatcherChangeTypes.Renamed: // Always perform rename operation.
                    {
                        var renamedArgs = (RenamedEventArgs)args;

                        ObjectChangeDescription possibleAlreadyRenamed = m_activeList.Values.AsParallel().SingleOrDefault(x =>
                                x.Change.ChangeType == WatcherChangeTypes.Renamed && (x.Change as RenamedEventArgs).FullPath == renamedArgs.OldFullPath);
                        if (possibleAlreadyRenamed == null)
                        {
                            _ = TryAddToActiveList(new ObjectChangeDescription(renamedArgs.OldFullPath, args));
                        }
                        else
                        {
                            LoggingService.LogInfo($"Replaced from {(possibleAlreadyRenamed.Change as RenamedEventArgs).OldFullPath} -> {(possibleAlreadyRenamed.Change as RenamedEventArgs).FullPath}");
                            possibleAlreadyRenamed.IsProcessed = true;

                            // Join to renaming into one - from first original to last actual.
                            var renamedArgsNew = new RenamedEventArgs(WatcherChangeTypes.Renamed,
                                    Path.GetDirectoryName(renamedArgs.FullPath),
                                    Path.GetFileName(renamedArgs.Name),
                                    Path.GetFileName((possibleAlreadyRenamed.Change as RenamedEventArgs).OldName));

                            LoggingService.LogInfo($@"Replaced to {renamedArgsNew.OldFullPath} -> {renamedArgsNew.FullPath}");

                            _ = TryAddToActiveList(new ObjectChangeDescription(renamedArgsNew.OldFullPath, renamedArgsNew));
                        }
                    }

                    break;

                case WatcherChangeTypes.All:
                    break;
            }

            m_timeOfLastAddedChangeType = DateTime.UtcNow;
        }

        public async Task HandleLockedFilesAsync()
        {
            using (await m_lockHandleLockedFiles.LockAsync().ConfigureAwait(continueOnCapturedContext: false))
            {
                //ToArray is called, because in body we remove elements, so we need to get copied collection and
                //because it is more often that m_lockedFiles doesn't have a lot of items and they are only for reading
                foreach (ObjectChangeDescription lockedFile in m_lockedFiles.Values.ToArray())
                {
                    ObjectStateType fileState = PathExtensions.GetObjectState(lockedFile.Change.FullPath);
                    if ( ( fileState != ObjectStateType.Locked ) && lockedFile.Change.FullPath.Contains( CurrentUserProvider.RootFolderPath ) )
                    {
                        try
                        {
                            await PerformApiAction(lockedFile).ConfigureAwait(false);
                            m_lockedFiles.TryRemove(lockedFile.Id);
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogCriticalError(ex);
                        }
                    }
                }
            }
        }

        //TODO: test it
        public async Task HandleDownloadedNotMovedFilesAsync()
        {
            if (CanBeProcessedDownloadedNotMovedFiles)
            {
                IList<DownloadingFileInfo> movedFiles = new SynchronizedCollection<DownloadingFileInfo>();

                var handleProcess = new ActionBlock<DownloadingFileInfo>(async downloadedNotMovedFile =>
                {
                    Boolean isDownloadedFileAvailableToBeMoved = IsDownloadedFileAvailableToBeMoved(downloadedNotMovedFile);
                    if (isDownloadedFileAvailableToBeMoved)
                    {
                        try
                        {
                            ServerObjectDescription serverObjectDescription = await ApiClient.GetExistingObjectDescription(downloadedNotMovedFile.LocalFilePath).ConfigureAwait(continueOnCapturedContext: false);

                            serverObjectDescription.CompareFileOnServerAndLocal(new FileInfo(downloadedNotMovedFile.LocalFilePath), out ComparationLocalAndServerFileResult comparationResult);
                            Boolean isFileNewerOnServer = comparationResult.IsFileNewerOnServer();

                            if (!isFileNewerOnServer || !File.Exists(downloadedNotMovedFile.LocalFilePath))//if file is deleted by user, then it shouldn't be moved
                            {
                                //exception will not be thrown if file doesn't exist
                                File.Delete(downloadedNotMovedFile.PathWhereDownloadFileFirst);

                                //the same or newer file already exist in bucket
                                movedFiles.Add(downloadedNotMovedFile);
                            }
                            else
                            {
                                //in order to IFileSystemFacade ignored this deletion
                                SyncingObjectsList.AddDownloadingFile(downloadedNotMovedFile);

                                try
                                {
                                    if (File.Exists(downloadedNotMovedFile.PathWhereDownloadFileFirst))
                                    {
                                        File.SetAttributes(downloadedNotMovedFile.LocalFilePath, FileAttributes.Normal);
                                        File.Delete(downloadedNotMovedFile.LocalFilePath);

                                        File.Move(downloadedNotMovedFile.PathWhereDownloadFileFirst, downloadedNotMovedFile.LocalFilePath);
                                        File.SetAttributes(downloadedNotMovedFile.LocalFilePath, FileAttributes.Normal);

                                        try
                                        {
                                            AdsExtensions.WriteInfoAboutNewFileVersion(new FileInfo(downloadedNotMovedFile.LocalFilePath), downloadedNotMovedFile.Version, downloadedNotMovedFile.Guid);
                                        }
                                        catch
                                        {
                                            ;//do nothing
                                        }
                                    }

                                    movedFiles.Add(downloadedNotMovedFile);

                                    //wait while IFileSystemFacade ignore deletion and move
                                    await Task.Delay(m_timeWaitWhileFileSystemFacadeIgnoreFileChange).ConfigureAwait(false);
                                }
                                catch (Exception)
                                {
                                    ;//do nothing
                                }
                                finally
                                {
                                    SyncingObjectsList.RemoveDownloadingFile(downloadedNotMovedFile);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogError(ex, ex.Message);
                        }
                    }
                });

                Parallel.ForEach(m_downloadedNotMovedFile.Values, downloadedNotMovedFile => handleProcess.Post(downloadedNotMovedFile));

                handleProcess.Complete();
                await handleProcess.Completion.ConfigureAwait(false);

                m_downloadedNotMovedFile.RemoveRange( movedFiles.Select( m => m.PathWhereDownloadFileFirst ) );
            }
        }

        public void Clear()
        {
            m_activeList.Clear();
            m_lockedFiles.Clear();

            foreach (DownloadingFileInfo downloadedNotMovedFile in m_downloadedNotMovedFile.Values)
            {
                downloadedNotMovedFile.Dispose();
            }
            m_downloadedNotMovedFile.Clear();
        }

        private async void OnSyncFolderChange(Object sender, RootFolderPathChangedEventArgs eventArgs) =>
            await FindDownloadedNotMovedFilesAsync(eventArgs.NewRootFolder).ConfigureAwait(continueOnCapturedContext: false);

        private async Task FindDownloadedNotMovedFilesAsync(String syncFolder)
        {
            if(!String.IsNullOrWhiteSpace(syncFolder))
            {
                DirectoryInfo tempDirectoryInfo = DirectoryExtensions.DirectoryForDownloadingTempFiles( syncFolder );

                String searchPatternForAllFiles = "*.*";
                String[] tempFullFileNames = null;
                Boolean isExceptionThrown;
                try
                {
                    tempFullFileNames = Directory.GetFiles( tempDirectoryInfo.FullName, searchPatternForAllFiles, SearchOption.AllDirectories );
                    isExceptionThrown = false;
                }
                catch ( IOException ex )
                {
                    isExceptionThrown = true;
                    LoggingService.LogCriticalError( ex );
                }

                if ( !isExceptionThrown )
                {
                    var findProcess = new ActionBlock<String>( fullPathToTempFile =>
                    {
                        Boolean? isFileFullyDownloadedNullable = null;

                        try
                        {
                            AdsExtensions.ReadIsFileDownloaded( fullPathToTempFile, out Boolean isFileFullyDownloaded );
                            isFileFullyDownloadedNullable = isFileFullyDownloaded;
                        }
                        catch ( Exception ex )
                        {
                            LoggingService.LogCriticalError( ex );
                        }

                        if ( isFileFullyDownloadedNullable == true )
                        {
                            AdsExtensions.ReadDownloadingFileInfo( fullPathToTempFile, syncFolder, out DownloadingFileInfo downloadingFileInfo, out Boolean isSuccessfullyRead );

                            if ( isSuccessfullyRead )
                            {
                                //try remove with older version
                                TryRemoveDownloadedNotMovedFile( downloadingFileInfo, isRemoved: out _ );
                                SimpleAddDownloadedNotMovedFile( downloadingFileInfo );
                            }
                        }
                    } );

                    Parallel.ForEach( tempFullFileNames, fullPathToTempFile =>
                    {
                        findProcess.Post( fullPathToTempFile );
                    } );

                    findProcess.Complete();
                    await findProcess.Completion.ConfigureAwait( false );
                }
            }
        }

        private Boolean TryAddToChangeLockedList( ObjectChangeDescription change ) =>
            m_lockedFiles.TryAdd( change.Id, change );

        private Boolean SimpleAddDownloadedNotMovedFile( DownloadingFileInfo downloadingFileInfo) =>
            m_downloadedNotMovedFile.TryAdd( downloadingFileInfo.PathWhereDownloadFileFirst, downloadingFileInfo );

        private Boolean TryAddToActiveList(ObjectChangeDescription change) =>
            m_activeList.TryAdd( change.Id, change );

        private Boolean TryAddRenamedPathesToIgnore( String renamedPathesToIgnore ) =>
            m_renamedPathesToIgnore.TryAdd( renamedPathesToIgnore, renamedPathesToIgnore );

        private Boolean IsAllowedToHandleChangesInSyncFolder()
        {
            if (!m_activeList.Any())
            {
#if DEBUG
                Log.Information( messageTemplate: "Is not allow to switch queue now, because _activeList is empty." );
#endif
                return false;
            }

            if (SyncingObjectsList.HasDownloadingFiles())
            {
#if DEBUG
                Log.Information( "Is not allow to switch queue now, because sync objects list has downloading files." );
#endif

                return false;
            }

            if (IsSyncFromServerNow)
            {
#if DEBUG
                Log.Information( "Is not allow to switch queue now, because IsSyncFromServerNow." );
#endif

                return false;
            }

            if (m_isSyncToServerNow)
            {
#if DEBUG
                Log.Information( "Is not allow to switch queue now, because _isSyncToServerNow." );
#endif
                return false;
            }

            if (!WebRestorable.IsInternetConnectionAvaliable())
            {
#if DEBUG
                Log.Information( "Is not allow to switch queue now, because no internet connection avaliable." );
#endif

                return false;
            }

            return (DateTime.UtcNow - m_timeOfLastAddedChangeType).TotalSeconds > TIMER_TO_TRY_ADD_TO_ACT_LIST_IN_SEC;
        }

        private List<ObjectChangeDescription> MoveDeletedToTop(List<ObjectChangeDescription> listForHandling)
        {
            var deleted = listForHandling.Where(x => x.Change.ChangeType == WatcherChangeTypes.Deleted).OrderBy(x => x.Change.FullPath.Count()).ToList();
            var other = listForHandling.Where(x => x.Change.ChangeType != WatcherChangeTypes.Deleted).ToList();

            other.InsertRange(0, deleted);

            return other;
        }

        private Int32 OrderByDeletedFirstDesc(FileSystemEventArgs change)
        {
            if (change.ChangeType == WatcherChangeTypes.Deleted)
            {
                // Try process deleted objects with shorter names.
                return 32 + change.FullPath.Count();
            }

            return (Int32)change.ChangeType;
        }

        private async void TrySwitchQueueTick(Object sender, EventArgs e)
        {
            if ( m_activeList.Any() || m_lockedFiles.Any() )
            {
                _ = await TryHandleChangesInSyncFolderAsync();
            }
        }

        private void ChangeFileListsAccordingToNewStates()
        {
            using ( m_lockHandleLockedFiles.Lock() )
            {
                foreach ( ObjectChangeDescription lockedFile in m_lockedFiles.Values.ToArray() )
                {
                    ObjectStateType objectStateNow = PathExtensions.GetObjectState(lockedFile.Change.FullPath);
                    if (objectStateNow != ObjectStateType.Locked)
                    {
                        _ = m_lockedFiles.TryRemove( lockedFile.Id );

                        if (!m_activeList.Any(activeFile => activeFile.Value.Change.FullPath.Equals(lockedFile)))
                        {
                            m_activeList.TryAdd( lockedFile.Id, lockedFile );
                        }
                    }
                }
            }
        }

        //we should first sync once to server to know that we uploaded all newer files
        private Boolean CanBeProcessedDownloadedNotMovedFiles =>
            (m_countSyncToServer >= 1) && m_downloadedNotMovedFile.Any();

        private async Task<Boolean> TryHandleChangesInSyncFolderAsync()
        {
            List<ObjectChangeDescription> listForHandling = null;

            Boolean isAllowedToHandleChangesInSyncFolder = IsAllowedToHandleChangesInSyncFolder();
            if ( isAllowedToHandleChangesInSyncFolder )
            {
                using ( await BackgroundSynchronizer.LockNotifyAndCheckSyncStart.LockAsync().ConfigureAwait( continueOnCapturedContext: false ) )
                {
                    if ( !IsSyncFromServerNow && !BackgroundSynchronizer.IsSyncToServerNow )
                    {
                        m_isSyncToServerNow = true;

                        //m_isSyncToServerNow can be changed in any tick(now it is impossible)
                        m_eventAggregator.GetEvent<IsSyncToServerChangedEvent>().Publish( payload: true );

                        listForHandling = new List<ObjectChangeDescription>( m_activeList.Values );
                        m_activeList.Clear();
                    }
                }
            }

            if (listForHandling != null)
            {
                try
                {
                    await HandleChangesInSyncFolder(listForHandling, BackgroundSynchronizer.SourceToCancelSyncToServer.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    ;//do nothing
                }
                finally
                {
                    m_isSyncToServerNow = false;
                    m_eventAggregator.GetEvent<IsSyncToServerChangedEvent>().Publish(payload: false);
                }
            }

            return isAllowedToHandleChangesInSyncFolder;
        }

        /// <param name="destFullFileName">
        /// Place where temp downloaded file should be moved
        /// </param>
        private Boolean IsDownloadedFileAvailableToBeMoved(DownloadingFileInfo downloadedNotMovedFile)
        {
            Func<ObjectChangeDescription, Boolean> predicate = objectChangeDescription => objectChangeDescription.Change.FullPath.Equals(downloadedNotMovedFile.LocalFilePath, StringComparison.OrdinalIgnoreCase);
            ObjectStateType objectState = PathExtensions.GetObjectState(downloadedNotMovedFile.LocalFilePath);

            Boolean shouldDownloadedFileBeMoved = objectState == ObjectStateType.Ok &&
                !m_lockedFiles.Values.AsParallel().Any(predicate) &&
                !m_activeList.Values.AsParallel().Any(predicate) &&
                !SyncingObjectsList.IsUploadingNow(downloadedNotMovedFile.LocalFilePath);

            if (shouldDownloadedFileBeMoved)
            {
                try
                {
                    IFileSystemFacade fileSystemFacade = AppSettings.ExportedValue<IFileSystemFacade>();
                    if (fileSystemFacade != null)
                    {
                        shouldDownloadedFileBeMoved = !fileSystemFacade.IsObjectHandling(downloadedNotMovedFile.LocalFilePath);
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogCriticalError(ex.Message, ex);
                }
            }

            return shouldDownloadedFileBeMoved;
        }

        private async Task HandleChangesInSyncFolder(List<ObjectChangeDescription> listForHandling, CancellationToken cancellationToken)
        {
            // If copy existing file (A) to some destination and move to the destination (B) the same one, NTFS will fire 3 events:
            // Delete B, delete A, create A in new destination. // TODO Test and investigate.
            //listForHandling = listForHandling.OrderBy(x => OrderByDeletedFirstDesc(x.Change)).ToList();
            //listForHandling = MoveDeletedToTop(listForHandling);

            // Catalogs should be created not async.
            for (Int32 i = 0; i < listForHandling.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (listForHandling[i].IsProcessed)
                {
                    continue;
                }

                ObjectStateType objectStateNow = PathExtensions.GetObjectState(listForHandling[i].Change.FullPath);

                // TODO 1.0 How about rename and move? Check the cases.

                if (objectStateNow == ObjectStateType.Locked)
                {
                    LoggingService.LogInfo($"File {listForHandling[i].Change.FullPath} is locked now...");

                    var lockedFile = listForHandling[ i ].Clone() as ObjectChangeDescription;
                    TryAddToChangeLockedList( lockedFile );

                    listForHandling[i].IsProcessed = true;
                }
                else
                {
                    try
                    {
                        // Try find delete event for the same object
                        switch (listForHandling[i].Change.ChangeType)
                        {
                            case WatcherChangeTypes.Created:
                                {
                                    ObjectChangeDescription possibleDeletedPair = listForHandling.FirstOrDefault(x =>
                                            x.Change.ChangeType == WatcherChangeTypes.Deleted &&
                                            Path.GetFileName(x.Change.FullPath) == Path.GetFileName(listForHandling[i].Change.FullPath));

                                    if (possibleDeletedPair != null)
                                    {
                                        // TODO Release 2.0 How about move and rename at the same time?
                                        _ = await ApiClient.MoveAsync(possibleDeletedPair.OriginalFullPath, listForHandling[i].Change.FullPath);

                                        possibleDeletedPair.IsProcessed = true;
                                    }
                                    else
                                    {
                                        await PerformApiAction(listForHandling[i]);
                                    }

                                    break;
                                }

                            case WatcherChangeTypes.Deleted:
                                {
                                    String currentFullPathItem = listForHandling[i].OriginalFullPath;

                                    String possibleIgnorePath = m_renamedPathesToIgnore.Values.FirstOrDefault(x => x == currentFullPathItem);

                                    if (possibleIgnorePath == null)
                                    {
                                        ObjectChangeDescription possibleCreatedPair = listForHandling.FirstOrDefault(x =>
                                                x.Change.ChangeType == WatcherChangeTypes.Created &&
                                                Path.GetFileName(x.Change.FullPath) == Path.GetFileName(listForHandling[i].Change.FullPath));

                                        if (possibleCreatedPair != null) // The same object was created
                                        {
                                            _ = await ApiClient.MoveAsync(listForHandling[i].Change.FullPath, possibleCreatedPair.OriginalFullPath);

                                            possibleCreatedPair.IsProcessed = true;
                                        }
                                        else
                                        {
                                            // NOTE. The idea is next - let's find siblings cuz we can do call for all siblings.
                                            // Then let's mark as processed all sub objects of this siblings, cuz server already deleted specified objects and respectively all children.

                                            IEnumerable<ObjectChangeDescription> allObjectsToDelete = listForHandling.Where(x => !x.IsProcessed &&
                                                                                x.Change.ChangeType == WatcherChangeTypes.Deleted);

                                            DeleteResponse response = await ApiClient.DeleteAsync(allObjectsToDelete.Select(x => x.OriginalFullPath).ToArray());

                                            if (response.IsSuccess)
                                            {
                                                allObjectsToDelete.ForEach(x => x.IsProcessed = true);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        _ = m_renamedPathesToIgnore.TryRemove(possibleIgnorePath);
                                    }

                                    break;
                                }

                            case WatcherChangeTypes.Renamed:
                                {
                                    String oldFullPath = (listForHandling[i].Change as RenamedEventArgs).OldFullPath;

                                    // NOTE: Check that the file was created just before renaming.
                                    ObjectChangeDescription createdBeforeRenaming =
                                            listForHandling.SingleOrDefault(x => x.OriginalFullPath == oldFullPath && x.Change.ChangeType == WatcherChangeTypes.Created && !x.IsProcessed);

                                    if ((createdBeforeRenaming != null) || PathExtensions.IsTemporaryFileName(oldFullPath))
                                    {
                                        //maybe it should be replaced in row after upload
                                        if (createdBeforeRenaming != null)
                                        {
                                            createdBeforeRenaming.IsProcessed = true;
                                        }

                                        FileInfo fileInfo = FileInfoHelper.TryGetFileInfo(listForHandling[i].Change.FullPath);

                                        if (fileInfo != null)
                                        {
                                            Log.Warning($"'{listForHandling[i].Change.FullPath}' was just created before renaming or just renamed from temp file.");
                                            Log.Warning("File was renamed but will be uploaded...");
                                            _ = await ApiClient.TryUploadAsync(fileInfo);
                                        }
                                    }
                                    else
                                    {
                                        await PerformApiAction(listForHandling[i]);
                                    }

                                    break;
                                }

                            default:
                                await PerformApiAction(listForHandling[i]);
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        var clonedObjectChangeDescription = listForHandling[ i ].Clone() as ObjectChangeDescription;
                        //exception should be logged in higher position of stack trace
                        TryAddToActiveList( clonedObjectChangeDescription );
                    }
                    finally
                    {
                        listForHandling[i].IsProcessed = true;
                    }
                }
            }

            var listToRemove = new ObjectChangeDescription[listForHandling.Count];
            listForHandling.CopyTo(listToRemove);
            foreach (ObjectChangeDescription element in listToRemove)
            {
                if (element.IsProcessed)
                {
                    listForHandling.Remove(element);
                }
            }

            if (listForHandling.Any())
            {
                await HandleChangesInSyncFolder(listForHandling, cancellationToken).ConfigureAwait(false);
            }

            LoggingService.LogInfo("Active queue with NTFS changes was processed.");
        }

        private async Task PerformApiAction( ObjectChangeDescription fileChange )
        {
            String fullPath = fileChange.Change.FullPath;

            switch ( fileChange.Change.ChangeType )
            {
                case WatcherChangeTypes.Created:
                {
                    ObjectStateType objectState = PathExtensions.GetObjectState( fullPath );

                    if ( objectState == ObjectStateType.Ok )
                    {
                        if ( Directory.Exists( fullPath ) )
                        {
                            _ = await ApiClient.CreateDirectoryOnServerAsync( fullPath );
                        }
                        else
                        {
                            String localPathMarker = AdsExtensions.Read( fullPath, AdsExtensions.Stream.LocalPathMarker );

                            if ( String.IsNullOrEmpty( localPathMarker ) )
                            {
                                AdsExtensions.Write( fullPath, text: fullPath, AdsExtensions.Stream.LocalPathMarker ); // NOTE. For totally new file we just set up default local path marker.
                                localPathMarker = fullPath;
                            }

                            if ( localPathMarker != fullPath )
                            {
                                _ = await ApiClient.CopyAsync( localPathMarker, fullPath );
                            }
                            else
                            {
                                FileInfo fileInfo = FileInfoHelper.TryGetFileInfo( fullPath );

                                if ( fileInfo != null )
                                {
                                    ServerObjectDescription serverObjectDescription = await ApiClient.GetExistingObjectDescription( fullPath ).ConfigureAwait( false );

                                    Boolean shouldFileBeUploaded = serverObjectDescription.ShouldLocalFileBeUploaded( fileInfo );
                                    if ( shouldFileBeUploaded )
                                    {
                                        LoggingService.LogInfo( $"File '{fullPath}' was created and will be uploaded..." );

                                        _ = await ApiClient.TryUploadAsync( fileInfo ).ConfigureAwait( false );
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if ( objectState == ObjectStateType.Deleted )
                        {
                            //File was renamed or deleted before upload. Do nothing.
                            break;
                        }

                        TryAddToActiveList( fileChange );
                        LoggingService.LogInfo( $"File {fileChange.OriginalFullPath} has added with state {objectState} to {nameof( m_lockedFiles )}" );
                    }
                }
                break;

                case WatcherChangeTypes.Changed:
                {
                    await HandleChangedFile( fullPath );
                }
                break;

                case WatcherChangeTypes.Deleted:
                {
                    throw new InconsistencyException( $"Delete request should be processed earlier. File: {fileChange.OriginalFullPath}" );
                }

                case WatcherChangeTypes.Renamed:
                {
                    //TODO add support fast rename batch actions. Probably before general for combine all rename into one.
                    var renamedArgs = (RenamedEventArgs)fileChange.Change;

                    String oldFullPath = renamedArgs.OldFullPath;
                    if ( oldFullPath == renamedArgs.FullPath )
                    {
                        TryAddRenamedPathesToIgnore( oldFullPath );
                    }
                    else
                    {
                        // TODO Add offline rename
                        _ = await ApiClient.RenameAsync( renamedArgs.OldFullPath, renamedArgs.FullPath );
                    }
                }

                break;
                case WatcherChangeTypes.All:
                {
                    throw new ArgumentException( "How to handle WatcherChangeType.All?" );
                }
            }
        }

        private async Task HandleChangedFile( String fullPath )
        {
            if ( Directory.Exists( fullPath ) )
            {
                // ignore only directory change
            }
            else
            {
                FileInfo fileInfo = FileInfoHelper.TryGetFileInfo(fullPath);

                if (fileInfo != null)
                {
                    ServerObjectDescription objectDescrOnServer = await ApiClient.GetExistingObjectDescription(fullPath).ConfigureAwait(continueOnCapturedContext: false);

                    Boolean shouldFileBeUploaded = objectDescrOnServer.ShouldLocalFileBeUploaded(fileInfo);

                    //TODO: upload file even it is has the same size
                    if (shouldFileBeUploaded)
                    {
                        _ = await ApiClient.TryUploadAsync(fileInfo).ConfigureAwait(false);
                    }
                }
                //try
                //{
                //    ObjectNa
                //    ServerObjectDescription.CompareFileOnServerAndLocal(CurrentUserProvider.LoggedUser.Id, localFilePath, possibleDescriptionOnServer.Md5, possibleDescriptionOnServer.Version, out isChangedLocal, out areFilesIntegral);
                //}
                //catch (IOException ex)
                //{
                //    LoggingService.LogInfo(ex.Message);
                //    return;
                //}
                //catch (ArgumentException ex)
                //{
                //    LoggingService.LogInfo(ex.Message);
                //    return;
                //}

                //if (!areFilesIntegral && isChangedLocal)
                //{
                //    Console.WriteLine();
                //    Console.WriteLine($"localWriteTimeUtc for file {fileInfo.Name} is bigger than such date on server - so it will be deleted and upload to server.");

                //    var deleteResponse = await ApiClient.DeleteAsync(fullPath);
                //    //NotifyService.Notify(deleteResponse);

                //    var uploadResponse = await ApiClient.TryUploadAsync(fileInfo);
                //}
                //var fileInfo = FileInfoHelper.TryGetFileInfo(fileChange.Change.FullPath);

                //if (fileInfo != null)
                //{
                //    var resultOfDeleting = await ApiClient.DeleteAsync(fileInfo.FullName);
                //    NotifyService.Notify(resultOfDeleting);

                //    //var secondsWait = ApiSettings.SecondsToCacheServerList;
                //    //await Task.Delay(secondsWait * 10000);

                //    if (fileInfo.Length != 0)
                //    {
                //        Console.WriteLine($"PerformApiAction: File {fileInfo.FullName} was changed and will be uploaded...");
                //        var resultOfUploading = await ApiClient.TryUploadAsync(fileInfo);
                //        NotifyService.Notify(resultOfUploading);
                //    }
                //}
                //else
                //{
                //    // do nothing.
                //}
            }
        }
    }
}
