using LUC.Interfaces.Extensions;

using System;
using System.IO;

namespace LUC.Interfaces.Helpers
{
    //TODO Release 2.0: None method should return null
    public static class FileInfoHelper
    {
        public const String ERROR_DESCRIPTION =
            "File {0} is deleted, not exists, have too long path or you do not have access to it.";

        public static FileInfo TryGetFileInfo( String fullPath )
        {
            FileInfo fileInfo = null;

            if (File.Exists( fullPath ))
            {
                try
                {
                    FileExtensions.TryRemoveAttributeIfItExists( fullPath, FileAttributes.ReadOnly, out Boolean? isAttributeInFile );
                    if ( isAttributeInFile == false )
                    {
                        fileInfo = new FileInfo( fullPath );
                    }
                }
                catch ( Exception )
                {
                    ;//ignore
                }
            }

            return fileInfo;
        }

        

        public static DirectoryInfo TryGetDirectoryInfo( String fullPath )
        {
            if ( !Directory.Exists( fullPath ) )
            {
                return null;
            }

            try
            {
                return new DirectoryInfo( fullPath );
            }
            catch ( Exception )
            {
                return null;
            }
        }
    }
}
