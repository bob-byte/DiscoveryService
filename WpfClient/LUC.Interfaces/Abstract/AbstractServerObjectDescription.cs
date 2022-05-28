using LUC.DVVSet;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using LUC.Interfaces.Models;

using Serilog;

using System;
using System.IO;

namespace LUC.Interfaces.Abstract
{
    public abstract class AbstractServerObjectDescription
    {
        private static ICurrentUserProvider s_currentUserProvider;
        private static IFileChangesQueue s_fileChangesQueue;

        protected AbstractServerObjectDescription()
        {
            InitService( ref s_currentUserProvider );
            InitService( ref s_fileChangesQueue );
        }

        public String Guid { get; set; }

        public String ObjectKey { get; set; }

        public DateTime LastModifiedDateTimeUtc { get; set; }

        public String Version { get; set; }

        public Int64 ByteCount { get; set; }

        private static void InitService<T>( ref T service )
        {
            SingletonInitializer.ThreadSafeInit(
                value: () =>
                {
                    T exportedValue;
                    try
                    {
                        exportedValue = AppSettings.ExportedValue<T>();
                    }
                    catch ( InvalidOperationException ex )
                    {
                        Log.Error( ex.Message, ex );
                        exportedValue = default;
                    }

                    return exportedValue;
                },
                ref service
            );
        }

        public virtual Boolean ShouldLocalFileBeUploaded( FileInfo localFileInfo )
        {
            CompareFileOnServerAndLocal( localFileInfo, out ComparationLocalAndServerFileResult comaprationResult );

            Boolean shouldLocalFileBeUploaded = comaprationResult.ShouldFileBeUploaded();
            return shouldLocalFileBeUploaded;
        }

        /// <summary>
        /// Compares file on the server and the local PC. If the file from the server with the same size and version as on local PC was already downloaded in folder with temp files or in some bucket, then returns <a href="false"/>, otherwise - <a href="true"/>
        /// </summary>
        /// <param name="currentLocalFileInfo">
        /// File info of file in some bucket
        /// </param>
        /// <returns>
        /// Should file be downloaded?
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="currentLocalFileInfo"/> is null
        /// </exception>
        public virtual Boolean ShouldBeDownloaded( FileInfo currentLocalFileInfo )
        {
            if ( currentLocalFileInfo != null )
            {
                Boolean isFileAlreadyDownloaded = default;
                String tempFullFileName = null;

                if ( ( s_currentUserProvider != null ) && ( !String.IsNullOrWhiteSpace( s_currentUserProvider.RootFolderPath ) ) )
                {
                    tempFullFileName = PathExtensions.TempFullFileNameForDownload( currentLocalFileInfo.FullName, s_currentUserProvider.RootFolderPath );
                    AdsExtensions.ReadIsFileDownloaded( tempFullFileName, out isFileAlreadyDownloaded );

                    if ( isFileAlreadyDownloaded )
                    {
                        currentLocalFileInfo = new FileInfo( tempFullFileName );
                    }
                }

                CompareFileOnServerAndLocal( currentLocalFileInfo, out ComparationLocalAndServerFileResult comparationResult );

                Boolean shouldFileBeDownloaded = comparationResult.IsFileNewerOnServer();

                if ( shouldFileBeDownloaded && isFileAlreadyDownloaded )
                {
                    //case when file was downloaded, but didn't added to FileChangesQueue
                    AdsExtensions.ReadDownloadingFileInfo( tempFullFileName, s_currentUserProvider.RootFolderPath, out DownloadingFileInfo downloadingFileInfo, out Boolean isSuccessfullyRead );
                    if ( isSuccessfullyRead )
                    {
                        s_fileChangesQueue.AddDownloadedNotMovedFile( downloadingFileInfo );
                        shouldFileBeDownloaded = false;
                    }
                }

                return shouldFileBeDownloaded;
            }
            else
            {
                throw new ArgumentNullException( nameof( currentLocalFileInfo ) );
            }
        }

        public virtual void CompareFileOnServerAndLocal( FileInfo localFileInfo, out ComparationLocalAndServerFileResult comparationResult )
        {
            Boolean isTooBigSizeToComputeMd5 = false;
            comparationResult = ComparationLocalAndServerFileResult.None;

            try
            {
                isTooBigSizeToComputeMd5 = localFileInfo.Length >= GeneralConstants.TOO_BIG_SIZE_TO_COMPUTE_MD5;
            }
            catch ( FileNotFoundException )
            {
                comparationResult = ComparationLocalAndServerFileResult.DoesntExistLocally;
                if ( String.IsNullOrWhiteSpace( Version ) )
                {
                    //also set that file doesn't exist on the server
                    comparationResult |= ComparationLocalAndServerFileResult.DoesntExistOnServer;
                }
            }

            if ( comparationResult == ComparationLocalAndServerFileResult.None )
            {
                CompareFileOnServerAndLocal( localFileInfo, !isTooBigSizeToComputeMd5, out comparationResult );
            }
        }

