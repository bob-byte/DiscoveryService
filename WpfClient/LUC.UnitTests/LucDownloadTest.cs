using LUC.DiscoveryServices.Common.Extensions;
using LUC.DiscoveryServices.Messages;
using LUC.Interfaces;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

using NUnit.Framework;
using FluentAssertions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LUC.Services.Implementation;
using LUC.Interfaces.Constants;
using System.Linq;
using LUC.Interfaces.Extensions;
using LUC.DiscoveryServices.Common;

namespace LUC.UnitTests
{
    public class LucDownloadTest
    {
        public const Int32 BYTES_COUNT_OF_RND_FILE = 10 * GeneralConstants.BYTES_IN_ONE_MEGABYTE;

        private const String PATH_FROM_ROOT_FOLDER_TO_TESTING_FILES = "DownloadedFiles";

        private const String DEFAULT_FILE_NAME_FOR_TESTING = "123qweij1woen1oine.txt";


        private ApiClient.ApiClient m_apiClient;
        private LoginResponse m_loginResponse;
        private FileUploadResponse m_uploadResponse;
        private ICurrentUserProvider m_currentUser;

        private FileInfo m_fileInfoInServer;
        private String m_hexPrefixOfUploadedFile;

        private IBucketName m_rndBucketName;

        private String m_pathForDownloadingFile;
        private String m_directoryFullNameForDownloadingFiles;
        private String m_hexPrefixOfDownloadingFile;
        private String m_downloadingFileName;

        private Random m_random;

        private ObjectDescriptionModel m_objectDescription;
        private ObjectsListResponse m_listResponse;

        private ISyncingObjectsList m_syncingObjects;

        [OneTimeSetUp]
        public async Task InitAsync()
        {
            m_downloadingFileName = DEFAULT_FILE_NAME_FOR_TESTING;

            m_random = new Random();

            (m_apiClient, m_loginResponse, m_currentUser) = await SetUpTests.LoginAsync().ConfigureAwait( continueOnCapturedContext: false );

            m_loginResponse.Should().NotBeNull();
            Assert.IsTrue( m_loginResponse.IsSuccess, m_loginResponse.Message );

            m_rndBucketName = RndBucketName();

            await InitFileForDownloadAsync();

            m_syncingObjects = SetUpTests.SyncingObjectsList;

            //directory will be not created if it already exists
            Directory.CreateDirectory( m_directoryFullNameForDownloadingFiles );

            m_pathForDownloadingFile = Path.Combine( m_directoryFullNameForDownloadingFiles, m_fileInfoInServer.Name );
            m_downloadingFileName = m_fileInfoInServer.Name;

            m_hexPrefixOfDownloadingFile = m_currentUser.ExtractPrefix( m_pathForDownloadingFile );
            m_hexPrefixOfUploadedFile = m_currentUser.ExtractPrefix( m_fileInfoInServer.FullName );
        }

#if !DEBUG
        [OneTimeTearDown]
        public void DeleteUploadedFile()
        {
            if ( m_uploadResponse?.IsSuccess == true )
            {
                m_fileInfoInServer.Delete();
                m_apiClient.DeleteAsync( m_fileInfoInServer.FullName ).GetAwaiter().GetResult();
            }
        }   
#endif

        [OneTimeTearDown]
        public void TryDeleteDirectoryWithDownloadingFiles()
        {
            var directoryInfo = new DirectoryInfo( m_directoryFullNameForDownloadingFiles );

            if ( directoryInfo.Exists )
            {
                foreach ( String filePath in directoryInfo.EnumerateFiles().Select( c => c.FullName ) )
                {
                    File.SetAttributes( filePath, FileAttributes.Normal );
                }

                directoryInfo.Delete( recursive: true );
            }
        }

