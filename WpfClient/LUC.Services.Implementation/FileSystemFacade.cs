using LUC.Interfaces;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;
using LUC.Services.Implementation.Helpers;

using Serilog;
using Serilog.Events;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Windows.Threading;

namespace LUC.Services.Implementation
{
    [Export(typeof(IFileSystemFacade))]
    public class FileSystemFacade : IFileSystemFacade
    {
        #region Fields

        private readonly Object m_lockHandleWatcherEvent = new Object();

        private readonly Dictionary<String, DateTime> m_lastReadyToLockTime = new Dictionary<String, DateTime>();
        private readonly Dictionary<String, DateTime> m_lastReadyToUnLockTime = new Dictionary<String, DateTime>();

        private readonly ConcurrentDictionary<Double, FileSystemEventArgs> m_watcherJournal;

        private readonly IPathFiltrator m_pathFiltrator;
        private readonly ILoggingService m_loggingService;

        private Boolean m_isWatcherWork = false;

        private FileSystemWatcher m_watcher;

        #endregion

        #region Constructors

        [ImportingConstructor]
        public FileSystemFacade(IPathFiltrator pathFiltrator)
        {
            m_loggingService = new LoggingService();
            m_pathFiltrator = pathFiltrator;
            m_watcherJournal = new ConcurrentDictionary<Double, FileSystemEventArgs>();
        }

        #endregion

        #region Properties

        [Import(typeof(IApiClient))]
        private IApiClient ApiClient { get; set; }

        [Import(typeof(IBackgroundSynchronizer))]
        public IBackgroundSynchronizer BackgroundSynchronizer { get; set; }

        [Import(typeof(IFileChangesQueue))]
        public IFileChangesQueue Queue { get; set; }

        [Import(typeof(ISyncingObjectsList))]
        public ISyncingObjectsList SyncingObjectsList { get; set; }

        [Import(typeof(INotifyService))]
        public INotifyService NotifyService { get; set; }

        [Import(typeof(ICurrentUserProvider))]
        public ICurrentUserProvider CurrentUserProvider { get; set; }

        #endregion

        public void RunMonitoring()
        {
            m_watcher = new FileSystemWatcher(CurrentUserProvider.RootFolderPath)
            {
                IncludeSubdirectories = true,

                NotifyFilter = NotifyFilters.Attributes
                               | NotifyFilters.DirectoryName
                               | NotifyFilters.FileName
                               | NotifyFilters.LastWrite
                               | NotifyFilters.Size,
            };

            m_watcher.Created += Watcher_Event;
            m_watcher.Deleted += Watcher_Event;
            m_watcher.Changed += Watcher_Event;
            m_watcher.Error += Watcher_Error;
            m_watcher.Renamed += Watcher_Event;
            m_watcher.Disposed += Watcher_Disposed;

            m_watcher.EnableRaisingEvents = true;
        }

        public Boolean IsObjectHandling(String fullObjectName)
        {
            Boolean isObjectHandling = default;

            Parallel.ForEach(m_watcherJournal.Values, (eventArgs, loopState) =>
            {
                if (eventArgs.FullPath.IsEqualFilePathesInCurrentOs(fullObjectName))
                {
                    isObjectHandling = true;
                    loopState.Stop();
                }
            });

            return isObjectHandling;
        }

        public void StopMonitoring()
        {
            if (m_watcher != null)
            {
                m_watcher.EnableRaisingEvents = false;
                m_watcher.Created -= Watcher_Event;
                m_watcher.Deleted -= Watcher_Event;
                m_watcher.Changed -= Watcher_Event;
                m_watcher.Error -= Watcher_Error;
                m_watcher.Renamed -= Watcher_Event;
                m_watcher.Disposed -= Watcher_Disposed;
            }

            Queue.Clear();
            SyncingObjectsList.Clear();
        }

        private void Watcher_Event( Object sender, FileSystemEventArgs args )
        {
            Boolean isChangedUploadingFile = (args.ChangeType == WatcherChangeTypes.Changed) && SyncingObjectsList.IsUploadingNow(args.FullPath);

            Boolean shouldAlwaysBeIgnored = isChangedUploadingFile || !IsValidPath(args.FullPath) || !m_pathFiltrator.IsPathPertinent(args.FullPath);
            if (!shouldAlwaysBeIgnored)
            {
                lock (m_lockHandleWatcherEvent)
                {
                    Double utcNow;
                    do
                    {
                        utcNow = (DateTime.UtcNow - DateTime.Today).TotalMilliseconds;
                    }
                    while (m_watcherJournal.ContainsKey(utcNow));

                    m_watcherJournal.TryAdd(utcNow, args);

                    if (!m_isWatcherWork)
                    {
                        Watcher_Event_Handler(m_watcherJournal);
                    }
                }
            }
        }

