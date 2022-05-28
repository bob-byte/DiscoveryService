using Common.Exceptions;

using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;

using NUnit.Framework;

using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LUC.IntegrationTests
{
    public partial class OnlineOneUserOneActionIntegrationTests
    {
        public System.String[] fileNames = { "User2LockedUpdate.txt", "User2LockedCopy.txt", "User2LockedRename.txt", "User2LockedMove.txt" };
        private static LoginServiceModel s_originalyLoggedUser;

        public async Task TryToChangeLockStateAndAssert( System.String fileName, System.String filePath, AdsLockState state )
        {
            AdsExtensions.WriteLockDescription( filePath, new LockDescription( state ) );
            ILockDescription localLockResult = AdsExtensions.ReadLockDescription( filePath );
            Assert.IsTrue( localLockResult.LockState == state );
            await Task.Delay( 5000 );
            Interfaces.OutputContracts.ObjectsListResponse list = await GetApiClientList( filePath, 0 );
            Assert.IsTrue( list.ObjectFileDescriptions.SingleOrDefault( x =>
              {
                  return x.OriginalName == fileName
                         && x.IsLocked
                         && x.LockUserName == s_originalyLoggedUser.Login
                         && x.LockUserId == s_originalyLoggedUser.Id;
              } ) != null, "File doesn't locked or user name/user id is wrong" );
            localLockResult = AdsExtensions.ReadLockDescription( filePath );
            Assert.IsTrue( localLockResult.LockUserName == s_originalyLoggedUser.Login &&
                          localLockResult.LockUserId == s_originalyLoggedUser.Id &&
                          localLockResult.LockState == AdsLockState.LockedOnServer );
        }

        [Test, Order( 500 )]
        public async Task TwoUserWithLockTest()  // Create a file, lock by one user, try all operations with another user 
        {
            s_originalyLoggedUser = await LoginAs( "integration1", "integration1" );
            System.String filePath;
            foreach ( System.String name in fileNames )
            {
                filePath = Path.Combine( m_bucketPathList[ 0 ], name );
                if ( !File.Exists( filePath ) )
                {
                    File.WriteAllText( filePath, m_loremIpsum );
                    await Task.Delay( 5000 * DELAY_KOEFF );
                    AdsExtensions.WriteLockDescription( filePath, new LockDescription( AdsLockState.ReadyToLock ) ); // LOCK
                    //await ApiClient.LockFile(filePath); // TODO RR Check how it works.
                    await Task.Delay( 5000 * DELAY_KOEFF );
                    Interfaces.OutputContracts.ObjectsListResponse list = await GetApiClientList( filePath, 0 );
                    Assert.IsTrue( list.ObjectFileDescriptions.SingleOrDefault( x =>
                      {
                          return x.OriginalName == name
                                 && x.IsLocked
                                 && x.LockUserId == s_originalyLoggedUser.Id;
                      } ) != null, "File doesn't locked or user name/user id is wrong" );
                }
                // TODO RR This files should be created via API. Probably move the method to InitTestMethod. Then this files shoould be downloaded from server to client. Just add delay for it.
            }
            // -= Change User =- 
            m_loggedUser = await LoginAs( "integration2", "integration2" );
            System.String fileName = fileNames[ 1 ];
            filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
            File.WriteAllText( filePath, m_loremIpsum );
            // -= TRY COPY =-
            try
            {
                System.String copiedFileName = "Copied" + fileName;
                System.String whereToCopyPath = Path.Combine( m_bucketPathList[ 0 ], copiedFileName );
                System.DateTime timeBeforeReplace = File.GetLastWriteTimeUtc( filePath );
                //FileCoping(false, filePath, whereToCopyPath);
                //await Task.Delay(7000 * DelayKoeff);
                //var list = await GetApiClientList(filePath, 0);
                //AssertAny(list, fileName, timeBeforeReplace);
                //list = await GetApiClientList(whereToCopyPath, 0);
                //Assert.IsFalse(list.ObjectFileDescriptions.Any(x =>
                //{
                //    return x.OriginalName == copiedFileName;
                //}), $"There is {copiedFileName} in destenation folder");
            }
            catch ( IntegrationTestException integrationEx )
            {
                throw integrationEx;
            }

            fileName = fileNames[ 0 ];
            filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
            // -= TRY UPDATE =- 
            try
            {
                System.DateTime timeBeforeReplace = File.GetLastWriteTimeUtc( filePath );
                FileReplacing( false, filePath, "12345" );                                      //TODO RR change UI for integration tests/ Do not show messageBox.
                await Task.Delay( 15000 * DELAY_KOEFF );
                Interfaces.OutputContracts.ObjectsListResponse list = await GetApiClientList( filePath, 0 );
                AssertAny( list, fileName, timeBeforeReplace );
            }
            catch ( IntegrationTestException integrationEx )
            {
                throw integrationEx;
            }

            fileName = fileNames[ 2 ];
            // -= TRY RENAME =-
            try
            {
                System.String renamedFileName = "Renamed" + fileName;
                System.String howToRenamePath = Path.Combine( m_bucketPathList[ 0 ], renamedFileName );
                FileRenaming( false, fileName, howToRenamePath );
                await Task.Delay( 15000 * DELAY_KOEFF );
                Interfaces.OutputContracts.ObjectsListResponse list = await GetApiClientList( howToRenamePath, 0 );
                Assert.IsTrue( list.ObjectFileDescriptions.Any( x => x.OriginalName == fileName ), $"File with this name {fileName} doesn't exist" );
                Assert.IsFalse( list.ObjectFileDescriptions.Where( x => x.IsDeleted is false ).Any( x => x.OriginalName == renamedFileName ), $"File renamed to {renamedFileName} but it shouldn't" );
            }
            catch ( IntegrationTestException integrationEx )
            {
                throw integrationEx;
            }

            fileName = fileNames[ 1 ];
            filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
            // -= TRY DELETE =-
            try
            {
                System.DateTime timeBeforeReplace = File.GetLastWriteTimeUtc( filePath );
                File.Delete( filePath );
                await Task.Delay( 15000 * DELAY_KOEFF );
                Interfaces.OutputContracts.ObjectsListResponse list = await GetApiClientList( filePath, 0 );
                AssertAny( list, fileName, timeBeforeReplace );
            }
            catch ( IntegrationTestException integrationEx )
            {
                throw integrationEx;
            }

            fileName = fileNames[ 0 ];
            filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
            try
            {
                await TryToChangeLockStateAndAssert( fileName, filePath, AdsLockState.ReadyToLock );
            }
            catch ( IntegrationTestException integrationEx )
            {
                throw integrationEx;
            }

            fileName = fileNames[ 0 ];
            filePath = Path.Combine( m_bucketPathList[ 0 ], fileName );
            try
            {
                await TryToChangeLockStateAndAssert( fileName, filePath, AdsLockState.ReadyToUnlock );
            }
            catch ( IntegrationTestException integrationEx )
            {
                throw integrationEx;
            }
        }
    }
}
