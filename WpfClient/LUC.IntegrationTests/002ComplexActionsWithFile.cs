using LUC.Interfaces.Extensions;

using NUnit.Framework;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LUC.IntegrationTests
{
    public partial class OnlineOneUserOneActionIntegrationTests
    {

        [Test, Order( 200 )]
        /// Create a file, copy, updated copied version, rename copied version, rename original file, replace original file
        public async Task LocalFileOperationsBeforeAssertTest()
        {
            String fileName = "LocalOperationsFile.txt";
            String filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
            if ( !File.Exists( filePath ) )
            {
                File.WriteAllText( filePath, m_loremIpsum );
            }

            if ( File.Exists( filePath ) )
            {
                String copiedFileName = "Copied" + fileName;
                String whereToCopyPath = Path.Combine( m_bucketPathList[ 0 ], copiedFileName );
                FileCoping( false, filePath, whereToCopyPath );
                await Task.Delay( 5000 );
                FileReplacing( false, whereToCopyPath, "AAAAA" );
                await Task.Delay( 5000 );
                String renamedCopiedFile = "Renamed" + copiedFileName;
                String howToRenameCopiedFilePath = Path.Combine( m_bucketPathList[ 0 ], renamedCopiedFile );
                FileRenaming( false, copiedFileName, howToRenameCopiedFilePath );
                await Task.Delay( 5000 );
                DateTime timeAfterRename = File.GetLastWriteTimeUtc( howToRenameCopiedFilePath );
                String renamedFileName = "Renamed" + fileName;
                String howToRenameOriginalFilePath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );
                FileRenaming( false, fileName, howToRenameOriginalFilePath );
                await Task.Delay( 11000 );
                FileReplacing( false, howToRenameOriginalFilePath, "BBBBB" );
                await Task.Delay( 5000 );
                Interfaces.OutputContracts.ObjectsListResponse list = await GetApiClientList( howToRenameOriginalFilePath, 0 );
                DateTime timeAfterReplace = File.GetLastWriteTimeUtc( howToRenameOriginalFilePath );
                AssertAny( list, renamedFileName, timeAfterReplace );
                list = await GetApiClientList( howToRenameCopiedFilePath, 0 );
                AssertAny( list, renamedCopiedFile, timeAfterRename );
            }
        }

        [Test, Order( 201 )]
        /// Create a file, rename, move back'n'forth , rename to default;
        public async Task AnotherOperationsBeforeAssertTest()
        {
            String fileName = "AnotherOperationsFile.txt";
            String filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
            if ( !File.Exists( filePath ) )
            {
                File.WriteAllText( filePath, m_loremIpsum );
            }

            if ( File.Exists( filePath ) )
            {
                String renamedFileName = "Renamed" + fileName;
                String howToRenameFilePath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );
                FileRenaming( false, fileName, howToRenameFilePath );
                String destFilePath = Path.Combine( m_bucketPathList[ 0 ], "LocalFolderForMove" );
                if ( !Directory.Exists( destFilePath ) )
                {
                    _ = Directory.CreateDirectory( destFilePath );
                }

                destFilePath = Path.Combine( destFilePath, renamedFileName );
                FileMoving( false, howToRenameFilePath, destFilePath );
                await Task.Delay( 300 * DELAY_KOEFF );
                FileMoving( false, destFilePath, howToRenameFilePath );
                await Task.Delay( 300 * DELAY_KOEFF );
                FileRenaming( false, renamedFileName, filePath );
            }

            await Task.Delay( 7000 * DELAY_KOEFF );

            Interfaces.OutputContracts.ObjectsListResponse list = await GetApiClientList( filePath, 0 );
            Assert.IsTrue( list.ObjectFileDescriptions.Any( x => x.OriginalName == fileName ) );
        }

        [Test, Order( 202 )]
        public async Task OtherOperationsBeforeAssertTest()  /// Create a file, empty file , fill file , rename;
        {
            String fileName = "OtherOperationsFile.txt";
            String filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
            if ( !File.Exists( filePath ) )
            {
                File.WriteAllText( filePath, m_loremIpsum );
            }

            if ( File.Exists( filePath ) )
            {
                File.WriteAllText( filePath, String.Empty );
                File.WriteAllText( filePath, "AAAAA" );
                String renamedFileName = "Renamed" + fileName;
                String renamedFilePath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );
                FileRenaming( false, fileName, renamedFilePath );
                DateTime timeAfterReplace = File.GetLastWriteTimeUtc( renamedFilePath );
                await Task.Delay( 7000 * DELAY_KOEFF );
                Interfaces.OutputContracts.ObjectsListResponse list = await GetApiClientList( renamedFilePath, 0 );
                AssertAny( list, renamedFileName, timeAfterReplace );
            }
        }

        [Test, Order( 203 )]
        /// Move already created file to bucket, rename, copy to same dir, delete original, erase copy;
        public async Task DifferentOperationsBeforeAssertTest()
        {
            String fileName = "Different.txt";
            String filePath = Path.Combine( m_testRootFolderPath, fileName );
            if ( !File.Exists( filePath ) )
            {
                File.WriteAllText( filePath, m_loremIpsum );
            }

            if ( File.Exists( filePath ) )
            {
                String destFilePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
                await Task.Delay( 3000 * DELAY_KOEFF );
                FileMoving( false, filePath, destFilePath );
                await Task.Delay( 3000 * DELAY_KOEFF );
                String renamedFileName = "Renamed" + fileName;
                destFilePath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );
                FileRenaming( false, fileName, destFilePath );  // TODO RR 
                await Task.Delay( 3000 * DELAY_KOEFF );
                String copiedFileName = "Copied" + renamedFileName;
                String whereToCopyPath = Path.Combine( m_bucketPathList[ 0 ], copiedFileName );
                FileCoping( false, destFilePath, whereToCopyPath ); // TODO RR copied Different.txt when it's already renamed to RenamedDifferent.txt !
                await Task.Delay( 3000 * DELAY_KOEFF );
                File.Delete( destFilePath );
                File.WriteAllText( whereToCopyPath, String.Empty );
                await Task.Delay( 7000 * DELAY_KOEFF );
                Interfaces.OutputContracts.ObjectsListResponse list = await GetApiClientList( destFilePath, 0 );

                Assert.IsTrue( list.ObjectFileDescriptions.Any( x => x.OriginalName == renamedFileName && x.IsDeleted ), "Deleted file still exist!" ); //DEBUG
                await AssertThatDeleted( whereToCopyPath, 0 );  //in new version we don`t need delete empty file
            }
        }
        [Test, Order( 204 )]
        public async Task RenameMoveRenameBeforeAssertTest()  //Create a file, rename, move, rename
        {
            String fileName = "Complex.txt";
            String filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
            if ( !File.Exists( filePath ) )
            {
                File.WriteAllText( filePath, m_loremIpsum );
            }

            if ( File.Exists( filePath ) )
            {
                String renamedFileName = "Renamed" + fileName;
                String renamedFilePath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );
                FileRenaming( false, fileName, renamedFilePath ); // Rename
                String destFilePath = Path.Combine( m_bucketPathList[ 0 ], "LocalFolderForMove" );
                if ( !Directory.Exists( destFilePath ) )
                {
                    _ = Directory.CreateDirectory( destFilePath );
                    await Task.Delay( 100 );
                }

                destFilePath = Path.Combine( destFilePath, renamedFileName );
                FileMoving( false, renamedFilePath, destFilePath ); //Move
                filePath = Path.Combine( m_bucketPathList[ 0 ], "LocalFolderForMove", fileName );
                if ( File.Exists( destFilePath ) )
                {
                    File.Move( destFilePath, filePath );  // Rename
                }

                await Task.Delay( 20000 * DELAY_KOEFF );
            }

            Interfaces.OutputContracts.ObjectsListResponse list = await GetApiClientList( filePath, 0 );
            DateTime timeAfter = File.GetLastWriteTimeUtc( filePath );
            Assert.IsTrue( list.ObjectFileDescriptions.Where( x => x.IsDeleted is false ).Any( x => x.OriginalName == fileName &&
                                                                                             DateTimeExtensions.AssumeIsTheSame( x.LastModifiedUtc.FromUnixTimeStampToDateTime(),
                                                                                             timeAfter ) ), $"Expected file {fileName} does not exist" );
        }

        [Test, Order( 205 )]
        public async Task MoveRenameMoveBeforeAssertTest()  //Create a file, move, rename, move
        {
            String fileName = "MoveRenameMove.txt";
            String filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
            if ( !File.Exists( filePath ) )
            {
                File.WriteAllText( filePath, m_loremIpsum );
            }

            String destFilePath = Path.Combine( m_bucketPathList[ 0 ], "LocalFolderForMove" );
            if ( !Directory.Exists( destFilePath ) )
            {
                _ = Directory.CreateDirectory( destFilePath );
                await Task.Delay( 100 );
            }

            destFilePath = Path.Combine( destFilePath, fileName );
            FileMoving( false, filePath, destFilePath ); //Move
            String renamedFileName = "Renamed" + fileName;
            filePath = Path.Combine( m_bucketPathList[ 0 ], "LocalFolderForMove", renamedFileName );
            if ( File.Exists( destFilePath ) )
            {
                File.Move( destFilePath, filePath );  // Rename
            }

            destFilePath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );
            FileMoving( false, filePath, destFilePath ); //Move
            await Task.Delay( 20000 * DELAY_KOEFF );
            DateTime timeAfter = File.GetLastWriteTimeUtc( destFilePath );
            Interfaces.OutputContracts.ObjectsListResponse list = await GetApiClientList( destFilePath, 0 );
            Assert.IsTrue( list.ObjectFileDescriptions.Where( x => x.IsDeleted is false ).Any( x => x.OriginalName == renamedFileName &&
                                                                                             DateTimeExtensions.AssumeIsTheSame( x.LastModifiedUtc.FromUnixTimeStampToDateTime(),
                                                                                             timeAfter ) ), $"Expected file {renamedFileName} does not exist or local time not equal with server" );
        }
    }
}