        private void Watcher_Event_Handler(ConcurrentDictionary<Double, FileSystemEventArgs> watcherJournal)
        {
            System.Threading.Volatile.Write(ref m_isWatcherWork, true);

            if (watcherJournal.Count == 0)
            {
                System.Threading.Volatile.Write(ref m_isWatcherWork, false);
                return;
            }

            foreach (FileSystemEventArgs watcherEvent in watcherJournal.Values)
            {
                switch (watcherEvent.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                        {
                            Watcher_Created(watcherEvent);
                        }

                        break;

                    case WatcherChangeTypes.Deleted:
                        {
                            Watcher_Deleted(watcherEvent);
                        }

                        break;

                    case WatcherChangeTypes.Changed:
                        {
                            Watcher_Changed(watcherEvent);
                        }

                        break;
                    case WatcherChangeTypes.Renamed:
                        {
                            Watcher_Renamed((RenamedEventArgs)watcherEvent);
                        }

                        break;

                    case WatcherChangeTypes.All:
                        break;
                        
                    default:
                        Log.Warning( $"Watcher: Unconditional Event for {watcherEvent.GetType().Name}" );
                        break;
                }
            }

            watcherJournal.Clear();
            System.Threading.Volatile.Write(ref m_isWatcherWork, false);
        }