        public virtual void CompareFileOnServerAndLocal(
            FileInfo localFileInfo,
            Boolean whetherCompareMd5,
            out ComparationLocalAndServerFileResult comparationResult )
        {
            if ( localFileInfo == null )
            {
                throw new ArgumentNullException( nameof( localFileInfo ), message: $"Got null in some parameter of the method {nameof( CompareFileOnServerAndLocal )}" );
            }
            else
            {
                if ( String.IsNullOrWhiteSpace( Version ) )
                {
                    comparationResult = ComparationLocalAndServerFileResult.DoesntExistOnServer;
                    if ( !localFileInfo.Exists )
                    {
                        //also set that file doesn't exist on local PC
                        comparationResult |= ComparationLocalAndServerFileResult.DoesntExistLocally;
                    }
                }
                else
                {
                    if ( !localFileInfo.Exists )
                    {
                        comparationResult = ComparationLocalAndServerFileResult.DoesntExistLocally;
                    }
                    else
                    {
                        StringComparison stringComparison = StringComparison.Ordinal;
                        String localVersion = AdsExtensions.Read( localFileInfo.FullName, AdsExtensions.Stream.LastSeenVersion );

                        //case when file was uploaded, but version wasn't set
                        if ( String.IsNullOrWhiteSpace( localVersion ) )
                        {
                            localVersion = Version;
                        }

                        Boolean isSameFileVersion = localVersion.Equals( Version, stringComparison );
                        Boolean isSameFileSize = localFileInfo.Length == ByteCount;

                        if ( isSameFileVersion )
                        {
                            try
                            {
                                if ( isSameFileSize )
                                {
                                    Boolean isSameMd5 = false;

                                    if ( whetherCompareMd5 ) //NOTE большие файлы редактируются редко, и практически никогда после редактирования не остаются точно такого же размера
                                    {
                                        isSameMd5 = IsChangedMdFiveOfFile( localFileInfo.FullName );
                                    }
                                    else
                                    {
                                        isSameMd5 = true;
                                    }

                                    comparationResult = isSameMd5 ? ComparationLocalAndServerFileResult.Equal : ComparationLocalAndServerFileResult.OlderOnServer;
                                }
                                else
                                {
                                    comparationResult = IsVersionLocallyOlderThenOnServer( localVersion );
                                }
                            }
                            catch ( FileNotFoundException )
                            {
                                comparationResult = ComparationLocalAndServerFileResult.DoesntExistLocally;
                            }
                        }
                        else if ( !String.IsNullOrWhiteSpace( localVersion ) )
                        {
                            comparationResult = IsVersionLocallyOlderThenOnServer( localVersion );
                        }
                        else if ( String.IsNullOrWhiteSpace( localVersion ) || String.IsNullOrWhiteSpace( AdsExtensions.Read( localFileInfo.FullName, AdsExtensions.Stream.Guid ) ) )
                        {
                            Log.Fatal( messageTemplate: $"Version or guid of file {localFileInfo.FullName} wasn't writen in ADS" );

                            if ( isSameFileSize )
                            {
                                Boolean isSameMd5 = false;

                                if ( whetherCompareMd5 ) //NOTE large files are rarely edited, and almost never remain exactly the same size after editing
                                {
                                    isSameMd5 = IsChangedMdFiveOfFile( localFileInfo.FullName );
                                }
                                else
                                {
                                    isSameMd5 = true;
                                }

                                if ( isSameMd5 )
                                {
                                    AdsExtensions.WriteInfoAboutNewFileVersion( localFileInfo, Version, Guid );
                                    comparationResult = ComparationLocalAndServerFileResult.Equal;
                                }
                                else
                                {
                                    comparationResult = ComparationLocalAndServerFileResult.NewerOnServer;
                                }
                            }
                            else
                            {
                                comparationResult = ComparationLocalAndServerFileResult.NewerOnServer;
                            }
                        }
                        else
                        {
                            //it can't be
                            throw new InvalidProgramException();
                        }
                    }
                }

                if ( comparationResult != ComparationLocalAndServerFileResult.Equal )
                {
                    String logRecord = $"{nameof( comparationResult )} of file {localFileInfo.FullName} on the server and local PC = {comparationResult}";

                    Log.Information( logRecord );
                    Console.WriteLine( logRecord );
                }
            }
        }

        protected ComparationLocalAndServerFileResult IsVersionLocallyOlderThenOnServer( String localFileVersion)
        {
            if ( !String.IsNullOrWhiteSpace( Version ) )
            {
                var serverVectorClock = Clock.StringToClock( Version );
                var localVectorClock = Clock.StringToClock( localFileVersion );

                Boolean isVersionLocallyOlder = Dvvdotnet.Less( localVectorClock, serverVectorClock );

                ComparationLocalAndServerFileResult comparationResult = isVersionLocallyOlder ? ComparationLocalAndServerFileResult.NewerOnServer : ComparationLocalAndServerFileResult.OlderOnServer;
                return comparationResult;
            }
            else
            {
                throw new InvalidOperationException( message: $"{nameof( Version )} is null or white space" );
            }
        }

        private Boolean IsChangedMdFiveOfFile(String fullFileName)
        {
            String lastMD5 = AdsExtensions.Read( fullFileName, AdsExtensions.Stream.Md5 );
            Boolean isSameMd5;

            if ( !String.IsNullOrWhiteSpace( lastMD5 ) )
            {
                try
                {
                    String currentMD5 = ArrayExtensions.CalculateMd5Hash( fullFileName );
                    isSameMd5 = currentMD5.Equals( lastMD5, StringComparison.Ordinal );
                }
                //do not pay attention that file is in use
                catch ( IOException)
                {
                    isSameMd5 = true;
                }
            }
            else
            {
                //case when MD5 wasn't writen because of user closed app before it
                isSameMd5 = true;
            }

            return isSameMd5;
        }
    }
}
