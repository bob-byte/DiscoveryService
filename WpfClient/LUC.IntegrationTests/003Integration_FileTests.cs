using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LUC.IntegrationTests
{
    public partial class OnlineOneUserOneActionIntegrationTests
    {

        /// <summary>
        ///     GetApiClientList method
        /// </summary>
        /// <param name="path">Insert path to ExtractPrefix</param>
        /// <param name="bucket">0 - First bucket; 1 - Second bucket;</param>
        public Task<ObjectsListResponse> GetApiClientList( String path, Int32 bucket, Boolean isShowDeleted = false )
        {
            IList<String> bucketDirectoryPaths = CurrentUserProvider.ProvideBucketDirectoryPaths();
            String serverBucketName = CurrentUserProvider
                .GetBucketNameByDirectoryPath( bucketDirectoryPaths.ElementAt( bucket ) ).ServerName;

            if ( String.IsNullOrEmpty( path ) )
            {
                throw new ArgumentException( "Path is empty!" );
            }

            String serverPrefix = CurrentUserProvider.ExtractPrefix( path );

            return GetInternalApiClientList( serverBucketName, serverPrefix, isShowDeleted );
        }

        public async Task<ObjectsListResponse> GetInternalApiClientList( String serverBucketName, String serverPrefix, Boolean showDeleted ) => await ApiClient.ListAsync( serverBucketName, serverPrefix, showDeleted );

        /// <summary>
        ///     CreateFile method
        /// </summary>
        /// <param name="isBig">true - creating big file; false - creating small file;</param>
        /// <param name="isReadOnly">true - setting ReadOnly attribute; false - setting Normal attribute;</param>
        /// <param name="fileName">file name working with;</param>
        /// <param name="inetSpeed">internet speed for delay calculation;</param>
        public void CreateFile( Boolean isBig, Boolean isReadOnly, String fileName, Double inetSpeed )
        {
            String filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );

            if ( isBig ) // Creating big file 
            {
                if ( !File.Exists( filePath ) )
                {
                    var fs = new FileStream( filePath, FileMode.CreateNew );
                    _ = fs.Seek( 10L * 1024 * 1024, SeekOrigin.Begin );
                    fs.WriteByte( 0 );
                    fs.Close();
                    SetAttributesForFile( isReadOnly, filePath );
                }
            }
            else // Creating small file 
            {
                if ( !File.Exists( filePath ) )
                {
                    File.WriteAllText( filePath, m_loremIpsum );
                    SetAttributesForFile( isReadOnly, filePath );
                }
            }
        }

        public async Task CreateFileAndAssertWhetherItUploadedOnServer( Boolean isFileBig, Boolean isReadOnly, String filePath )
        {
            ObjectsListResponse list;
            FileInfo ourFile;
            String fileName;

            fileName = Path.GetFileName( filePath );
            CreateFile( isFileBig, isReadOnly, fileName, m_uploadSpeedKbps );
            ourFile = new FileInfo( filePath );
            Int64 fileSize = ourFile.Length;
            IEnumerable<ObjectFileDescriptionSubResponse> foo;
            do
            {
                await Task.Delay( 1000 );
                list = await GetApiClientList( filePath, 0 );
                foo = list.ObjectFileDescriptions.Where( x => x.OriginalName == fileName );
            } while ( !foo.Any() );
            Assert.IsTrue( list.ObjectFileDescriptions.Any( x => x.OriginalName == fileName && x.Bytes == fileSize ) ); // TODO 2.0 - Check MD5 
        }

        public String GetRenamedPathAndName( String filePathBeforeRename )
        {
            String fileName = Path.GetFileName( filePathBeforeRename );
            String renamedFileName = "Renamed" + fileName;
            String renamedFilePath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );

            return renamedFilePath;
        }

        public void SetAttributesForFile( Boolean isReadOnly, String filePath )
        {
            if ( !File.Exists( filePath ) )
            {
                return;
            }

            File.SetAttributes( filePath, isReadOnly ? FileAttributes.ReadOnly : FileAttributes.Normal );
        }

        public async Task<RenamedFileProperties> FileRenamingAndSettingAttributeReturningList(
            Boolean isReadOnly,
            String fileToRename,
            String howToRenamePath )
        {
            FileRenaming( isReadOnly, fileToRename, howToRenamePath );
            String renamedFileName = Path.GetFileName( howToRenamePath );
            await Task.Delay( ( 5000 * DELAY_KOEFF ) + 5000 );
            ObjectsListResponse list = await GetApiClientList( howToRenamePath, 0 );
            var renamedFileProp = new RenamedFileProperties
            {
                ListResponse = list,
                FileName = renamedFileName
            };

            return renamedFileProp;
        }

        public void FileRenaming( Boolean isReadOnly, String fileToRename, String howToRenamePath )
        {
            if ( !File.Exists( Path.Combine( m_bucketPathList[ 0 ], fileToRename ) ) )
            {
                return;
            }

            File.Move( Path.Combine( m_bucketPathList[ 0 ], fileToRename ), howToRenamePath );
            SetAttributesForFile( isReadOnly, howToRenamePath ); // if isReadOnly = false - sets attribute as normal
        }

        public async Task<ReplacedFileProperties> FileReplacingAndReturningList(
            Boolean isReadOnly,
            String replaceFilePath,
            String textForReplace )
        {
            FileReplacing( isReadOnly, replaceFilePath, textForReplace );
            await Task.Delay( ( 5000 * DELAY_KOEFF ) + 5000 );
            ObjectsListResponse list = await GetApiClientList( replaceFilePath, 0 );
            DateTime timeAfterReplace = File.GetLastWriteTimeUtc( replaceFilePath );
            var replacedFileProp = new ReplacedFileProperties
            {
                ListResponse = list,
                TimeAfterReplace = timeAfterReplace
            };

            return replacedFileProp;
        }

        public void FileReplacing( Boolean isReadOnly, String replaceFilePath, String textForReplace )
        {
            if ( File.Exists( replaceFilePath ) )
            {
                File.WriteAllText( replaceFilePath, textForReplace );
                SetAttributesForFile( isReadOnly, replaceFilePath ); // if isReadOnly = false - sets attribute as normal
            }
        }

        public void FileMoving( Boolean isReadOnly, String sourceFilePath, String whereToMovePath )
        {
            if ( File.Exists( sourceFilePath ) )
            {
                File.Move( sourceFilePath, whereToMovePath );
            }

            SetAttributesForFile( isReadOnly, whereToMovePath ); // if isReadOnly = false - sets attribute as normal
        }

        public void FileCoping( Boolean isReadOnly, String sourceFilePath, String whereToCopyFilePath )
        {
            if ( File.Exists( sourceFilePath ) )
            {
                File.Copy( sourceFilePath, whereToCopyFilePath );
            }

            SetAttributesForFile( isReadOnly, whereToCopyFilePath ); // if isReadOnly = false - sets attribute as normal
        }

        public async Task AssertLockAndUnlockForFile( String filePath, Int32 bucket )
        {
            var fileInfo = new FileInfo( filePath );

            if ( File.Exists( filePath ) )
            {
                AdsExtensions.WriteLockDescription( filePath, new LockDescription( AdsLockState.ReadyToLock ) ); // LOCK
                await Task.Delay( 5000 * DELAY_KOEFF );
                await AssertThatLocked( filePath, bucket );
                AdsExtensions.WriteLockDescription( filePath, new LockDescription( AdsLockState.ReadyToUnlock ) ); // UNLOCK
                await Task.Delay( 5000 * DELAY_KOEFF );
                ObjectsListResponse list = await GetApiClientList( filePath, bucket );
                Assert.IsTrue( list.ObjectFileDescriptions.Where( x => x.IsDeleted is false ).SingleOrDefault( x =>
                        x.OriginalName == fileInfo.Name &&
                        !x.IsLocked ) != null, "Unlock assert != true" );
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        public async Task DeleteAndAssertFile( String filePath, Int32 bucket )
        {
            if ( File.Exists( filePath ) )
            {
                File.Delete( filePath );
            }

            await Task.Delay( 5000 * DELAY_KOEFF );

            await AssertThatDeleted( filePath, bucket );
        }

        public async Task AssertThatLocked( String filePath, Int32 bucket )
        {
            String fileName = Path.GetFileName( filePath );
            ObjectsListResponse list = await GetApiClientList( filePath, bucket );
            Assert.IsTrue( list.ObjectFileDescriptions.SingleOrDefault( x => x.IsLocked &&
                      x.OriginalName == fileName ) != null,
                "File doesn't locked or user name/user id is wrong" );
        }

        public void AssertThatRenamed( ObjectsListResponse list, String beforeRenameName, String afterRenameName )
        {
            Assert.IsFalse( list.ObjectFileDescriptions.Any( x => x.OriginalName == beforeRenameName ),
                "File with this name still exist" );

            Assert.IsTrue(
                list.ObjectFileDescriptions.Where( x => x.IsDeleted is false ).Any( x => x.OriginalName == afterRenameName ),
                "File doesn't renamed or name is wrong" );
        }

        public void AssertAny( ObjectsListResponse list, String originalName, DateTime localTimeOfChange )
        {
            IEnumerable<ObjectFileDescriptionSubResponse> notDeletedList = list.ObjectFileDescriptions.Where( x => x.IsDeleted is false );
            DateTime utcOfList = DateTime.UtcNow;
            if ( notDeletedList.Any() )
            {
                utcOfList = notDeletedList.First().LastModifiedUtc.FromUnixTimeStampToDateTime();
            }

            Assert.IsTrue( notDeletedList.Any( x =>
                      x.OriginalName == originalName ),
                $"File {originalName} doesn't exists. DateTime on server {utcOfList}, and local DateTime is {localTimeOfChange}" );
        }

        public async Task AssertThatMoved( String srcFilePath, String destFilePath, Int32 srcBucket, Int32 destBucket )
        {
            String fileName = Path.GetFileName( srcFilePath );
            ObjectsListResponse list = await GetApiClientList( srcFilePath, srcBucket );

            Assert.IsFalse( list.ObjectFileDescriptions.Any( x => x.IsDeleted is false && x.OriginalName == fileName ),
                "After move file still in source folder" );

            list = await GetApiClientList( destFilePath, destBucket );

            Assert.IsTrue( list.ObjectFileDescriptions.Any( x => x.IsDeleted is false && x.OriginalName == fileName ),
                "File does not moved to dest folder" );
        }

        public async Task AssertThatCopied( String srcFilePath, String destFilePath, Int32 srcBucket, Int32 destBucket )
        {
            String copiedFile = Path.GetFileName( destFilePath );
            ObjectsListResponse list = await GetApiClientList( srcFilePath, srcBucket );
            Assert.IsTrue( list.ObjectFileDescriptions.Any(),
                "File which tried to be copied - doesn't exist" );

            list = await GetApiClientList( destFilePath, destBucket );
            Assert.IsTrue( list.ObjectFileDescriptions.Any( x => x.OriginalName == copiedFile ),
                $"There is no {copiedFile} is destination folder" );
        }

        public async Task AssertThatDeleted( String filePath, Int32 bucket )
        {
            ObjectsListResponse list = await GetApiClientList( filePath, bucket );
            Assert.IsFalse( list.ObjectFileDescriptions.Any(),
                "Deleted file still exist!" );
        }

        [Test, Order( 300 )]
        public async Task AllActionsWithLockedSmallFileTest()
        {
            String fileName = "AllActionsWithLock.txt";
            String filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
            await CreateFileAndAssertWhetherItUploadedOnServer( false, false, filePath );
            await Task.Delay( 3000 * DELAY_KOEFF );
            if ( File.Exists( filePath ) )
            {
                AdsExtensions.WriteLockDescription( filePath, new LockDescription( AdsLockState.ReadyToLock ) ); // LOCK
                await Task.Delay( 3000 * DELAY_KOEFF );
                await AssertThatLocked( filePath, 0 );
                // -= Rename Locked File =-
                String renamedFileName = "Renamed" + fileName;
                String howToRenameFilePath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );
                RenamedFileProperties Renamed = await FileRenamingAndSettingAttributeReturningList( false, fileName, howToRenameFilePath );
                await Task.Delay( 3000 * DELAY_KOEFF );
                AssertThatRenamed( Renamed.ListResponse, fileName, renamedFileName );
                // -= Update Locked File =-
                ReplacedFileProperties Replaced = await FileReplacingAndReturningList( false, howToRenameFilePath, "AAAAA" );
                await Task.Delay( 7000 * DELAY_KOEFF );
                AssertAny( Replaced.ListResponse, renamedFileName, Replaced.TimeAfterReplace );
                // -= Move Locked File =-
                String destFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName );
                if ( !Directory.Exists( destFilePath ) )
                {
                    _ = Directory.CreateDirectory( destFilePath );
                }

                await Task.Delay( 7000 * DELAY_KOEFF );
                destFilePath = Path.Combine( destFilePath, renamedFileName );
                FileMoving( false, howToRenameFilePath, destFilePath );
                await Task.Delay( 12000 * DELAY_KOEFF );
                await AssertThatMoved( howToRenameFilePath, destFilePath, 0, 0 );
                // -= Copy Locked File =-
                String copyFileDestPath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );
                FileCoping( false, destFilePath, copyFileDestPath );
                await Task.Delay( 9000 * DELAY_KOEFF );
                await AssertThatCopied( destFilePath, copyFileDestPath, 0, 0 );
                // -= Delete Locked File =-
                await DeleteAndAssertFile( destFilePath, 0 );
            }
        }

        [Test, Order( 301 )]
        public async Task CreateSmallFileTest()
        {
            String createFilePath = Path.Combine( m_bucketPathList[ 0 ], m_smallFileName );
            await CreateFileAndAssertWhetherItUploadedOnServer( false, false, createFilePath );
            await AssertLockAndUnlockForFile( createFilePath, 0 );
        }

        [Test, Order( 302 )]
        public async Task ReadOnlyRenameSmallFileTest()
        {
            String howToRenameFilePath = Path.Combine( m_bucketPathList[ 0 ], m_readOnlySmallFileName );
            RenamedFileProperties Renamed = await FileRenamingAndSettingAttributeReturningList( true, m_smallFileName, howToRenameFilePath );
            AssertThatRenamed( Renamed.ListResponse, m_smallFileName, m_readOnlySmallFileName );
            await AssertLockAndUnlockForFile( howToRenameFilePath, 0 );
        }

        [Test, Order( 303 )]
        public async Task RenameSmallFileTest()
        {
            String howToRenameFilePath = Path.Combine( m_bucketPathList[ 0 ], m_renamedSmallFileName );
            RenamedFileProperties Renamed = await FileRenamingAndSettingAttributeReturningList( false, m_readOnlySmallFileName, howToRenameFilePath );
            AssertThatRenamed( Renamed.ListResponse, m_readOnlySmallFileName, m_renamedSmallFileName );
            await AssertLockAndUnlockForFile( howToRenameFilePath, 0 );
        }

        [Test, Order( 304 )]
        public async Task ReadOnlyReplaceSmallFileTest()
        {
            String replaceFilePath = GetRenamedPathAndName( m_fullNameOfSmallFile );
            String replacedFileName = Path.GetFileName( replaceFilePath );
            ReplacedFileProperties Replaced = await FileReplacingAndReturningList( true, replaceFilePath, "Text replaced" );
            AssertAny( Replaced.ListResponse, replacedFileName, Replaced.TimeAfterReplace );
            await AssertLockAndUnlockForFile( replaceFilePath, 0 );
        }

        [Test, Order( 305 )]
        public async Task ReplaceSmallFileTest()
        {
            String replaceFilePath = GetRenamedPathAndName( m_fullNameOfSmallFile );
            String replacedFileName = Path.GetFileName( replaceFilePath );
            ReplacedFileProperties Replaced = await FileReplacingAndReturningList( false, replaceFilePath, m_loremIpsum );
            AssertAny( Replaced.ListResponse, replacedFileName, Replaced.TimeAfterReplace );
            await AssertLockAndUnlockForFile( replaceFilePath, 0 );
        }

        [Test, Order( 306 )]
        public async Task ReadOnlyMoveToFolderSmallFileTest() // One readOnly test method for 2 move cases 
        {
            String srcFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName );
            String fileToMovePath = Path.Combine( srcFilePath, m_readOnlySmallFileName );

            if ( !Directory.Exists( srcFilePath ) )
            {
                _ = Directory.CreateDirectory( srcFilePath );
                await Task.Delay( 6000 * DELAY_KOEFF );
            }

            if ( !File.Exists( fileToMovePath ) )
            {
                File.WriteAllText( fileToMovePath, m_loremIpsum );
                await Task.Delay( 10000 * DELAY_KOEFF );
            }

            String whereToMovePath = Path.Combine( m_bucketPathList[ 0 ], m_readOnlySmallFileName );
            FileMoving( true, fileToMovePath, whereToMovePath ); // Move from folder to first bucket to save logic and set readOnly
            await Task.Delay( 15000 * DELAY_KOEFF );
            await AssertLockAndUnlockForFile( whereToMovePath, 0 );
            await AssertThatMoved( fileToMovePath, whereToMovePath, 0, 0 );
        }

        [Test, Order( 307 )]
        public async Task MoveToFolderSmallFileTest()
        {
            String srcFilePath = GetRenamedPathAndName( m_fullNameOfSmallFile );
            String renamedFileName = Path.GetFileName( srcFilePath );
            String destFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName, renamedFileName );

            FileMoving( false, srcFilePath, destFilePath );
            await Task.Delay( 20000 * DELAY_KOEFF );
            await AssertLockAndUnlockForFile( destFilePath, 0 );
            await AssertThatMoved( srcFilePath, destFilePath, 0, 0 );
        }

        [Test, Order( 308 )]
        public async Task ReadOnlyCopyUpSmallFileTest() // One readOnly test method for 2 copy cases
        {
            String filePath = GetRenamedPathAndName( m_fullNameOfSmallFile );
            String renamedFileName = Path.GetFileName( filePath );
            String srcFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName, renamedFileName );
            String destFilePath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );
            FileCoping( true, srcFilePath, destFilePath );
            await Task.Delay( 7000 * DELAY_KOEFF );
            await AssertLockAndUnlockForFile( destFilePath, 0 );
            await AssertThatCopied( srcFilePath, destFilePath, 0, 0 );
        }

        [Test, Order( 309 )]
        public async Task CopyDownSmallFileTest()
        {
            String filePath = Path.Combine( m_bucketPathList[ 0 ], m_fullNameOfSmallFile );
            String renamedFileName = Path.GetFileName( filePath );
            String srcFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName, renamedFileName );
            String destFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName, m_folderToMoveName );
            String whereToCopyFilePath = Path.Combine( destFilePath, renamedFileName );

            if ( !Directory.Exists( destFilePath ) )
            {
                _ = Directory.CreateDirectory( destFilePath );
            }

            await Task.Delay( 7000 * DELAY_KOEFF );
            CreateFile( false, false, srcFilePath, m_uploadSpeedKbps );
            await Task.Delay( 5000 * DELAY_KOEFF );
            FileCoping( false, srcFilePath, whereToCopyFilePath );
            await Task.Delay( 7000 * DELAY_KOEFF );
            await AssertThatCopied( srcFilePath, whereToCopyFilePath, 0, 0 );
            await AssertLockAndUnlockForFile( whereToCopyFilePath, 0 );
        }

        [Test, Order( 310 )]
        public async Task ReadOnlyCopytoSameDirectorySmallFileTest()
        {
            String copiedFileName = "Copied" + m_smallFileName;
            String srcFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName, m_renamedSmallFileName );
            String destFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName );
            String whereToCopyFilePath = Path.Combine( destFilePath, copiedFileName );
            FileCoping( true, srcFilePath, whereToCopyFilePath );
            await Task.Delay( 7000 * DELAY_KOEFF );
            await AssertThatCopied( srcFilePath, whereToCopyFilePath, 0, 0 );
            await AssertLockAndUnlockForFile( whereToCopyFilePath, 0 );
        }

        [Test, Order( 311 )]
        public async Task DeleteReadOnlySmallFileTest()
        {
            String copiedFileName = "Copied" + m_smallFileName;
            String deleteFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName, copiedFileName );
            await DeleteAndAssertFile( deleteFilePath, 0 );
        }

        [Test, Order( 312 )]
        public async Task MoveToBucketSmallFileTest()
        {
            String bucketName2 = Directory.EnumerateDirectories( m_testRootFolderPath ).ElementAt( 1 );
            String srcFilePath = GetRenamedPathAndName( m_fullNameOfSmallFile );
            String renamedFileName = Path.GetFileName( srcFilePath );
            String destFilePath = Path.Combine( bucketName2, renamedFileName );
            File.WriteAllText( srcFilePath, m_loremIpsum );

            if ( File.Exists( srcFilePath ) )
            {
                File.Move( srcFilePath, destFilePath );
            }

            await Task.Delay( 7000 );
            await AssertThatMoved( srcFilePath, destFilePath, 0, 1 );
            await AssertLockAndUnlockForFile( destFilePath, 1 );
        }

        [Test, Order( 313 )]
        public async Task ReplaceInFolderSmallFileTest()
        {
            String srcFilePath = GetRenamedPathAndName( m_fullNameOfSmallFile );
            String renamedFileName = Path.GetFileName( srcFilePath );
            String filePath = Path.Combine( m_bucketPathList[ 0 ], "FolderForFile", renamedFileName );

            if ( File.Exists( filePath ) )
            {
                File.WriteAllText( filePath, "Text replaced" );
            }

            await Task.Delay( 15000 * DELAY_KOEFF );
            ObjectsListResponse list = await GetApiClientList( filePath, 0 );
            DateTime timeAfterReplace = File.GetLastWriteTimeUtc( filePath );
            AssertAny( list, renamedFileName, timeAfterReplace );
            await AssertLockAndUnlockForFile( filePath, 0 );
        }

        #region BigFileTests

        [Test, Order( 350 )]
        public async Task CreateBigFileTest()
        {
            String filePath = Path.Combine( m_bucketPathList[ 0 ], m_bigFileName );
            await CreateFileAndAssertWhetherItUploadedOnServer( true, false, filePath );
            await AssertLockAndUnlockForFile( filePath, 0 );
        }

        [Test, Order( 351 )]
        public async Task CreateReadOnlyBigFileTest()
        {
            String filePath = Path.Combine( m_bucketPathList[ 0 ], m_readOnlyBigFileName );
            await CreateFileAndAssertWhetherItUploadedOnServer( true, true, filePath );
            await AssertLockAndUnlockForFile( filePath, 0 );
        }

        [Test, Order( 352 )]
        public async Task RenameBigFileTest()
        {
            String filePath = Path.Combine( m_bucketPathList[ 0 ], m_bigFileName );
            String renameFilePath = GetRenamedPathAndName( filePath );
            String renamedFileName = Path.GetFileName( renameFilePath );
            if ( File.Exists( filePath ) )
            {
                File.Move( filePath, renameFilePath );
            }

            await Task.Delay( 15000 * DELAY_KOEFF );
            ObjectsListResponse list = await GetApiClientList( renameFilePath, 0 );
            AssertThatRenamed( list, m_bigFileName, renamedFileName );
            await AssertLockAndUnlockForFile( renameFilePath, 0 );
        }

        [Test, Order( 353 )]
        public async Task MoveToFolderBigFileTest()
        {
            String filePath = Path.Combine( m_bucketPathList[ 0 ], m_bigFileName );
            String srcFilePath = GetRenamedPathAndName( filePath );
            String renamedFileName = Path.GetFileName( srcFilePath );
            String destFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName );
            if ( !Directory.Exists( destFilePath ) )
            {
                _ = Directory.CreateDirectory( destFilePath );
            }

            await Task.Delay( 5000 * DELAY_KOEFF );
            destFilePath = Path.Combine( destFilePath, renamedFileName );
            if ( File.Exists( srcFilePath ) )
            {
                File.Move( srcFilePath, destFilePath );
            }

            await Task.Delay( 7000 * DELAY_KOEFF );
            await AssertThatMoved( srcFilePath, destFilePath, 0, 0 );
            await AssertLockAndUnlockForFile( destFilePath, 0 );
        }

        [Test, Order( 354 )]
        public async Task CopyUpBigFileTest()
        {
            String filePath = Path.Combine( m_bucketPathList[ 0 ], m_bigFileName );
            String renamedfilePath = GetRenamedPathAndName( filePath );
            String renamedFileName = Path.GetFileName( renamedfilePath );
            String srcFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName, renamedFileName );
            String destFilePath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );
            if ( File.Exists( srcFilePath ) )
            {
                File.Copy( srcFilePath, destFilePath );
            }

            await Task.Delay( 25000 * DELAY_KOEFF );
            await AssertThatCopied( srcFilePath, destFilePath, 0, 0 );
            await AssertLockAndUnlockForFile( destFilePath, 0 );
        }

        [Test, Order( 355 )]
        public async Task CopyDownBigFileTest()
        {
            String filePath = Path.Combine( m_bucketPathList[ 0 ], m_bigFileName );
            String renamedFilePath = GetRenamedPathAndName( filePath );
            String renamedFileName = Path.GetFileName( renamedFilePath );
            String srcFilePath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );
            String destFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName );

            if ( !Directory.Exists( destFilePath ) )
            {
                _ = Directory.CreateDirectory( destFilePath );
            }

            destFilePath = Path.Combine( destFilePath, renamedFileName );
            CreateFile( true, false, srcFilePath, m_uploadSpeedKbps );
            await Task.Delay( 10000 * DELAY_KOEFF );
            if ( File.Exists( srcFilePath ) )
            {
                File.Copy( srcFilePath, destFilePath );
            }

            await Task.Delay( 10000 * DELAY_KOEFF );
            await AssertThatCopied( srcFilePath, destFilePath, 0, 0 );
            await AssertLockAndUnlockForFile( destFilePath, 0 );
        }

        [Test, Order( 356 )]
        public async Task CopytoSameBigFileTest()
        {
            String filePath = Path.Combine( m_bucketPathList[ 0 ], m_bigFileName );
            String renamedFilePath = GetRenamedPathAndName( filePath );
            String renamedFileName = Path.GetFileName( renamedFilePath );
            String srcFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName, renamedFileName );
            String destFilePath = Path.Combine( m_bucketPathList[ 0 ], m_folderToMoveName );

            if ( !Directory.Exists( destFilePath ) )
            {
                _ = Directory.CreateDirectory( destFilePath );
            }

            await Task.Delay( 300 * DELAY_KOEFF );
            destFilePath = Path.Combine( destFilePath, renamedFileName );

            CreateFile( true, false, srcFilePath, m_uploadSpeedKbps );

            if ( File.Exists( srcFilePath ) )
            {
                File.Copy( srcFilePath, renamedFilePath );
            }

            await Task.Delay( 7000 * DELAY_KOEFF );
            await AssertThatCopied( srcFilePath, destFilePath, 0, 0 );
            await AssertLockAndUnlockForFile( destFilePath, 0 );
        }

        [Test, Order( 357 )]
        public async Task DeleteBigFileTest()
        {
            String copiedFileName = "Copied" + m_bigFileName;
            String deleteFilePath = Path.Combine( m_bucketPathList[ 0 ], copiedFileName );
            await CreateFileAndAssertWhetherItUploadedOnServer( true, false, deleteFilePath );
            await Task.Delay( 7000 * DELAY_KOEFF );
            await DeleteAndAssertFile( deleteFilePath, 0 );
        }

        [Test, Order( 358 )]
        public async Task MoveToBucketBigFileTest()
        {
            await DeleteEverythingFromServer();
            String bucketName2 = Directory.EnumerateDirectories( m_testRootFolderPath ).ElementAt( 1 );
            String filePath = Path.Combine( m_bucketPathList[ 0 ], m_bigFileName );
            String srcFilePath = GetRenamedPathAndName( filePath );
            String renamedFileName = Path.GetFileName( srcFilePath );
            String destFilePath = Path.Combine( bucketName2, renamedFileName );
            await CreateFileAndAssertWhetherItUploadedOnServer( true, false, srcFilePath );
            await Task.Delay( 1000 );

            if ( File.Exists( srcFilePath ) )
            {
                File.Move( srcFilePath, destFilePath );
            }

            await Task.Delay( 7000 );
            await AssertThatMoved( srcFilePath, destFilePath, 0, 1 );
            await AssertLockAndUnlockForFile( destFilePath, 1 );
        }

        [Test, Order( 359 )]
        public async Task ReplaceInFolderBigFileTest()
        {
            String filePath = Path.Combine( m_bucketPathList[ 0 ], m_bigFileName );
            String srcFilePath = GetRenamedPathAndName( filePath );
            String renamedFileName = Path.GetFileName( srcFilePath );
            String destFilePath = Path.Combine( m_bucketPathList[ 0 ], "FolderForFile", renamedFileName );

            if ( File.Exists( destFilePath ) )
            {
                File.WriteAllText( destFilePath, "Text replaced" );
            }

            await Task.Delay( 15000 * DELAY_KOEFF );
            ObjectsListResponse list = await GetApiClientList( destFilePath, 0 );
            DateTime timeAfterReplace = File.GetLastWriteTimeUtc( destFilePath );
            AssertAny( list, renamedFileName, timeAfterReplace );
            await AssertLockAndUnlockForFile( destFilePath, 0 );
        }

        [Test]
        [Order( 360 )]
        public async Task SpecialCaseBigFileTest()
        {
            String fileName = "SpecialBigFile.txt";
            String filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );

            var fs = new FileStream( filePath, FileMode.CreateNew );
            _ = fs.Seek( 20L * 1024 * 1024, SeekOrigin.Begin );
            fs.WriteByte( 0 );
            //fs.Close();             // TODO O Check Md5Hash 
            await Task.Delay( 1000 );
            _ = fs.Seek( 20L * 1024 * 1024, SeekOrigin.Begin );
            fs.WriteByte( 0x41 );
            fs.Close();
        }
        #endregion

        [Test]
        [Order( 365 )]
        public async Task SpecialCaseDeleteFileTest()
        {
            String fileName = "TryToEmptyFile.txt";
            String createFilePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
            await CreateFileAndAssertWhetherItUploadedOnServer( false, false, createFilePath );
            File.WriteAllText( createFilePath, String.Empty );
            await Task.Delay( 5000 );
            Assert.AreEqual( 0, new FileInfo( createFilePath ).Length );
            await Task.Delay( 5000 * DELAY_KOEFF );
            await AssertThatDeleted( createFilePath, 0 );
        }
    }
}
