using LUC.Interfaces.Models;
using LUC.Services.Implementation;

using NUnit.Framework;

namespace LUC.IntegrationTests
{
    [TestFixture]
    public class UnitTest1
    {
        readonly CurrentUserProvider m_userProvider = new CurrentUserProvider();
        readonly System.String m_latinBucketDirectory = "IntegrationBucket";
        readonly System.String[] m_testPathes = new System.String[ 11 ] { @"C:\ProgramFiles\IntegrationBucket",
                                               @"C:\LightIntegrationTests\Integration1\IntegrationBucket",
                                               @"C:\A\B\C\D\E\F\G\H\I\J\K\L\M\N\O\P\IntegrationBucket",
                                               @"C:\ProgramFiles\Директорії\Тека\IntegrationBucket",
                                               @"C:\ProgramData\ЪЫЫЫ\IntegrationBucket",
                                               @"C:\目錄\目錄\IntegrationBucket",
                                               @"C:\دليل\دليل\IntegrationBucket",
                                               @"C:\éàùçü\éàùçü\IntegrationBucket",
                                               @"C:\ὁμιλῶν\ὁμιλῶν\IntegrationBucket",
                                               @"C:\तान्यहानि\तान्यहानि\IntegrationBucket",
                                               @"C:\ProgramFiles\Директорії\目錄\دليل\éàùçü\ὁμιλῶν\तान्यहानि\IntegrationBucket"};

        [Test]
        public void BucketNameTest()
        {
            System.String resultBucket;
            var loginmodel = new LoginServiceModel
            {
                TenantId = "TenantId"
            };
            loginmodel.Groups.Add( new GroupServiceModel { Id = "id", Name = "IntegrationBucket" } );
            //this.m_userProvider.SetLoggedUser( loginmodel );
            foreach ( System.String path in this.m_testPathes )
            {
                IBucketName bucketName = this.m_userProvider.GetBucketNameByDirectoryPath( path );
                resultBucket = bucketName.LocalName;
                Assert.AreEqual( this.m_latinBucketDirectory, resultBucket );
            }
        }
    }
}
