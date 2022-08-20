using LUC.Interfaces;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

using Serilog;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Security;

namespace LUC.Services.Implementation
{
    [Export( typeof( ISyncingObjectsList ) )]
    public class SyncingObjectsList : ISyncingObjectsList
    {
        private readonly Object m_lockIsUploadingNow;

        private readonly List<DownloadingFileInfo> m_downloadingFiles;

        private readonly List<String> m_internalCreatedDirectories;

        private readonly List<String> m_internalDeletingFiles;
        private readonly List<String> m_internalDeletingDirectories;

        private List<Tuple<String, String>> m_internalRenamedFiles;

        private List<String> m_uploadingFiles;
        private readonly Object m_lockObject = new Object();

        [ImportingConstructor]
        public SyncingObjectsList( ICurrentUserProvider currentUserProvider )
        {
            CurrentUserProvider = currentUserProvider;
            CurrentUserProvider.RootFolderPathChanged += ( s, e ) => CancelDownloadingAllFilesWhichBelongPath( e.OldRootFolder );

            m_lockIsUploadingNow = new Object();

            m_downloadingFiles = new List<DownloadingFileInfo>();
            m_internalCreatedDirectories = new List<String>();
            m_internalDeletingFiles = new List<String>();
            m_internalDeletingDirectories = new List<String>();
            m_internalRenamedFiles = new List<Tuple<String, String>>();
            m_uploadingFiles = new List<String>();
        }

        private ICurrentUserProvider CurrentUserProvider { get; }

        public INotificationResult RenameFileOnlyLocal( FileInfo fileInfo, String destinationFileName, DateTime writeTimeUtc, String guid )
        {
            if ( fileInfo == null )
            {
                throw new ArgumentNullException( nameof( fileInfo ) );
            }

            if ( fileInfo.Name == destinationFileName )
            {
                Log.Warning( $"Attempt to rename file on the same name. {fileInfo.FullName}" );
                return new NotificationResult { IsSuccess = false };
            }

            String destinationPath = Path.Combine( fileInfo.DirectoryName, destinationFileName );
            if ( File.Exists( destinationPath ) || Directory.Exists( destinationPath ) )
            {
                return new NotificationResult { IsSuccess = false };
            }

            // NTFS raises 3 events: delete, change, rename. We should handle them internally.
            AddDeletingFile( fileInfo.FullName );

            var downloadingFileInfo = new DownloadingFileInfo
            {
                IsDownloaded = true, // TODO Test, check that true should be later or not.
                LocalFilePath = destinationFileName
            };

            AddDownloadingFile( downloadingFileInfo );
            AddRenamedFile( fileInfo.FullName, destinationPath );

            try
            {
                fileInfo.MoveTo( destinationPath );
                File.SetCreationTimeUtc( destinationPath, writeTimeUtc );
                File.SetLastWriteTimeUtc( destinationPath, writeTimeUtc );

                if ( !String.IsNullOrEmpty( guid ) )
                {
                    AdsExtensions.WriteGuidAndLocalPathMarkersIfNotTheSame( destinationPath, guid );
                }

                return new NotificationResult { IsSuccess = true };
            }
            catch ( IOException )
            {
                RemoveDeletingFile( fileInfo.FullName );
                RemoveDownloadingFile( downloadingFileInfo );
                RemoveRenamedFile( fileInfo.FullName, destinationFileName );

                return new NotificationResult { IsSuccess = false };
            }
            catch ( SecurityException )
            {
                RemoveDeletingFile( fileInfo.FullName );
                RemoveDownloadingFile( downloadingFileInfo );
                RemoveRenamedFile( fileInfo.FullName, destinationFileName );

                return new NotificationResult { IsSuccess = false };
            }
            catch ( UnauthorizedAccessException )
            {
                RemoveDeletingFile( fileInfo.FullName );
                RemoveDownloadingFile( downloadingFileInfo );
                RemoveRenamedFile( fileInfo.FullName, destinationFileName );

                return new NotificationResult { IsSuccess = false };
            }
        }

        public void TryCancelAllDownloadingFilesWithDifferentVersions( ObjectsListResponse objectsListResponse, String bucketServerName, out Boolean isCancelledAnyDownload )
        {
            var objectsListModel = objectsListResponse?.ToObjectsListModel();
            TryCancelAllDownloadingFilesWithDifferentVersions( objectsListModel, bucketServerName, out isCancelledAnyDownload );
        }

