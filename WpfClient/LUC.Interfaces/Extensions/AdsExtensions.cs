using CodeFluent.Runtime.BinaryServices;

using LUC.Interfaces.Enums;
using LUC.Interfaces.Helpers;
using LUC.Interfaces.Models;

using Serilog;

using System;
using System.IO;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using LUC.Interfaces.Constants;

namespace LUC.Interfaces.Extensions
{
    //TODO: don't return value in any public method of this class
    public static partial class AdsExtensions
    {
        private const String LUC_ID_IN_ADS = "cloud.lightupon.";

        private const String GUID_ADS_NAME = LUC_ID_IN_ADS + "guid";
        private const String LAST_SEEN_VERSION = LUC_ID_IN_ADS + "lastseenversion";
        private const String LOCAL_PATH_ADS_NAME = LUC_ID_IN_ADS + "path";
        private const String LOCK_ADS_NAME = LUC_ID_IN_ADS + "lock";
        private const String LAST_MD5 = LUC_ID_IN_ADS + "md5";
        private const String DOWNLOADED_NOT_MOVED_FILE = LUC_ID_IN_ADS + "downloadednotmovedfile";

        private const String PREFIX_FOR_PATH_LENGTH_IGNORE = "\\\\?\\";

        private const Int32 NUMBER_OF_RETRIES = 50;
        private const Int32 DELAY_ON_RETRY = 100;

        private static readonly Dictionary<Stream, String> s_streamIds = new Dictionary<Stream, String>
        {
            { Stream.Guid, GUID_ADS_NAME},
            { Stream.LastSeenVersion, LAST_SEEN_VERSION },
            { Stream.LocalPathMarker, LOCAL_PATH_ADS_NAME },
            { Stream.Lock, LOCK_ADS_NAME },
            { Stream.Md5, LAST_MD5 },
            { Stream.IsDownloadedButNotMovedFile, DOWNLOADED_NOT_MOVED_FILE }
        };

        private static String ToAdsPath(this String path, Stream stream) =>
            $"{PREFIX_FOR_PATH_LENGTH_IGNORE}{path}:{s_streamIds[stream]}";

        public static void WriteInfoAboutNewFileVersion(FileInfo fileInfo, String newFileVersion, String guid)
        {
            Int64 fileSize = -1;
            try
            {
                fileSize = fileInfo.Length;
            }
            catch
            {
                ;//do nothing
            }

            if ((fileSize != -1) && (fileSize < GeneralConstants.TOO_BIG_SIZE_TO_COMPUTE_MD5))
            {
                String currentMD5 = ArrayExtensions.CalculateMd5Hash(fileInfo.FullName);
                Write(fileInfo.FullName, currentMD5, Stream.Md5);
            }

            Write(fileInfo.FullName, newFileVersion, Stream.LastSeenVersion);

            WriteGuidAndLocalPathMarkersIfNotTheSame(fileInfo.FullName, guid);
        }

        public static void WriteLockDescription(String path, ILockDescription lockDescription) =>
            Write(path, lockDescription.ToString(), Stream.Lock);

        public static void WriteThatDownloadProcessIsStarted(String path) =>
            InternalWriteInAds(path, text: Boolean.FalseString, Stream.IsDownloadedButNotMovedFile); //write that file isn't downloaded

        /// <summary>
        /// Write that file is downloaded, but doesn't moved to <seealso cref="ICurrentUserProvider.RootFolderPath"/>
        /// </summary>
        /// <param name="downloadingFileInfo"></param>
        public static void WriteThatFileIsDownloaded(DownloadingFileInfo downloadingFileInfo)
        {
            Write(downloadingFileInfo.PathWhereDownloadFileFirst, text: Boolean.TrueString, Stream.IsDownloadedButNotMovedFile);
            Write(downloadingFileInfo.PathWhereDownloadFileFirst, downloadingFileInfo.Version, Stream.LastSeenVersion);
            Write(downloadingFileInfo.PathWhereDownloadFileFirst, downloadingFileInfo.Guid, Stream.Guid);
        }

        public static void Write(String path, String text, Stream adsStream)
        {
            Boolean fileExists = File.Exists(path);
            Boolean isTextNullOrWhiteSpace = String.IsNullOrWhiteSpace(text);

            if (!isTextNullOrWhiteSpace && fileExists && (adsStream != Stream.None))
            {
                InternalWriteInAds(path, text, adsStream);
            }
            else if (!fileExists)
            {
                throw new FileNotFoundException(message: $"File doens\'t exist, but {nameof(Write)} was called", path);
            }
            else if (isTextNullOrWhiteSpace)
            {
                throw new ArgumentException("Is null or white space", nameof(text));
            }
            else
            {
                throw new ArgumentException($"Has {Stream.None} value", nameof(adsStream));
            }
        }