        private async Task InitFileForDownloadAsync()
        {
            m_directoryFullNameForDownloadingFiles = Path.Combine( m_currentUser.RootFolderPath, m_rndBucketName.LocalName, PATH_FROM_ROOT_FOLDER_TO_TESTING_FILES );
            TryDeleteDirectoryWithDownloadingFiles();

            m_pathForDownloadingFile = Path.Combine( m_directoryFullNameForDownloadingFiles, m_downloadingFileName );

            String fullFileNameOfUploadingFile = Path.Combine( m_currentUser.RootFolderPath, m_rndBucketName.LocalName, m_downloadingFileName );

            Boolean createNewRndFile = !File.Exists( fullFileNameOfUploadingFile );
            if ( createNewRndFile )
            {
                m_fileInfoInServer = CreateFile( BYTES_COUNT_OF_RND_FILE, m_rndBucketName.LocalName );

                m_uploadResponse = await m_apiClient.TryUploadAsync( m_fileInfoInServer ).ConfigureAwait( false );
                Assert.IsTrue( m_uploadResponse.IsSuccess, m_uploadResponse.Message );
            }
            else
            {
                String localPathToFileWhichIsInServer = Path.Combine( m_currentUser.RootFolderPath, m_rndBucketName.LocalName, DEFAULT_FILE_NAME_FOR_TESTING );
                m_fileInfoInServer = new FileInfo( localPathToFileWhichIsInServer );
                if ( m_fileInfoInServer.Length != BYTES_COUNT_OF_RND_FILE )
                {
                    m_fileInfoInServer = CreateFile( BYTES_COUNT_OF_RND_FILE, m_rndBucketName.LocalName );
                }

                m_listResponse = await m_apiClient.ListAsync( m_rndBucketName.ServerName ).ConfigureAwait( false );
                m_objectDescription = m_listResponse.ToObjectsListModel().ObjectDescriptions.Find( o => o.OriginalName.Equals( m_fileInfoInServer.Name, StringComparison.Ordinal ) && ( o.ByteCount == BYTES_COUNT_OF_RND_FILE ) );
                if ( m_objectDescription == null )
                {
                    m_uploadResponse = await m_apiClient.TryUploadAsync( m_fileInfoInServer ).ConfigureAwait( false );
                    Assert.IsTrue( m_uploadResponse.IsSuccess, m_uploadResponse.Message );

                    m_listResponse = await m_apiClient.ListAsync( m_rndBucketName.ServerName ).ConfigureAwait( false );
                    m_objectDescription = m_listResponse.ToObjectsListModel().ObjectDescriptions.Find( o => o.OriginalName.Equals( m_fileInfoInServer.Name, StringComparison.Ordinal ) && !o.IsDeleted );
                }
            }

            if ( m_listResponse == null )
            {
                m_listResponse = await m_apiClient.ListAsync( m_rndBucketName.ServerName ).ConfigureAwait( false );
                m_objectDescription = m_listResponse.ToObjectsListModel().ObjectDescriptions.Find( o => o.OriginalName.Equals( m_fileInfoInServer.Name, StringComparison.Ordinal ) && !o.IsDeleted );
            }

            Assert.IsTrue( m_objectDescription != null, message: $"Created rnd file {m_fileInfoInServer.Name} doesn\'t exist in server any more" );
        }

        private IBucketName RndBucketName()
        {
            String rndBucketFullName = m_currentUser.ProvideBucketDirectoryPaths()[ 0 ];
            IBucketName rndBucketName = m_currentUser.TryExtractBucket( rndBucketFullName );

            return rndBucketName;
        }

        [Test]
        public void DownloadNecessaryChunksFromServer_DownloadAllChunksOfFileFromServer_DownloadedFileHasSameLengthAsCreatedInFileSystem()
        {
            UInt64 totalBytesCountOfFile = (UInt64)m_fileInfoInServer.Length;
            List<ChunkRange> allChunkRanges = ChunkRanges( start: 0, finallyEnd: totalBytesCountOfFile - 1, DsConstants.MAX_CHUNK_SIZE, totalBytesCountOfFile );
            var downloadingFileInfo = m_objectDescription.ToDownloadingFileInfo( m_rndBucketName.ServerName, m_pathForDownloadingFile, m_hexPrefixOfUploadedFile );

            m_apiClient.Downloader.DownloadNecessaryChunksFromServer( alreadyDownloadedBytesCount: 0, allChunkRanges, downloadingFileInfo, m_pathForDownloadingFile );

            //method DownloadNecessaryChunksFromServer sets readonly attribute to m_pathForDownloadingFile
            File.SetAttributes( m_pathForDownloadingFile, FileAttributes.Normal );

            var downloadedFile = new FileInfo( m_pathForDownloadingFile );
            downloadedFile.Length.Should().Be( m_fileInfoInServer.Length );
        }

