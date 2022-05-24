using LightClientLibrary;

using LUC.Interfaces;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.InputContracts;
using LUC.Interfaces.Models;
using LUC.Services.Implementation;

using Moq;

using Newtonsoft.Json;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

using Prism.Events;

using Unity;
using Prism.Mef.Events;
using LUC.UnitTests;

namespace LUC.IntegrationTests
{
    [TestFixture]
    public partial class OnlineOneUserOneActionIntegrationTests
    {
        private const String NAME_OF_TEST_DIRECTORY = "LightIntegrationTests";

        //TODO: replace inititialization of file names in
        //method OnlineOneUserOneActionIntegrationTests.InitObjectPathsAndNames
        private readonly String m_smallFileName = "SmallFile.txt";
        private readonly String m_renamedSmallFileName = "RenamedSmallFile.txt";
        private readonly String m_readOnlySmallFileName = "ReadOnlySmallFile.txt";
        private readonly String m_bigFileName = "BigFile.txt";
        private readonly String m_readOnlyBigFileName = "ReadOnlyBigFile.txt";
        private readonly String m_folderToMoveName = "FolderForFile";

        private String m_testRootFolderPath;

        private String m_speedTestPath;
        private String m_fullNameOfSmallFile;
        private String m_diffUploadFullFileName;

        private List<String> m_bucketPathList;
        private List<String> m_bucketNameList;

        private const Int32 DELAY_KOEFF = 1; // Try to increase in case of slow connection 

        private Double m_uploadSpeedKbps;
        private Double m_downloadSpeedKbps;

        private readonly String m_testFolderName = "JustFolder";

        private readonly String m_loremIpsum =
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

        private LoginServiceModel m_loggedUser;
        private readonly IUnityContainer m_unityContainer = new UnityContainer();
        private readonly LightClient m_lightClient = new LightClient();

        [OneTimeSetUp]
        public async Task Init()
        {
            InitObjectPathsAndNames();

            KillAlreadyRunApp();

            m_unityContainer.Setup( m_testRootFolderPath );

            m_loggedUser = await LoginAs( "integration2", "integration2" );
            await CalculateSpeedInKbps();
            await DeleteEverythingFromServer();
            await Task.Delay( 5000 );
            PrepareFiles( m_testRootFolderPath + @"\integration1" );
            PrepareFiles( m_testRootFolderPath + @"\integration2" );
            PrepareDirectory();
            CreateLocalBuckets();
            await RunRealApplication();
        }

        private void InitObjectPathsAndNames()
        {
            String diskName = new String( CurrentAssembly.Location.Take( count: 3 ).ToArray() );

            m_testRootFolderPath = Path.Combine( diskName, NAME_OF_TEST_DIRECTORY );
            Directory.CreateDirectory( m_testRootFolderPath );

            //init bucket names
            m_bucketNameList = new List<String>
            {
                "integration1",
                "integration2"
            };

            //init bucket pathes
            m_bucketPathList = new List<String>();

            foreach (String bucket in m_bucketNameList)
            {
                String bucketPath = Path.Combine( m_testRootFolderPath, bucket );
                m_bucketPathList.Add( bucketPath );
            }

            //init file pathes
            m_speedTestPath = Path.Combine( m_testRootFolderPath, m_bucketNameList[ 0 ], "SpeedTest.txt" );
            m_fullNameOfSmallFile = Path.Combine( m_testRootFolderPath, m_bucketNameList[ 0 ], m_smallFileName );
            m_diffUploadFullFileName = Path.Combine( m_testRootFolderPath, m_bucketNameList[ 0 ], "diffuploadtest.txt" );
        }

        private Assembly CurrentAssembly =>
            Assembly.GetAssembly( typeof( OnlineOneUserOneActionIntegrationTests ) );