        public static void WriteGuidAndLocalPathMarkersIfNotTheSame(String path, String guid)
        {
            Boolean fileExists = File.Exists(path);
            Boolean isTextNullOrWhiteSpace = String.IsNullOrWhiteSpace(guid);

            if (!isTextNullOrWhiteSpace && fileExists)
            {
                String current = Read(path, Stream.Guid);

                FileExtensions.TryRemoveAttributeIfItExists(path, FileAttributes.ReadOnly, isStillAttributeInFile: out _);
                StringComparison stringComparison = StringComparison.Ordinal;

                if (current.Equals(guid, stringComparison))
                {
                    // Update only local path marker if it is needed.
                    String currentLocalPathMarker = Read(path, Stream.LocalPathMarker);

                    if (!currentLocalPathMarker.Equals(path, stringComparison))
                    {
                        Write(path, path.ToHexString(), Stream.LocalPathMarker);

                        //File.SetLastWriteTimeUtc(path, currentLastWriteUtc);//try to remove it
                    }

                    FileExtensions.TryRemoveAttributeIfItExists(path, FileAttributes.ReadOnly, isStillAttributeInFile: out _);
                }
                else
                {
                    Write(path, guid, Stream.Guid);

                    // If guid is created - remember original path of the file.
                    Write(path, path.ToHexString(), Stream.LocalPathMarker);

                    //TODO: test, whether we need this row
                    //File.SetLastWriteTimeUtc(path, currentLastWriteUtc);
                }
            }
            else if (!fileExists)
            {
                throw new FileNotFoundException(message: $"File doens\'t exist, but {nameof(Write)} was called", path);
            }
            else
            {
                throw new ArgumentException("Is null or white space", nameof(guid));
            }
        }