        [Test]
        public void JoinedChunkRanges_GetAllRangesAndRemoveLastAndSecondAndMiddle_CountOfJoinedChunkRangesIsThree()
        {
            UInt64 totalBytesCountOfFile = (UInt64)m_fileInfoInServer.Length;
            List<ChunkRange> allChunkRanges = ChunkRanges( start: 0, finallyEnd: totalBytesCountOfFile - 1, DsConstants.MAX_CHUNK_SIZE, totalBytesCountOfFile );

            allChunkRanges.RemoveAt( index: allChunkRanges.Count - 1 );
            allChunkRanges.RemoveAt( 1 );
            allChunkRanges.RemoveAt( index: allChunkRanges.Count / 2 );

            List<ChunkRange> joinedChunkRanges = m_apiClient.Downloader.JoinedChunkRanges( allChunkRanges ).ToList();
            joinedChunkRanges.Count.Should().Be( expected: 3 );
        }

#if !ENABLE_DS_DOWNLOAD_FROM_CURRENT_PC
        [Test]
        public async Task DownloadFileAsync_CancelDownloadBecauseOfFileOnServerAndInFileSystemHaveDifferentVersions_DownloadIsCancelledAndFileIsDeleted()
        {
            // Because this call is not awaited, execution of the current method continues before the call is completed.
#pragma warning disable CS4014 
            //start download in another thread
            Task.Run( () => m_apiClient.DownloadFileAsync( m_rndBucketName.ServerName, m_hexPrefixOfUploadedFile, m_directoryFullNameForDownloadingFiles, m_fileInfoInServer.Name, m_objectDescription ) );
#pragma warning restore CS4014

            await Task.Delay( TimeSpan.FromSeconds( value: 2 ) );

            //create list response with different value of version of rnd file
            var objectsResponse = new ObjectsListResponse
            {
                IsSuccess = true,
                ObjectFileDescriptions = new ObjectFileDescriptionSubResponse[]
                {
                    new ObjectFileDescriptionSubResponse
                    {
                        IsDeleted = false,
                        Bytes = m_objectDescription.ByteCount,
                        Version = m_random.RandomSymbols( count: 2 ),//version always has much more symbols
                        OriginalName = m_fileInfoInServer.Name
                    }
                },

                RequestedPrefix = m_hexPrefixOfDownloadingFile
            };

            m_syncingObjects.TryCancelAllDownloadingFilesWithDifferentVersions( objectsResponse, m_rndBucketName.ServerName, out Boolean isCancelledAnyDownload );

            isCancelledAnyDownload.Should().BeTrue( because: "file on server and on local PC have different versions" );

            await Task.Delay( TimeSpan.FromSeconds( 4 ) );

            Boolean isFileDeletedAfterDownload = !File.Exists( m_pathForDownloadingFile );
            if ( !isFileDeletedAfterDownload )
            {
                File.Delete( m_pathForDownloadingFile );
                isFileDeletedAfterDownload.Should().BeTrue( "downloader should delete file after cancellation" );
            }
        }
#endif

        private FileInfo CreateFile( Int64 bytesCountOfFile, String localBucketName )
        {
            var random = new Random();

            //create name of rnd file
            String rndFileName = DEFAULT_FILE_NAME_FOR_TESTING;
            String fileExtension = Path.GetExtension( rndFileName );
            Int32 symbolsCountOfName = rndFileName.Length - fileExtension.Length;

            String pathTOBucketWhereFileWillBeCreated = Path.Combine( m_currentUser.RootFolderPath, localBucketName );
            String fullFileName = Path.Combine( pathTOBucketWhereFileWillBeCreated, rndFileName );

            while ( File.Exists( fullFileName ) )
            {
                Int32 lengthOfSeparator = 2;//separator is \\
                Int32 startIndex = random.Next( symbolsCountOfName ) + pathTOBucketWhereFileWillBeCreated.Length + lengthOfSeparator;
                String newRndSymbols = random.RandomSymbols( 1 );

                fullFileName = fullFileName.Insert( startIndex, newRndSymbols );
            }

            WriteRndFile( bytesCountOfFile, fullFileName );

            var infoOfRndFile = new FileInfo( fullFileName );
            return infoOfRndFile;
        }

        private void WriteRndFile( Int64 bytesCount, String fullFileName )
        {
            var random = new Random();
            using ( FileStream writer = File.Create( fullFileName ) )
            {
                Byte[] buffer = new Byte[ bytesCount ];
                random.NextBytes( buffer );

                Int32 chunkSize = bytesCount < DsConstants.MAX_CHUNK_SIZE ? (Int32)bytesCount : DsConstants.MAX_CHUNK_SIZE;
                for ( Int32 offset = 0; offset + chunkSize <= bytesCount && chunkSize > 0; )
                {
                    writer.Write( buffer, offset, chunkSize );
                    offset += chunkSize;

                    if ( offset + chunkSize > bytesCount )
                    {
                        chunkSize = (Int32)bytesCount - offset;
                    }
                }
            }
        }

        private List<ChunkRange> ChunkRanges(
            UInt64 start,
            UInt64 finallyEnd,
            UInt32 maxChunkSize,
            UInt64 total
        ){
            var chunkRanges = new List<ChunkRange>();

            UInt64 totalPerContact = finallyEnd + 1 - start;

            for ( UInt64 end = finallyEnd + 1 - start > maxChunkSize ? start + maxChunkSize - 1 : finallyEnd;
                 !IsLastChunk( start, end );
                 start = end + 1, end = ( ( end + maxChunkSize ) <= finallyEnd ) ? ( end + maxChunkSize ) : finallyEnd )
            {
                chunkRanges.Add( new ChunkRange( start, end, totalPerContact, total ) );
            }

            return chunkRanges;
        }

        private Boolean IsLastChunk( UInt64 start, UInt64 end ) =>
            start >= end;
    }
}