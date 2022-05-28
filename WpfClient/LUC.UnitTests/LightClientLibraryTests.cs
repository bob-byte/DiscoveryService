using System;
using System.IO;
using System.Net.Http;

using LightClientLibrary;

using Newtonsoft.Json;

using NUnit.Framework;

namespace LightClientTests
{
    [TestFixture]
    public class Tests
    {
        private const String DIFF_UPLOAD_FILE = @"E:\LightIntegrationTests\integration1\diffuploadtest.txt";
        private readonly LightClient m_lightClient = new LightClient();
        readonly String m_host = "https://lightup.cloud";

        [Test]
        [Order( 1 )]
        public void DiffUploadTest()
        {
            HttpResponseMessage response = m_lightClient.LoginAsync( "integration2", "integration2", m_host ).Result;

            Assert.IsTrue( response.IsSuccessStatusCode );

            String str = response.Content.ReadAsStringAsync().Result;
            LoginResponse responseValues = JsonConvert.DeserializeObject<LoginResponse>( str );

            if ( File.Exists( DIFF_UPLOAD_FILE ) )
            {
                File.Delete( DIFF_UPLOAD_FILE );
            }

            //Create a new file, 30MB
            var fs = new FileStream( DIFF_UPLOAD_FILE, FileMode.CreateNew );
            fs.Seek( 30L * 1024 * 1024, SeekOrigin.Begin );
            fs.WriteByte( 0 );
            fs.Close();

            //fill the file with random bytes (0-100)
            Byte[] bytes = File.ReadAllBytes( DIFF_UPLOAD_FILE );
            for ( Int32 i = 0; i < bytes.Length; i++ )
            {
                bytes[ i ] = (Byte)( DateTime.Now.Ticks % 100 );
            }

            File.WriteAllBytes( DIFF_UPLOAD_FILE, bytes );

            Int64 startUpload = DateTime.Now.Ticks / 1000000;  //set time precision to 0.1 seconds
            HttpResponseMessage uploadResponse = m_lightClient.Upload( m_host, responseValues?.Token, responseValues?.Id,
                responseValues?.Groups[ 0 ].BucketId, DIFF_UPLOAD_FILE, "" ).Result;

            Int64 durationUpload = (DateTime.Now.Ticks / 1000000) - startUpload;

            Assert.IsTrue( uploadResponse.IsSuccessStatusCode );

            str = uploadResponse.Content.ReadAsStringAsync().Result;
            FileUploadResponse uploadResponseValues = JsonConvert.DeserializeObject<FileUploadResponse>( str );

            //Change a first and last bytes of diff upload file
            bytes[ 0 ] = 111;
            bytes[ bytes.Length - 1 ] = 111;
            File.WriteAllBytes( DIFF_UPLOAD_FILE, bytes );

            Int64 newUpload = DateTime.Now.Ticks / 1000000;
            HttpResponseMessage newUploadResponse = m_lightClient.Upload( m_host, responseValues.Token, responseValues.Id,
                responseValues.Groups[ 0 ].BucketId, DIFF_UPLOAD_FILE, "", uploadResponseValues?.Version ).Result;
            Int64 newDurationUpload = (DateTime.Now.Ticks / 1000000) - newUpload;

            Assert.IsTrue( newDurationUpload < durationUpload, "First upload " + durationUpload + ", second upload " + newDurationUpload ); //this upload should be a faster than first
            Assert.IsTrue( newUploadResponse.IsSuccessStatusCode );
        }

        [Test]
        [Order( 2 )]
        public void FullChunkUploadTest()
        {
            HttpResponseMessage response = m_lightClient.LoginAsync( "integration2", "integration2", m_host ).Result;

            Assert.IsTrue( response.IsSuccessStatusCode );

            String str = response.Content.ReadAsStringAsync().Result;
            LoginResponse responseValues = JsonConvert.DeserializeObject<LoginResponse>( str );

            if ( File.Exists( DIFF_UPLOAD_FILE ) )
            {
                File.Delete( DIFF_UPLOAD_FILE );
            }

            //Create a new file, 4000000 B
            var fs = new FileStream( DIFF_UPLOAD_FILE, FileMode.CreateNew );
            fs.Seek( 4L * 1000 * 1000, SeekOrigin.Begin );
            fs.WriteByte( 0 );
            fs.Close();

            HttpResponseMessage uploadResponse = m_lightClient.Upload( m_host, responseValues?.Token, responseValues?.Id,
                responseValues?.Groups[ 0 ].BucketId, DIFF_UPLOAD_FILE, "" ).Result;

            Assert.IsTrue( uploadResponse.IsSuccessStatusCode );
        }
    }
}