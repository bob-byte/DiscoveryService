using System;
using System.Collections.Generic;
using System.IO;

namespace LUC.DiscoveryServices.Test.Extensions
{
    static class DirectoryExtension
    {
        public static void CopyDirsAndSubdirs( String sourceFolder, String outputFolder )
        {
            if ( ( !String.IsNullOrWhiteSpace( sourceFolder ) ) && ( !String.IsNullOrWhiteSpace( outputFolder ) ) )
            {
                IEnumerable<String> allDirectories = AllDirsAndSubdirs( sourceFolder );
                foreach ( String directory in allDirectories )
                {
                    String afterSourceFolder = directory.Substring( sourceFolder.Length + 1, directory.Length - sourceFolder.Length - 1 );

                    String newDirectoryFullName = Path.Combine( outputFolder, afterSourceFolder );
                    try
                    {
                        Directory.CreateDirectory( newDirectoryFullName );
                    }
                    catch ( IOException )
                    {
                        continue;
                    }
                }
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public static IEnumerable<String> AllDirsAndSubdirs( String rootFolderPath, String fileSearchPattern ) =>
            AllDirsAndSubdirs( rootFolderPath, ( pathToDir ) => Directory.GetDirectories( pathToDir, fileSearchPattern ) );

        public static IEnumerable<String> AllDirsAndSubdirs( String rootFolderPath ) =>
            AllDirsAndSubdirs( rootFolderPath, ( pathToDir ) => Directory.GetDirectories( pathToDir ) );

        private static IEnumerable<String> AllDirsAndSubdirs( String rootFolderPath, Func<String, IEnumerable<String>> funcToFind )
        {
            var dirsToSearchDirectories = new Queue<String>();
            dirsToSearchDirectories.Enqueue( rootFolderPath );

            while ( dirsToSearchDirectories.Count > 0 )
            {
                String folderPath = dirsToSearchDirectories.Dequeue();
                IEnumerable<String> directoriesInOneDir;
                try
                {
                    directoriesInOneDir = funcToFind( folderPath );
                }
                catch ( UnauthorizedAccessException )
                {
                    continue;
                }
                catch ( DirectoryNotFoundException )
                {
                    continue;
                }

                foreach ( String directoryFullName in directoriesInOneDir )
                {
                    dirsToSearchDirectories.Enqueue( directoryFullName );
                    yield return directoryFullName;
                }
            }
        }
    }
}
