using LUC.Interfaces.Constants;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Helpers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace LUC.Interfaces.Extensions
{
    public static class PathExtensions
    {
        //Officially these value should be 260, but string also contains zero character
        private const Int32 MAX_FILE_NAME_LENGTH_IN_LINUX = 259;

        public const String PATH_SEPARATOR = "\\";

        private static Int32 MaxFileNameLength => MAX_FILE_NAME_LENGTH_IN_LINUX;

        public static String Md5(String path)
        {
            using ( var md5 = MD5.Create() )
            {
                using ( FileStream stream = File.OpenRead( path ) )
                {
                    Byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// Returns ok, if all is ok
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        //TODO: to define whether file is locked just write in ADS some value
        public static ObjectStateType GetObjectState(String path)
        {
            if ( path == null )
            {
                return ObjectStateType.Unknown;
            }

            try
            {
                if ( Directory.Exists( path ) )
                {
                    return ObjectStateType.Ok;
                }

                if ( File.Exists( path ) )
                {
                    using ( FileStream fileStream = File.OpenWrite( path ) )
                    {
                        return ObjectStateType.Ok;
                    }
                }
                else
                {
                    return ObjectStateType.Deleted;
                }
            }
            catch ( IOException e )
            {
                return FileLockHelper.IsFileLocked(e) ?
                    ObjectStateType.Locked :
                    ObjectStateType.Ok;
            }
            catch ( UnauthorizedAccessException )
            {
                return ObjectStateType.Locked; // TODO Maybe return new state.
            }
        }

        public static List<String> EnumerateFilesWithFilter(String path)
        {
            var info = new DirectoryInfo(path);
            var filePathes = new List<String>();

            if (info.Exists)
            {
                filePathes = info.GetFiles("*").
                    Where(x => !IsIgnorableExtension(x.FullName) && !IsTemporaryFileName(x.FullName)).
                    OrderBy(p => p.LastWriteTimeUtc).
                    Select(x => x.FullName).
                    ToList();
            }

            return filePathes;
        }

        public static String TempFullFileNameForDownload(String realFullFileName, String syncFolder)
        {
            String pathFromRootFolderToRealFile = PathFromFolder(realFullFileName, syncFolder);

            String tempFullFileNameForDownload = Path.Combine(syncFolder, DownloadConstants.FOLDER_NAME_FOR_DOWNLOADING_FILES, $"{pathFromRootFolderToRealFile}{DownloadConstants.TEMP_FILE_NAME_EXTENSION}");

            String pathToTempFile = Path.GetDirectoryName(tempFullFileNameForDownload);
            Directory.CreateDirectory(pathToTempFile);

            return tempFullFileNameForDownload;
        }

        public static Boolean IsEqualFilePathesInCurrentOs(this String path1, String path2) =>
            path1.Equals(path2, StringComparison.OrdinalIgnoreCase); //now we support only Windows OS

        public static String TargetDownloadedFullFileName(String tempFullFileNameForDownload, String syncFolder)
        {
            String realDownloadedFullFileName;
            String extension = Path.GetExtension( tempFullFileNameForDownload );
            if ( extension.IsEqualFilePathesInCurrentOs( DownloadConstants.TEMP_FILE_NAME_EXTENSION ) )
            {
                String pathToFolderWithTempFiles = Path.Combine( syncFolder, DownloadConstants.FOLDER_NAME_FOR_DOWNLOADING_FILES );
                String pathFromFolderWithTempFiles = PathFromFolder( tempFullFileNameForDownload, pathToFolderWithTempFiles );

                realDownloadedFullFileName = Path.Combine( syncFolder, pathFromFolderWithTempFiles );
                realDownloadedFullFileName = realDownloadedFullFileName.TrimEnd( DownloadConstants.TEMP_FILE_NAME_EXTENSION.ToCharArray() );
            }
            else
            {
                realDownloadedFullFileName = (String)tempFullFileNameForDownload.Clone();
            }
            
            return realDownloadedFullFileName;
        }

        /// <remarks>
        /// It doesn't include name of folder <paramref name="folderFullName"/>
        /// </remarks>
        public static String PathFromFolder(String fullObjectName, String folderFullName)
        {
            String pathFromFolder = fullObjectName.Replace(oldValue: folderFullName, newValue: String.Empty);

            String pathSeparator = PATH_SEPARATOR;
            if (pathFromFolder.StartsWith(pathSeparator))
            {
                pathFromFolder = pathFromFolder.TrimStart(pathSeparator.ToCharArray());
            }

            return pathFromFolder;
        }

        public static List<String> EnumerateDirectoriesWithFilter(String folderPath)
        {
            var directoryPathes = new List<String>();

            try
            {
                if (Directory.Exists(folderPath))
                {
                    directoryPathes = Directory.EnumerateDirectories(folderPath).
                                                              Where(x => !x.EndsWith(".") &&
                                                                         !x.EndsWith(" ")).
                                                              ToList();
                }
            }
            catch ( Exception )
            {
                return directoryPathes;
            }

            return directoryPathes;
        }

        public static Boolean IsIgnorableExtension(String path)
        {
            String extension = Path.GetExtension(path);

            return extension == ".lnk" ||
                extension == "." ||
                extension == " " ||
                ( extension.Length > 0 && extension.All( ch => ch == '.' ) ) ||
                ( extension.Length > 0 && extension.All( ch => ch == ' ' ) );
        }

        public static Boolean IsTemporaryFileName(String fullPath)
        {
            Boolean result = false;

            String fileName = Path.GetFileName(fullPath).ToLowerInvariant();

            //TODO: delete this condition
            if (fileName.Length > MaxFileNameLength)
            {
                result = true;
            }
            else
            {
                String fileExtension = Path.GetExtension(fileName);

                StringComparison stringComparison = StringComparison.Ordinal;

                if (fileName.StartsWith(value: "~") ||
                     fileName.StartsWith(".~") ||
                     fileName.StartsWith("<") ||
                     fileName.StartsWith(">") ||
                     fileName.StartsWith(":") ||
                     fileName.StartsWith("''") ||
                     fileName.StartsWith("|") ||
                     fileName.StartsWith("?") ||
                     fileName.StartsWith("*") ||
                     fileName.StartsWith("%") ||
                     fileName.EndsWith(value: "%") ||
                     fileName.EndsWith(" ") ||
                     fileName.EndsWith(".") ||
                     fileName.EndsWith(".dropbox.attr"))
                {
                    result = true;
                }
                //TODO: replace check extension in method PathExtensions.IsIgnorableExtension
                else if (fileExtension.Equals(value: ".tmp", stringComparison) ||
                    fileExtension.Equals(".dwl", stringComparison) || // Temp AutoCad file extension
                    fileExtension.Equals(".dwl2", stringComparison) || // Temp AutoCad file extension
                    fileExtension.Equals(".ds_store", stringComparison) ||
                    fileExtension.Equals(".lnk", stringComparison) ||
                    fileExtension.Equals(".dropbox", stringComparison) ||
                    fileExtension.Equals(DownloadConstants.TEMP_FILE_NAME_EXTENSION, stringComparison))
                {
                    result = true;
                }
                else if (fileName.Equals("desktop.ini", stringComparison) ||
                    fileName.Equals("thumbs.db", stringComparison) ||
                    fileName.Equals("icon\r", stringComparison) ||
                    String.IsNullOrEmpty(fileName))
                {
                    result = true;
                }
                else if (fileExtension.Equals(".bak", stringComparison) &&
                    File.Exists(path: $"{fullPath.Substring(startIndex: 0, length: fullPath.Length - 3)}dwg"))
                {
                    result = true; // Ignore .bak file which was created specifically by AutoCad.
                }
            }

            return result;
        }
    }
}