        private void KillAlreadyRunApp()
        {
            foreach ( Process p in Process.GetProcessesByName( "LUC.WpfClient" ) )
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(); // possibly with a timeout
                }
                catch ( Win32Exception )
                {
                    // process was terminating or can't be terminated - deal with it
                }
                catch ( InvalidOperationException )
                {
                    // process has already exited - might be able to let this one go
                }
            }
        }

        private void PrepareDirectory()
        {
            DirectoryInfo di;

            if ( Directory.Exists( m_testRootFolderPath ) )
            {
                di = new DirectoryInfo( m_testRootFolderPath + @"\integration1" );
                di.Attributes &= ~FileAttributes.ReadOnly;
                Directory.Delete( m_testRootFolderPath + @"\integration1", true );

                di = new DirectoryInfo( m_testRootFolderPath + @"\integration2" );
                di.Attributes &= ~FileAttributes.ReadOnly;
                Directory.Delete( m_testRootFolderPath + @"\integration2", true );
            }
            else
            {
                _ = Directory.CreateDirectory( m_testRootFolderPath );
            }
        }

        private static void PrepareFiles( String targetDirectory )
        {
            if ( !Directory.Exists( targetDirectory ) )
            {
                return;
            }

            // Process the list of files found in the directory.
            String[] fileEntries = Directory.GetFiles( targetDirectory );
            foreach ( String fileName in fileEntries )
            {
                File.SetAttributes( fileName, FileAttributes.Normal );
            }

            // Recurse into subdirectories of this directory.
            String[] subdirectoryEntries = Directory.GetDirectories( targetDirectory );
            foreach ( String subdirectory in subdirectoryEntries )
            {
                PrepareFiles( subdirectory );
            }
        }

        private ICurrentUserProvider m_currentUserProvider;

        private ICurrentUserProvider CurrentUserProvider =>
            m_currentUserProvider ??
            ( m_currentUserProvider = m_unityContainer.Resolve<ICurrentUserProvider>() );

        private IApiClient m_apiClient;

        private IApiClient ApiClient => m_apiClient ?? ( m_apiClient = m_unityContainer.Resolve<IApiClient>() );

        private async Task<LoginServiceModel> LoginAs( String login, String pass )
        {
            _ = await ApiClient.LogoutAsync();
            _ = await ApiClient.LoginAsync( login, pass );

            CurrentUserProvider.RootFolderPath = m_testRootFolderPath;

            return CurrentUserProvider.LoggedUser;
        }

        private void CreateLocalBuckets()
        {
            IList<String> bucketDirectoryPaths = CurrentUserProvider.ProvideBucketDirectoryPaths();

            foreach ( String bucket in bucketDirectoryPaths )
            {
                if ( !Directory.Exists( bucket ) )
                {
                    _ = Directory.CreateDirectory( bucket );
                }
            }
        }

        private async Task RunRealApplication()
        {
            //get index of name of current assembly in LocationOfCurrentAssembly
            Int32 indexOfAssembly = CurrentAssembly.Location.IndexOf( CurrentAssembly.GetName().Name, StringComparison.Ordinal );

            //substring to this index
            String pathToWpfClient = CurrentAssembly.Location.Substring( startIndex: 0, indexOfAssembly );

            String expectedAppPath = Path.Combine( pathToWpfClient, @"LUC.WpfClient\bin\Debug\LUC.WpfClient.exe");

            if ( File.Exists( expectedAppPath ) )
            {
                var processInfo = new ProcessStartInfo
                {
                    WorkingDirectory = Path.GetDirectoryName( expectedAppPath ) ?? throw new InvalidOperationException(),
                    FileName = expectedAppPath,
                    ErrorDialog = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                Process.Start( processInfo );
            }

            await Task.Delay( 10000 );
        }

        public async Task CalculateSpeedInKbps()
        {
            var timeToUd = new Stopwatch();
            Int64 upDownLoadMils;

            if ( File.Exists( m_speedTestPath ) )
            {
                File.Delete( m_speedTestPath );
            }

            var fs = new FileStream( m_speedTestPath, FileMode.CreateNew );
            _ = fs.Seek( 3L * 1024 * 1024, SeekOrigin.Begin );
            fs.WriteByte( 065 );
            fs.Close();

            var fileInfo = new FileInfo( m_speedTestPath );
            timeToUd.Start();
            Interfaces.OutputContracts.FileUploadResponse uploadResult = await ApiClient.TryUploadAsync( fileInfo );

            if ( !uploadResult.IsSuccess )
            {
                throw new FileLoadException();
            }

            timeToUd.Stop();
            upDownLoadMils = timeToUd.ElapsedMilliseconds;
            m_uploadSpeedKbps = fileInfo.Length / 1000000d / ( upDownLoadMils / 1000d );

            IList<String> bucketDirectoryPaths = CurrentUserProvider.ProvideBucketDirectoryPaths();
            String serverBucketName = CurrentUserProvider.GetBucketNameByDirectoryPath( bucketDirectoryPaths.ElementAt( 0 ) )
                .ServerName;

            String serverPrefix = CurrentUserProvider.ExtractPrefix( m_speedTestPath );
            Interfaces.OutputContracts.ObjectsListResponse resultList = await ApiClient.ListAsync( serverBucketName, serverPrefix );
            Interfaces.OutputContracts.ObjectFileDescriptionSubResponse item = resultList.ObjectFileDescriptions.Single( x => x.OriginalName == fileInfo.Name );

            timeToUd.Restart();
            await ApiClient.DownloadFileAsync( serverBucketName, serverPrefix, bucketDirectoryPaths.ElementAt( 0 ),
                "downloaded" + fileInfo.Name, item.ToObjectDescriptionModel() );

            timeToUd.Stop();
            upDownLoadMils = timeToUd.ElapsedMilliseconds;
            m_downloadSpeedKbps = fileInfo.Length / 1000000d / ( upDownLoadMils / 1000d );
        }

        public async Task DeleteEverythingFromServer()
        {
            if ( Directory.Exists( m_testRootFolderPath ) )
            {
                if ( !Directory.Exists( m_testRootFolderPath + @"\integration1" ) )
                {
                    Directory.CreateDirectory( m_testRootFolderPath + @"\integration1" );
                }

                if ( !Directory.Exists( m_testRootFolderPath + @"\integration2" ) )
                {
                    Directory.CreateDirectory( m_testRootFolderPath + @"\integration2" );
                }

                IEnumerable<String> buckets1 = Directory.EnumerateDirectories( m_testRootFolderPath + @"\integration1" );
                IEnumerable<String> buckets2 = Directory.EnumerateDirectories( m_testRootFolderPath + @"\integration2" );

                foreach ( String bucket in buckets2 )
                {
                    _ = buckets1.Append( bucket );
                }

                foreach ( String bucket in buckets1 )
                {
                    IEnumerable<String> curDirs = Directory.EnumerateDirectories( bucket );

                    if ( curDirs.Any() )
                    {
                        Interfaces.OutputContracts.DeleteResponse response = await ApiClient.DeleteAsync( curDirs.ToArray() );

                        if ( !response.IsSuccess )
                        {
                            throw new InvalidOperationException( "Data from server was not deleted." );
                        }
                    }

                    IEnumerable<String> curFiles = Directory.EnumerateFiles( bucket );

                    if ( curFiles.Any() )
                    {
                        Interfaces.OutputContracts.DeleteResponse response = await ApiClient.DeleteAsync( curFiles.ToArray() );

                        if ( !response.IsSuccess )
                        {
                            throw new InvalidOperationException( "Data from server was not deleted." );
                        }
                    }
                }

                await ReloginAndDeleteEverything( "integration1" ); // Delete Everything created by integration1 user 
                await ReloginAndDeleteEverything( "integration3" ); // Delete Everything created by integration3 user 
                m_loggedUser = await LoginAs( "integration2", "integration2" ); // Change user back to integration2 user
            }
        }

        public async Task ReloginAndDeleteEverything( String changeLoginFor )
        {
            m_loggedUser = await LoginAs( changeLoginFor, changeLoginFor );
            IList<String> bucketList = CurrentUserProvider.GetServerBuckets();

            foreach ( String bucket in bucketList )
            {
                await DeleteDirsAndFilesByLoggedUser( bucket );
            }
        }

        public async Task DeleteDirsAndFilesByLoggedUser( String bucket )
        {
            Interfaces.OutputContracts.ObjectsListResponse data = await ApiClient.ListAsync( bucket, String.Empty, true );

            var objectsKeys = new List<String>();

            var directoriesKeys = data.Directories.Where( x => x.IsDeleted is false )
                .Select( x => x.HexPrefix.LastHexPrefixPart() + '/' ).ToList();

            if ( directoriesKeys.Any() )
            {
                objectsKeys.AddRange( directoriesKeys );
            }

            IEnumerable<String> filesKeys = data.ObjectFileDescriptions.Where( x => x.IsDeleted is false ).Select( x => x.ObjectKey );

            if ( filesKeys.Any() )
            {
                objectsKeys.AddRange( filesKeys );
            }

            if ( objectsKeys.Any() )
            {
                var request = new DeleteRequest
                {
                    ObjectKeys = objectsKeys,
                    Prefix = String.Empty
                };

                HttpResponseMessage result = await ApiClient.DeleteAsync( request, bucket );

                if ( !result.IsSuccessStatusCode )
                {
                    throw new Exception( $"Can't delete from server. Status code = {result.StatusCode}" );
                }
            }
        }

        private List<Tuple<String, String>> GetPathPairs()
        {
            IEnumerable<String> buckets = Directory.EnumerateDirectories( m_testRootFolderPath );
            String bucketPath = buckets.First();
            String folderToMovePath;
            String whereToMovePath;
            String dirToMoveName;
            var pathList = new List<Tuple<String, String>>();
            Tuple<String, String> result;

            String[] dirs = Directory.GetDirectories( bucketPath, "*", SearchOption.AllDirectories );

            foreach ( String dir in dirs.Reverse() )
            {
                folderToMovePath = dir; // Top directory path 
                dirToMoveName = new DirectoryInfo( dir ).Name; // Top directory name
                whereToMovePath = Path.Combine( bucketPath, dirToMoveName ); // Creating path to move top directory

                result = new Tuple<String, String>( folderToMovePath, whereToMovePath ); // Creating pair of path to move
                pathList.Add( result ); // Creating list of tuple pairs
            }

            return pathList;
        }

        [Test]
        [Order( 1 )]
        public async Task A0_CheckIsServerEmpty()
        {
            IList<String> bucketDirectoryPaths = CurrentUserProvider.ProvideBucketDirectoryPaths();

            foreach ( String path in bucketDirectoryPaths )
            {
                String serverBucketName = CurrentUserProvider.GetBucketNameByDirectoryPath( path ).ServerName;
                Interfaces.OutputContracts.ObjectsListResponse result = await ApiClient.ListAsync( serverBucketName, String.Empty );
                Assert.IsFalse( result.Directories.Any( x => x.IsDeleted is false ) );
                Assert.IsFalse( result.ObjectFileDescriptions.Any( x => x.IsDeleted is false ) );
            }
        }

        [Test]
        [Order( 2000 )]
        async public Task DiffUploadTest()
        {
            KillAlreadyRunApp();
            String host = "http://lightup.cloud";
            HttpResponseMessage response = await m_lightClient.LoginAsync( "integration1", "integration1", host );

            Assert.IsTrue( response.IsSuccessStatusCode );

            String str = await response.Content.ReadAsStringAsync();
            LoginResponse responseValues = JsonConvert.DeserializeObject<LoginResponse>( str );

            if ( File.Exists( m_diffUploadFullFileName ) )
            {
                File.Delete( m_diffUploadFullFileName );
            }

            //Create a new file, 30MB
            var fs = new FileStream( m_diffUploadFullFileName, FileMode.CreateNew );
            _ = fs.Seek( 30L * 1024 * 1024, SeekOrigin.Begin );
            fs.WriteByte( 0 );
            fs.Close();

            //fill the file with random bytes (0-100)
            Byte[] bytes = File.ReadAllBytes( m_diffUploadFullFileName );
            for ( Int32 i = 0; i < bytes.Length; i++ )
            {
                bytes[ i ] = (Byte)( DateTime.Now.Ticks % 100 );
            }

            File.WriteAllBytes( m_diffUploadFullFileName, bytes );

            Int64 startUpload = DateTime.Now.Ticks / 1000000;  //set time precision to 0.1 seconds

            HttpResponseMessage uploadResponse = await m_lightClient.Upload( host, responseValues.Token, responseValues.Id,
                responseValues.Groups[ 0 ].BucketId, m_diffUploadFullFileName, "" );

            Int64 durationUpload = ( DateTime.Now.Ticks / 1000000 ) - startUpload;

            Assert.IsTrue( uploadResponse.IsSuccessStatusCode );

            str = await uploadResponse.Content.ReadAsStringAsync();
            FileUploadResponse uploadResponseValues = JsonConvert.DeserializeObject<FileUploadResponse>( str );

            //Change a first and last bytes of diff upload file
            bytes[ 0 ] = 111;
            bytes[ bytes.Length - 1 ] = 111;
            File.WriteAllBytes( m_diffUploadFullFileName, bytes );

            Int64 newUpload = DateTime.Now.Ticks / 1000000;
            HttpResponseMessage newUploadResponse = await m_lightClient.Upload( host, responseValues.Token, responseValues.Id,
                responseValues.Groups[ 0 ].BucketId, m_diffUploadFullFileName, "", uploadResponseValues.Version );
            Int64 newDurationUpload = ( DateTime.Now.Ticks / 1000000 ) - newUpload;

            Assert.IsTrue( newDurationUpload < durationUpload ); //this upload should be a faster than first
            Assert.IsTrue( newUploadResponse.IsSuccessStatusCode );
            _ = RunRealApplication();
        }
    }
}