        //TODO: don't return null
        public static FileInfo TryFindFileByGuid(String directoryPath, String guid, out Boolean existsFileWithSameGuid)
        {
            if (!String.IsNullOrEmpty(guid))
            {
                FileInfo fileInfo = null;
                existsFileWithSameGuid = false;

                try
                {
                    System.Collections.Generic.List<String> files = PathExtensions.EnumerateFilesWithFilter(directoryPath);

                    foreach (String path in files.Where(path =>
                    {
                        String readGuid = Read(path, Stream.Guid);

                        Boolean isSameGuid = readGuid.Equals(guid, StringComparison.Ordinal);
                        return isSameGuid;
                    }))
                    {
                        existsFileWithSameGuid = true;
                        fileInfo = FileInfoHelper.TryGetFileInfo(path);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }

                return fileInfo;
            }
            else
            {
                throw new ArgumentNullException(nameof(guid));
            }
        }

        public static ILockDescription ReadLockDescription(String path)
        {
            var adsStream = Stream.Lock;
            ILockDescription lockDescription;
            try
            {
                String lockDescrAsStr = Read(path, adsStream);
                lockDescription = new LockDescription(lockDescrAsStr);
            }
            catch (Exception ex)
            {
                lockDescription = new LockDescription(AdsLockState.Default);

                Log.Error(ex, ex.Message);
            }

            return lockDescription;
        }

        public static String Read(String path, Stream adsStream)
        {
            Boolean isPathNullOrWhiteSpace = String.IsNullOrWhiteSpace(path);

            if (!isPathNullOrWhiteSpace && (adsStream != Stream.None))
            {
                String adsPath = path.ToAdsPath(adsStream);
                String valueInAds = NtfsAlternateStream.Exists(adsPath) ?
                    NtfsAlternateStream.ReadAllText(adsPath) :
                    String.Empty;

                String result;

                if (adsStream == Stream.LocalPathMarker)
                {
                    String localPath = valueInAds.FromHexString();
                    result = localPath;
                }
                else
                {
                    result = valueInAds;
                }

                return result;
            }
            else if (isPathNullOrWhiteSpace)
            {
                throw new ArgumentException(message: "Is null or white space", nameof(path));
            }
            else
            {
                throw new ArgumentException($"Has {Stream.None} value", nameof(adsStream));
            }
        }

        /// <summary>
        /// Reads whether file was downloaded but doesn't moved to sync folder. If it is so, also read file version of downloaded file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="isFileDownloaded">
        /// Defines whether file was successfully downloaded
        /// </param>
        /// <param name="fileVersion">
        /// Version of downloaded file
        /// </param>
        public static void ReadIsFileDownloaded(String path, out Boolean isFileDownloaded)
        {
            String isFileDownloadedAsStr = Read(path, Stream.IsDownloadedButNotMovedFile);
            if (!String.IsNullOrWhiteSpace(isFileDownloadedAsStr))
            {
                isFileDownloaded = Convert.ToBoolean(isFileDownloadedAsStr);
            }
            else
            {
                isFileDownloaded = false;
            }
        }

        public static void ReadDownloadingFileInfo(String tempFullFileName, String syncFolder, out DownloadingFileInfo downloadingFileInfo, out Boolean isSuccessfullyRead)
        {
            downloadingFileInfo = null;
            isSuccessfullyRead = false;

            String fileVersion = Read(tempFullFileName, Stream.LastSeenVersion);
            if (!String.IsNullOrWhiteSpace(fileVersion))
            {
                String guid = Read(tempFullFileName, Stream.Guid);
                if (!String.IsNullOrWhiteSpace(guid))
                {
                    downloadingFileInfo = new DownloadingFileInfo
                    {
                        Version = fileVersion,
                        Guid = guid,
                        PathWhereDownloadFileFirst = tempFullFileName,
                        LocalFilePath = PathExtensions.TargetDownloadedFullFileName(tempFullFileName, syncFolder)
                    };

                    isSuccessfullyRead = true;
                }
            }
        }

        public static void Remove(String path, Stream adsStream)
        {
            Boolean fileExists = File.Exists(path);

            if ((adsStream != Stream.None) && fileExists)
            {
                String adsPath = path.ToAdsPath(adsStream);

                if (NtfsAlternateStream.Exists(adsPath))
                {
                    NtfsAlternateStream.Delete(adsPath);
                }
            }
            else if (!fileExists)
            {
                throw new FileNotFoundException($"File doens\'t exist, but {nameof(Remove)} was called", path);
            }
            else
            {
                throw new ArgumentException($"Has {Stream.None} value", nameof(adsStream));
            }
        }

        private static void InternalWriteInAds(String path, String text, Stream adsStream)
        {
            if (adsStream == Stream.IsDownloadedButNotMovedFile)
            {
                Boolean isValidText = Boolean.TryParse(text, result: out _);
                if (!isValidText)
                {
                    throw new ArgumentException(message: $"Should be {Boolean.TrueString}, or 1, or {Boolean.FalseString}, or 0. You gave {text}", nameof(text));
                }
            }

            String currentValueInAds = Read(path, adsStream);

            String textToWrite = (adsStream == Stream.LocalPathMarker) && path.Equals(text, StringComparison.Ordinal) ? text.ToHexString() : text;

            if (!currentValueInAds.Equals(textToWrite))
            {
                String adsPath = path.ToAdsPath(adsStream);
                Boolean isWritenInAds = false;

                for (Int32 numRetry = 0; (numRetry < NUMBER_OF_RETRIES) && !isWritenInAds; numRetry++)
                {
                    FileExtensions.TryRemoveAttributeIfItExists(path, FileAttributes.ReadOnly, isStillAttributeInFile: out _);

                    try
                    {
                        FileStream stream = NtfsAlternateStream.Open(adsPath, FileAccess.Write,
                            FileMode.OpenOrCreate, FileShare.None);

                        stream.Close();

                        NtfsAlternateStream.WriteAllText(adsPath, textToWrite);

                        Log.Information($"{adsPath} has {nameof(text)}: {textToWrite}");

                        isWritenInAds = true;
                    }
                    catch (FileLoadException ex)
                    {
                        // You may check error code to filter some exceptions, not every error
                        // can be recovered.
                        Thread.Sleep(DELAY_ON_RETRY);

                        Log.Error(ex, ex.Message);
                    }
                    catch (IOException ex)
                    {
                        // You may check error code to filter some exceptions, not every error
                        // can be recovered.
                        Thread.Sleep(DELAY_ON_RETRY);

                        Log.Error(ex, ex.Message);
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        Log.Error(ex, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, ex.Message);
                    }
                }

                FileExtensions.TryRemoveAttributeIfItExists(path, FileAttributes.ReadOnly, isStillAttributeInFile: out _);
            }
        }
    }
}
