using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

using System;
using System.IO;

namespace LUC.Interfaces
{
    public interface ISyncingObjectsList
    {
        void AddDownloadingFile( DownloadingFileInfo downloadingFile );

        void RemoveDownloadingFile( DownloadingFileInfo downloadingFile );

        void CancelDownloadingAllFilesWhichBelongPath( String path );

        DownloadingFileInfo TryFindDownloadingFile( String filePath );

        void TryCancelAllDownloadingFilesWithDifferentVersions( ObjectsListResponse objectsListResponse, String bucketServerName, out Boolean isCancelledAnyDownload );

        void TryCancelAllDownloadingFilesWithDifferentVersions( ObjectsListModel objectsListModel, String bucketServerName, out Boolean isCancelledAnyDownload );

        Boolean HasDownloadingFiles();

        void Clear();

        INotificationResult RenameFileOnlyLocal( FileInfo fileInfo, String destinationFileName, DateTime writeTimeUtc, String guid );

        Boolean TryDeleteFile( String filePath );

        void AddDeletingFile( String localFullPath );

        void AddDeletingFiles( String[] localFullPathes );

        void RemoveDeletingFile( String localFullPath );

        String TryFindDeletingFile( String localFullPath );

        void AddCreatingDirectory( String directoryPath );

        void RemoveCreatingDirectory( String directoryPath );

        String TryFindCreatingDirectory( String directoryPath );

        void AddDeletingDirectory( String directoryPath );

        void AddDeletingDirectories( String[] directoryPathes );

        void RemoveDeletingDirectory( String directoryPath );

        String TryFindDeletingDirectory( String directoryPath );

        void AddRenamedFile( String fullPathFrom, String fullPathTo );

        void RemoveRenamedFile( String fullPathFrom, String fullPathTo );

        Tuple<String, String> TryFindRenamedFile( String fullPathFrom, String fullPathTo );

        void RemoveUploadingFile( String path );

        void AddUploadingFile( String path );

        Boolean IsUploadingNow( String path );
    }
}