        private void Watcher_Renamed(RenamedEventArgs args)
        {
            try
            {
                if (PathExtensions.IsIgnorableExtension(args.OldFullPath))
                {
                    return;
                }
                else
                {
                    Tuple<String, String> maybeAlreadyRenamed = SyncingObjectsList.TryFindRenamedFile(args.OldFullPath, args.FullPath);

                    if (File.Exists(args.FullPath) && maybeAlreadyRenamed == null)
                    {
                        AdsExtensions.Write(args.FullPath, text: args.FullPath, AdsExtensions.Stream.LocalPathMarker);
                    }

                    String oldFileName = Path.GetFileName(args.OldFullPath);

                    if (PathExtensions.IsTemporaryFileName(oldFileName))
                    {
                        m_loggingService.LogInfo($"Watcher: Raised Renamed Event for {args.OldFullPath} -> {args.FullPath}");
                        //var createdEventArgs = new FileSystemEventArgs( WatcherChangeTypes.Created, CurrentUserProvider.RootFolderPath, args.Name );
                        Queue.AddEvent(args);
                    }
                    else
                    {
                        if (maybeAlreadyRenamed == null)
                        {
                            m_loggingService.LogInfo($"Watcher: Raised Renamed Event for {args.OldFullPath} -> {args.FullPath}");

                            SyncingObjectsList.CancelDownloadingAllFilesWhichBelongPath(args.OldFullPath);

                            Queue.AddEvent(args);
                        }
                        else
                        {
                            m_loggingService.LogError($"Event was not added to the queue. maybeAlreadyRenamed = {maybeAlreadyRenamed.Item1} , {maybeAlreadyRenamed.Item2}");
                            SyncingObjectsList.RemoveRenamedFile(args.OldFullPath, args.FullPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                m_loggingService.LogCriticalError(ex);
            }
        }

        private void Watcher_Error(Object sender, ErrorEventArgs args)
        {
            NotifyService.NotifyStaticMessage(args.GetException().Message);

            m_loggingService.LogCriticalError(args.GetException());
        }

        private void Watcher_Disposed(Object sender, EventArgs e) => m_loggingService.LogInfo($"'Disposed' Event was caught from FileSystemFacade");

        private void Watcher_Changed(FileSystemEventArgs args)
        {
            try
            {
                if (Directory.Exists(args.FullPath))
                {
                    return;
                }

                ILockDescription lockDesc = AdsExtensions.ReadLockDescription(args.FullPath);

                if (lockDesc.LockState == AdsLockState.LockedOnServer)
                {
                    return;
                }

                switch (lockDesc.LockState)
                {
                    case AdsLockState.ReadyToLock:
                        {
                            DateTime utcNow = DateTime.UtcNow;

                            // TODO Release 2.0 Support lock/unlock for several files at the time.
                            // NOTE. We should do lock asap but ignore very next events for local ready_to_lock state.
                            if (!m_lastReadyToLockTime.ContainsKey(args.FullPath) || (utcNow - m_lastReadyToLockTime[args.FullPath]).TotalSeconds > 3)
                            {
                                _ = Task.Run(() => ApiClient.LockFile(args.FullPath));
                            }

                            m_lastReadyToLockTime[args.FullPath] = utcNow;
                            break;
                        }

                    case AdsLockState.ReadyToUnlock:
                        {
                            DateTime utcNow = DateTime.UtcNow;
                            if (!m_lastReadyToUnLockTime.ContainsKey(args.FullPath) || (utcNow - m_lastReadyToUnLockTime[args.FullPath]).TotalSeconds > 3)
                            {
                                _ = Task.Run(() => ApiClient.UnlockFile(args.FullPath));
                            }

                            m_lastReadyToUnLockTime[args.FullPath] = utcNow;

                            break;
                        }

                    default:
                        {
                            DownloadingFileInfo info = SyncingObjectsList.TryFindDownloadingFile(args.FullPath);
                            //try add to ServerObjectDescription property Md5
                            //var descFileOnServer = objectNameProvider.GetExistingObjectDescription(args.FullPath).Result;
                            //TODO:
                            //add method CompareFileOnServerAndLocal(out isChangedLocal, out areFilesIntegral) to class ServerObjectDescription
                            //if isChangedLocal and areFilesIntegral then 
                            //Console.WriteLine($"Queue-> Added changed event: {DateTime.Now} {args.FullPath}");
                            //Queue.AddEvent(args);
                            switch (info)
                            {
                                case null:
                                    Queue.AddEvent(args);
                                    break;
                                default:
                                    if (info.IsDownloaded)
                                    {
                                        m_loggingService.LogInfo($"Watcher: RemoveDownloadingFile -> {args.FullPath} IsDownloaded => {info.IsDownloaded}");

                                        SyncingObjectsList.RemoveDownloadingFile(info);
                                    }
                                    else
                                    {
                                        // do nothing
                                    }

                                    break;
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                m_loggingService.LogCriticalError(ex);
            }
        }

        private void Watcher_Deleted(FileSystemEventArgs args)
        {
            try
            {
                m_loggingService.LogInfo($"Watcher: Raised Deleted Event for {args.FullPath}");

                String deletingFilePath = SyncingObjectsList.TryFindDeletingFile(args.FullPath);
                String deletingDirectory = SyncingObjectsList.TryFindDeletingDirectory(args.FullPath);

                if (deletingFilePath == null && deletingDirectory == null)
                {
                    Queue.AddEvent(args); // handle delete
                }
                else
                {
                    if (deletingFilePath != null)
                    {
                        SyncingObjectsList.RemoveDeletingFile(deletingFilePath); // not handle delete and forget about delete from application.
                    }

                    if (deletingDirectory != null)
                    {
                        SyncingObjectsList.RemoveDeletingDirectory(deletingDirectory); // not handle delete and forget about delete from application.
                    }
                }
            }
            catch (Exception ex)
            {
                m_loggingService.LogCriticalError(ex);
            }
        }

        private void Watcher_Created(FileSystemEventArgs args)
        {
            try
            {
                DownloadingFileInfo info = SyncingObjectsList.TryFindDownloadingFile(args.FullPath);

                String directoryName = SyncingObjectsList.TryFindCreatingDirectory(args.FullPath);

                // We should ignore file which uploaded by application or directory which created by application
                if (info == null && directoryName == null)
                {
#if DEBUG
                    m_loggingService.LogInfo( $"Queue-> Added created event: {DateTime.Now} {args.FullPath}" );
#endif

                    if (File.Exists(args.FullPath))
                    {
                        AdsExtensions.Remove(args.FullPath, AdsExtensions.Stream.Guid);
                    }

                    Queue.AddEvent(args);
                }

                if (directoryName != null)
                {
                    SyncingObjectsList.RemoveCreatingDirectory(directoryName);
                }

                // NOTE downloading files should be deleted after event changed for the file.
            }
            catch (Exception ex)
            {
                m_loggingService.LogCriticalError(ex);
            }
        }

        //TODO: replace in PathFiltrator class
        private Boolean IsValidPath(String fullPath)
        {
            Boolean isInvalidPath = IsOutsideBucketPath(fullPath) ||
                         SyncingObjectsList.TryFindDownloadingFile(fullPath) != null ||
                         fullPath.IsJunctionDirectory() ||
                         PathExtensions.IsIgnorableExtension(fullPath) ||
                         PathExtensions.IsTemporaryFileName(fullPath);

            return !isInvalidPath;
        }

        private Boolean IsOutsideBucketPath(String fullPath)
        {
            IList<String> bucketPathes = CurrentUserProvider.ProvideBucketDirectoryPaths();

            foreach (String bucket in bucketPathes)
            {
                Int32 indexOf = fullPath.ToLowerInvariant().IndexOf(bucket.ToLowerInvariant());

                if (bucket == fullPath)
                {
                    return true;
                }

                if (indexOf == 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
