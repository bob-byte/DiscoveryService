using System;
using System.IO;
using LightClientLibrary;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LightClientTests
{
    [TestFixture]
    public class Tests
    {
        private const String DiffUploadFile = @"E:\LightIntegrationTests\integration1\diffuploadtest.txt";
        private readonly LightClient _lightClient = new LightClient();

        [Test]
        [Order(1)]
        public void DiffUploadTest()
        {
            var host = "http://lightup.cloud";
            var response = _lightClient.LoginAsync("integration1", "integration1", host).Result;

            Assert.IsTrue(response.IsSuccessStatusCode);

            var str = response.Content.ReadAsStringAsync().Result;
            var responseValues = JsonConvert.DeserializeObject<LoginResponse>(str);

            if (File.Exists(DiffUploadFile)) File.Delete(DiffUploadFile);

            //Create a new file, 30MB
            var fs = new FileStream(DiffUploadFile, FileMode.CreateNew);
            fs.Seek(30L * 1024 * 1024, SeekOrigin.Begin);
            fs.WriteByte(0);
            fs.Close();

            //fill the file with random bytes (0-100)
            Byte[] bytes = File.ReadAllBytes(DiffUploadFile);
            for (Int32 i = 0; i < bytes.Length; i++) bytes[i] = (Byte)(DateTime.Now.Ticks % 100);
            File.WriteAllBytes(DiffUploadFile, bytes);

            var startUpload = DateTime.Now.Ticks / 1000000;  //set time precision to 0.1 seconds
            var uploadResponse = _lightClient.Upload(host, responseValues?.Token, responseValues?.Id,
                responseValues?.Groups[0].BucketId, DiffUploadFile, "").Result;

            var durationUpload = DateTime.Now.Ticks / 1000000 - startUpload;

            Assert.IsTrue(uploadResponse.IsSuccessStatusCode);

            str = uploadResponse.Content.ReadAsStringAsync().Result;
            var uploadResponseValues = JsonConvert.DeserializeObject<FileUploadResponse>(str);

            //Change a first and last bytes of diff upload file
            bytes[0] = 111;
            bytes[bytes.Length - 1] = 111;
            File.WriteAllBytes(DiffUploadFile, bytes);

            var newUpload = DateTime.Now.Ticks / 1000000;
            var newUploadResponse = _lightClient.Upload(host, responseValues.Token, responseValues.Id,
                responseValues.Groups[0].BucketId, DiffUploadFile, "", uploadResponseValues?.Version).Result;
            var newDurationUpload = DateTime.Now.Ticks / 1000000 - newUpload;

            Assert.IsTrue(newDurationUpload < durationUpload); //this upload should be a faster than first
            Assert.IsTrue(newUploadResponse.IsSuccessStatusCode);
        }
    }
}