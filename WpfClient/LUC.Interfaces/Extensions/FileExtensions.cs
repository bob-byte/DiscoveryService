using LUC.Interfaces.Constants;
using LUC.Interfaces.Models;

using Serilog;

using System;
using System.IO;

namespace LUC.Interfaces.Extensions
{
    public static class FileExtensions
    {
        public static void SetDownloadedFileToNormal( DownloadingFileInfo downloadedFileInfo, IFileChangesQueue fileChangesQueue = null )
        {
            //case when free drive space is too small for this file (we download immediatyly in downloadedFileInfo.LocalFilePath)
            if ( downloadedFileInfo.LocalFilePath.IsEqualFilePathesInCurrentOs( downloadedFileInfo.PathWhereDownloadFileFirst ) )
            {
                File.SetAttributes( downloadedFileInfo.LocalFilePath, FileAttributes.Normal );
            }
            else
            {
                try
                {
                    SetTempFileToNormal( downloadedFileInfo.PathWhereDownloadFileFirst, downloadedFileInfo.LocalFilePath );
                    fileChangesQueue?.TryRemoveDownloadedNotMovedFile( downloadedFileInfo, isRemoved: out _ );
                }
                catch
                {
                    fileChangesQueue?.AddDownloadedNotMovedFile( downloadedFileInfo );
                    throw;
                }
            }
        }

        public static void SetTempFileToNormal( String tempFullFileName, String normalFullFileName )
        {
            //we cannot to rename temp file in normal name if file with the last one already exists
            if ( File.Exists( normalFullFileName ) )
            {
                TryRemoveAttributeIfItExists( normalFullFileName, FileAttributes.ReadOnly, out Boolean? isStillAttributeInFile );

                if ( isStillAttributeInFile == false )
                {
                    File.Delete( normalFullFileName );
                }
                else
                {
                    throw new IOException( message: "Cannot to delete readonly attribute" );
                }
            }

            File.Move( tempFullFileName, normalFullFileName );

            File.SetAttributes( normalFullFileName, FileAttributes.Normal );
        }

        /// <summary>
        /// Get <seealso cref="FileStream"/> for creation or overwrite file (if it already exists)
        /// </summary>
        /// <param name="fullFileName">
        /// Path to downloading file
        /// </param>
        public static FileStream FileStreamForDownload( String fullFileName )
        {
            if ( File.Exists( fullFileName ) )
            {
                UpdateAttributesOfBeforeDownloadingFile( fullFileName );
            }

            FileStream fileStreamForDownload = File.Create( fullFileName );
            SetAttributesToTempDownloadingFile( fullFileName );
            return fileStreamForDownload;
        }

        public static void SetAttributesToTempDownloadingFile( String tempFullFileName )
        {
            FileAttributes tempFileAttributes = FileAttributes.Hidden | FileAttributes.ReadOnly;
            File.SetAttributes( tempFullFileName, tempFileAttributes );
        }

        public static void UpdateAttributesOfBeforeDownloadingFile(String fullFileName)
        {
            File.SetAttributes( fullFileName, FileAttributes.Normal );
        }

        public static void TryRemoveAttributeIfItExists( String fullFileName, FileAttributes fileAttribute, out Boolean? isStillAttributeInFile )
        {
            FileAttributes currentFileAttributes = default;
            try
            {
                currentFileAttributes = File.GetAttributes( fullFileName );
                isStillAttributeInFile = currentFileAttributes.HasFlag( fileAttribute );
            }
            catch ( Exception ex )
            {
                Log.Error( ex, ex.Message );
                isStillAttributeInFile = null;
            }

            if ( isStillAttributeInFile == true )
            {
                try
                {
                    //remove attribute(-s)
                    currentFileAttributes &= ~fileAttribute;
                    File.SetAttributes( fullFileName, currentFileAttributes );

                    isStillAttributeInFile = false;
                }
                catch ( Exception ex )
                {
                    isStillAttributeInFile = true;

                    Log.Error( ex, ex.Message );
                }
            }
            else
            {
                isStillAttributeInFile = false;
            }
        }
    }
}
