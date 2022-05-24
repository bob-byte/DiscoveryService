using Common.Exceptions;

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
        [Test, Order( 400 )]
        public async Task CreateDirectoryTest()
        {
            System.Collections.Generic.IEnumerable<String> buckets = Directory.EnumerateDirectories( m_testRootFolderPath );

            foreach ( String bucket in buckets )
            {
                _ = Directory.CreateDirectory( Path.Combine( bucket, m_testFolderName ) );
            }

            await Task.Delay( 7000 );

            System.Collections.Generic.IList<String> bucketDirectoryPathes = CurrentUserProvider.ProvideBucketDirectoryPaths();

            foreach ( String path in bucketDirectoryPathes )
            {
                String serverBucketName = CurrentUserProvider.GetBucketNameByDirectoryPath( path ).ServerName;
                Interfaces.OutputContracts.ObjectsListResponse result = await ApiClient.ListAsync( serverBucketName, String.Empty );

                Assert.IsTrue( result.Directories.Where( x => x.IsDeleted is false ).Any( x => x.HexPrefix == m_testFolderName.ToHexPrefix().ToLowerInvariant() ) );
            }
        }

        [Test, Order( 401 )]
        public async Task DeleteDirectoryTest()
        {
            // string testFolderName = "JustFolder";
            String expectedDeletedName;
            System.Collections.Generic.IEnumerable<String> buckets = Directory.EnumerateDirectories( m_testRootFolderPath );

            foreach ( String bucket in buckets )
            {
                String bucketFolderName = Path.Combine( bucket, m_testFolderName );
                if ( Directory.Exists( bucketFolderName ) )
                {
                    Directory.Delete( bucketFolderName );
                }
            }

            await Task.Delay( 20000 );

            System.Collections.Generic.IList<String> bucketDirectoryPathes = CurrentUserProvider.ProvideBucketDirectoryPaths();

            foreach ( String path in bucketDirectoryPathes )
            {
                String serverBucketName = CurrentUserProvider.GetBucketNameByDirectoryPath( path ).ServerName;
                Interfaces.OutputContracts.ObjectsListResponse result = await ApiClient.ListAsync( serverBucketName, String.Empty );
                expectedDeletedName = m_testFolderName + "-deleted";
                Assert.IsTrue( result.Directories.Any( x => x.IsDeleted && x.HexPrefix.FromHexString().Contains( expectedDeletedName ) ) );
            }
        }

        [Test, Order( 402 )]
        public async Task CreateTreeDirectoryTest()
        {
            System.Collections.Generic.IEnumerable<String> buckets = Directory.EnumerateDirectories( m_testRootFolderPath );
            String currentFolderPath = buckets.First();
            String treeFolderName;
            for ( Int32 i = 1; i <= 10; i++ )
            {
                treeFolderName = m_testFolderName + i; // create folder name
                currentFolderPath = Path.Combine( currentFolderPath, treeFolderName ); // create full path to folder

                if ( !Directory.Exists( currentFolderPath ) )
                {
                    _ = Directory.CreateDirectory( currentFolderPath );
                }
            }

            await Task.Delay( 10000 );
            System.Collections.Generic.IList<String> bucketDirectoryPathes = CurrentUserProvider.ProvideBucketDirectoryPaths();
            Interfaces.Models.IBucketName bucket = CurrentUserProvider.TryExtractBucket( currentFolderPath );
            if ( !bucket.IsSuccess )
            {
                throw new IntegrationTestException( bucket.ErrorMessage );
            }

            String serverBucketName = bucket.ServerName;
            String serverPrefix;
            currentFolderPath = buckets.First();   // back to bucket folder
            String prefixCombined;
            String[] dirs = Directory.GetDirectories( currentFolderPath, "*", SearchOption.AllDirectories );
            await Task.Delay( 15000 );
            foreach ( String dir in dirs )
            {
                treeFolderName = new DirectoryInfo( dir ).Name;
                serverPrefix = CurrentUserProvider.ExtractPrefix( dir );
                Interfaces.OutputContracts.ObjectsListResponse result = await ApiClient.ListAsync( serverBucketName, serverPrefix );

                prefixCombined = serverPrefix.ToLowerInvariant() + treeFolderName.ToHexPrefix().ToLowerInvariant();
                Assert.IsTrue( result.Directories.Where( x => x.IsDeleted is false ).Any( x => x.HexPrefix == prefixCombined ) );
            }


        }

        [Test, Order( 403 )]
        public async Task RenameTreeDirectoryTest()
        {
            System.Collections.Generic.IEnumerable<String> buckets = Directory.EnumerateDirectories( m_testRootFolderPath );
            String currentFolderPath = buckets.First();
            String treeFolderName;

            for ( Int32 i = 1; i <= 10; i++ )
            {
                treeFolderName = m_testFolderName + i; // create folder name
                currentFolderPath = Path.Combine( currentFolderPath, treeFolderName ); // create full path to folder

                if ( !Directory.Exists( currentFolderPath ) )
                {
                    _ = Directory.CreateDirectory( currentFolderPath );
                }
            }

            await Task.Delay( 10000 );

            String pathWithRenamedFolder = buckets.First();
            String newFolderName = "RenamedFolder";
            String oldFolderPath = buckets.First();
            for ( Int32 i = 1; i <= 10; i++ )
            {
                treeFolderName = m_testFolderName + i; // create folder name
                newFolderName += i;
                pathWithRenamedFolder = Path.Combine( pathWithRenamedFolder, newFolderName ); // create full path with renamed folder
                oldFolderPath = Path.Combine( oldFolderPath, treeFolderName ); // create full path to folder

                Directory.Move( oldFolderPath, pathWithRenamedFolder );
                oldFolderPath = pathWithRenamedFolder;
            }

            Interfaces.Models.IBucketName bucket = CurrentUserProvider.TryExtractBucket( pathWithRenamedFolder );
            if ( !bucket.IsSuccess )
            {
                throw new IntegrationTestException( bucket.ErrorMessage );
            }

            String serverBucketName = bucket.ServerName;
            String serverPrefix;
            pathWithRenamedFolder = buckets.First(); // back to bucket folder
            String prefixCombined;

            await Task.Delay( 30000 );

            String[] dirs = Directory.GetDirectories( pathWithRenamedFolder, "*", SearchOption.AllDirectories );
            foreach ( String dir in dirs )
            {
                newFolderName = new DirectoryInfo( dir ).Name;
                pathWithRenamedFolder = Path.Combine( pathWithRenamedFolder, newFolderName ); // create full path with renamed folder
                serverPrefix = CurrentUserProvider.ExtractPrefix( pathWithRenamedFolder );
                Interfaces.OutputContracts.ObjectsListResponse result = await ApiClient.ListAsync( serverBucketName, serverPrefix );
                await Task.Delay( 1000 );
                prefixCombined = serverPrefix.ToLowerInvariant() + newFolderName.ToHexPrefix().ToLowerInvariant();
                Assert.IsTrue( result.Directories.Where( x => x.IsDeleted is false ).Any( x => x.HexPrefix == prefixCombined ) );
            }
        }

        [Test, Order( 404 )]
        public async Task MoveTreeDirectoryTest()  // Takes top directory and move it to bucket 
        {
            System.Collections.Generic.IEnumerable<String> buckets = Directory.EnumerateDirectories( m_testRootFolderPath );
            System.Collections.Generic.List<Tuple<String, String>> listOfTuples = GetPathPairs();
            foreach ( Tuple<String, String> pathes in listOfTuples )
            {
                if ( pathes.Item1 != pathes.Item2 )//TODO O Move this condition into method MovingPathPair (We can't move this into method MovingPathPair until logic changed for foreach assertion)
                {
                    try
                    {
                        Directory.Move( pathes.Item1, pathes.Item2 );
                    }
                    catch ( Exception exx )
                    {
                        throw exx;
                    }
                }

                await Task.Delay( 100 );
            }

            await Task.Delay( 20000 );

            System.Collections.Generic.IList<String> bucketDirectoryPathes = CurrentUserProvider.ProvideBucketDirectoryPaths();

            Interfaces.Models.IBucketName bucket = CurrentUserProvider.TryExtractBucket( buckets.First() );
            if ( !bucket.IsSuccess )
            {
                throw new IntegrationTestException( bucket.ErrorMessage );
            }

            String serverBucketName = bucket.ServerName;
            String serverPrefix = CurrentUserProvider.ExtractPrefix( buckets.First() );
            Interfaces.OutputContracts.ObjectsListResponse result = await ApiClient.ListAsync( serverBucketName, serverPrefix );  // TODO O Reverse result - why do we need reverse? (List from server return first folder as first item but tuple returns last folder - first)
            String checkFolderName;
            foreach ( Tuple<String, String> pathes in listOfTuples.AsEnumerable().Reverse() )
            {
                checkFolderName = new DirectoryInfo( pathes.Item1 ).Name;
                Assert.IsTrue( result.Directories.Where( x => x.IsDeleted is false ).Any( x => x.HexPrefix == checkFolderName.ToHexPrefix().ToLowerInvariant() ) );
            }
        }
    }
}