        public void TryCancelAllDownloadingFilesWithDifferentVersions( ObjectsListModel objectsListModel, String bucketServerName, out Boolean isCancelledAnyDownload )
        {
            isCancelledAnyDownload = false;

            if ( objectsListModel != null )
            {
                if ( !String.IsNullOrWhiteSpace( bucketServerName ) )
                {
                    if ( objectsListModel.ObjectDescriptions.Count > 0 )
                    {
                        lock ( m_lockObject )
                        {
                            if ( m_downloadingFiles.Count > 0 )
                            {
                                String bucketDirectory = CurrentUserProvider.LocalBucketPath( bucketServerName );
                                String filePrefix = objectsListModel.RequestedPrefix.FromHexString();

                                String localFolderPath = String.IsNullOrWhiteSpace( filePrefix ) ?
                                    bucketDirectory :
                                    Path.Combine( bucketDirectory, filePrefix );

                                IEnumerable<DownloadingFileInfo> downloadingFiles = m_downloadingFiles.Where( d =>
                                    Path.GetDirectoryName( d.LocalFilePath ).Equals( localFolderPath, StringComparison.OrdinalIgnoreCase ) );

                                foreach ( DownloadingFileInfo fileWhichIsDownloading in downloadingFiles )
                                {
                                    String fileName = Path.GetFileName( fileWhichIsDownloading.LocalFilePath );

                                    Boolean isFileDifferentInServer = objectsListModel.ObjectDescriptions.Any( o =>
                                        fileName.Equals( o.OriginalName, StringComparison.Ordinal ) &&
                                        ( o.IsDeleted || !o.Version.Equals( fileWhichIsDownloading.Version, StringComparison.Ordinal ) )
                                    );

                                    if ( isFileDifferentInServer )
                                    {
                                        fileWhichIsDownloading.SourceToCancelDownload.Cancel();
                                        isCancelledAnyDownload = true;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    throw new ArgumentException( message: "Is null or white space", nameof( bucketServerName ) );
                }
            }
            else
            {
                throw new ArgumentNullException( nameof( objectsListModel ) );
            }
        }

        public void CancelDownloadingAllFilesWhichBelongPath( String path )
        {
            if ( !String.IsNullOrWhiteSpace( path ) )
            {
                lock ( m_lockObject )
                {
                    ICollection<DownloadingFileInfo> downloadingfilesWhichShouldBeCanceled = m_downloadingFiles.Where( x => x.LocalFilePath.Contains( path ) ).ToList();

                    foreach ( DownloadingFileInfo fileInfo in downloadingfilesWhichShouldBeCanceled )
                    {
                        fileInfo.SourceToCancelDownload.Cancel();
                    }

                    m_downloadingFiles.RemoveRange( downloadingfilesWhichShouldBeCanceled );
                }
            }
        }

        public Boolean TryDeleteFile( String filePath )
        {
            try
            {
                AddDeletingFile( filePath );
                File.Delete( filePath );
                return true;
            }
            catch ( IOException ex ) // File could be locked.
            {
#if DEBUG
                Console.WriteLine( ex.Message );
#endif
                RemoveDeletingFile( filePath );
                return false;
            }
            catch ( UnauthorizedAccessException ) // File may have attribute readonly.
            {
                RemoveDeletingFile( filePath );
                return false;
            }
        }

        public void Clear()
        {
            CancelDownloadingAllFilesWhichBelongPath( CurrentUserProvider.RootFolderPath );
            m_downloadingFiles.Clear();

            m_internalCreatedDirectories.Clear();

            m_internalDeletingFiles.Clear();
            m_internalDeletingDirectories.Clear();

            m_internalRenamedFiles.Clear();

            m_uploadingFiles.Clear();
        }

        // TODO Investigate added already exception. First case - failed renaming. It's fixed. Something other?
        public void AddUploadingFile( String path )
        {
            lock ( m_lockObject )
            {
                if ( m_uploadingFiles.Any( x => x == path ) )
                {
                    return;
                    throw new ArgumentException( $"{path} was added already to {nameof( m_uploadingFiles )}." );
                }

                m_uploadingFiles.Add( path );
            }
        }

        public void AddCreatingDirectory( String directoryPath )
        {
            lock ( m_lockObject )
            {
                if ( m_internalCreatedDirectories.Any( x => x == directoryPath ) )
                {
                    return;
                    throw new ArgumentException( $"{directoryPath} was added already to {nameof( m_internalCreatedDirectories )}." );
                }

                m_internalCreatedDirectories.Add( directoryPath );
            }
        }

        public void AddDeletingDirectories( String[] directoryPathes )
        {
            lock ( m_lockObject )
            {
                if ( m_internalDeletingDirectories.Any( x => directoryPathes.Any( y => y == x ) ) )
                {
                    return;
                    throw new ArgumentException( $"some files was added already to {nameof( m_internalDeletingDirectories )}." );
                }

                m_internalDeletingDirectories.AddRange( directoryPathes );
            }
        }

        public void AddDeletingDirectory( String directoryPath )
        {
            lock ( m_lockObject )
            {
                if ( m_internalDeletingDirectories.Any( x => x == directoryPath ) )
                {
                    return;
                    throw new ArgumentException( $"{directoryPath} was added already to {nameof( m_internalDeletingDirectories )}." );
                }

                m_internalDeletingDirectories.Add( directoryPath );
            }
        }

        public void AddDeletingFiles( String[] localFullPathes )
        {
            lock ( m_lockObject )
            {
                if ( m_internalDeletingFiles.Any( x => localFullPathes.Any( y => y == x ) ) )
                {
                    return;
                    throw new ArgumentException( $"some files was added already to {nameof( m_internalDeletingFiles )}." );
                }

                m_internalDeletingFiles.AddRange( localFullPathes );
            }
        }

        public void AddDeletingFile( String localFullPath )
        {
            lock ( m_lockObject )
            {
                if ( m_internalDeletingFiles.Any( x => x == localFullPath ) )
                {
                    return;
                    throw new ArgumentException( $"{localFullPath} was added already to {nameof( m_internalDeletingFiles )}." );
                }

                m_internalDeletingFiles.Add( localFullPath );
            }
        }

        public void AddDownloadingFile( DownloadingFileInfo downloadingFile )
        {
            lock ( m_lockObject )
            {
                if ( m_downloadingFiles.Any( x => x.LocalFilePath == downloadingFile.LocalFilePath ) )
                {
                    return;
                    throw new ArgumentException( $"{downloadingFile.LocalFilePath} was added already to {nameof( m_downloadingFiles )}." );
                }


                m_downloadingFiles.Add( downloadingFile );
            }
        }

        public void AddRenamedFile( String fullPathFrom, String fullPathTo )
        {
            lock ( m_lockObject )
            {
                if ( m_internalRenamedFiles.Any( x => x.Item1 == fullPathFrom && x.Item2 == fullPathTo ) )
                {
                    return;
                    throw new ArgumentException( $"Renaming from {fullPathFrom} to {fullPathTo} was added already to {nameof( m_internalRenamedFiles )}." );
                }

                m_internalRenamedFiles.Add( new Tuple<String, String>( fullPathFrom, fullPathTo ) );
            }
        }

        public Boolean HasDownloadingFiles() =>
            // Files with size like 1Gb may download relatively long.
            m_downloadingFiles.Any( x => !x.IsDownloaded );

        public void RemoveCreatingDirectory( String directoryPath )
        {
            lock ( m_lockObject )
            {
                _ = m_internalCreatedDirectories.Remove( directoryPath );
            }
        }

        public void RemoveDeletingDirectory( String directoryPath )
        {
            lock ( m_lockObject )
            {
                _ = m_internalDeletingDirectories.Remove( directoryPath );
            }
        }

        public void RemoveDeletingFile( String localFullPath )
        {
            lock ( m_lockObject )
            {
                _ = m_internalDeletingFiles.Remove( localFullPath );
            }
        }

        public void RemoveDownloadingFile( DownloadingFileInfo downloadingFile )
        {
            lock ( m_lockObject )
            {
                downloadingFile.Dispose();
                _ = m_downloadingFiles.Remove( downloadingFile );
            }
        }

        public void RemoveUploadingFile( String path )
        {
            lock ( m_lockObject )
            {
                _ = m_uploadingFiles.Remove( path );
            }
        }

        public void RemoveRenamedFile( String fullPathFrom, String fullPathTo )
        {
            lock ( m_lockObject )
            {
                Tuple<String, String> item = m_internalRenamedFiles.Single( x => x.Item1 == fullPathFrom && x.Item2 == fullPathTo );
                _ = m_internalRenamedFiles.Remove( item );
            }
        }

        public String TryFindCreatingDirectory( String directoryPath )
        {
            lock ( m_lockObject )
            {
                String result = m_internalCreatedDirectories.LastOrDefault( x => x == directoryPath );

                return result;
            }
        }

        public String TryFindDeletingDirectory( String directoryPath )
        {
            lock ( m_lockObject )
            {
                String result = m_internalDeletingDirectories.LastOrDefault( x => x == directoryPath );

                return result;
            }
        }

        public String TryFindDeletingFile( String localFullPath )
        {
            lock ( m_lockObject )
            {
                String result = m_internalDeletingFiles.LastOrDefault( x => x == localFullPath );

                return result;
            }
        }

        public Boolean IsUploadingNow( String path )
        {
            lock ( m_lockIsUploadingNow )
            {
                Boolean result = m_uploadingFiles.Any( x => x == path );

                return result;
            }
        }

        public DownloadingFileInfo TryFindDownloadingFile( String filePath )
        {
            lock ( m_lockObject )
            {
                DownloadingFileInfo result = m_downloadingFiles.LastOrDefault( x => x.LocalFilePath == filePath );

                return result;
            }
        }

        public Tuple<String, String> TryFindRenamedFile( String fullPathFrom, String fullPathTo )
        {
            Tuple<String, String> result;
            lock ( m_lockObject )
            {
                result = m_internalRenamedFiles.SingleOrDefault( x => x.Item1.IsEqualFilePathesInCurrentOs( fullPathFrom ) && x.Item2.IsEqualFilePathesInCurrentOs( fullPathTo ) );
            }

            return result;
        }
    }
}
