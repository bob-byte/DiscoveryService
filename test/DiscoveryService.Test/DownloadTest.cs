using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AutoFixture;
using AutoFixture.Kernel;

using FluentAssertions;

using LUC.DiscoveryService.Common;

using NUnit.Framework;

namespace LUC.DiscoveryService.Test
{
    [TestFixture]
    class DownloadTest
    {
        //public class PathBuilder : ISpecimenBuilder
        //{
        //    public Object Create(Object request, ISpecimenContext context)
        //    {

        //    }
        //}

        //class PathCustomization : ICustomization
        //{
        //    public void Customize(IFixture fixture)
        //    {
                
        //    }

        //}

        //[Test]
        //public void A()
        //{
        //    //create 6 MB file
        //    Random random = new Random();

        //    Int32 chunkCount = 6;
        //    Int32 bytesCount = Constants.MAX_CHUNK_SIZE * chunkCount;
        //    Byte[] fileBytes = new Byte[bytesCount];

        //    random.NextBytes( fileBytes );
        //    String rndFullFileName = Path.GetTempFileName();
        //    using (FileStream fileStream = File.Create(rndFullFileName))
        //    {
        //        fileStream.Write( fileBytes, offset: 0, bytesCount );

        //        Fixture specimens = new Fixture();
        //        var download = (Download)specimens.Build<Download>().With<IOBehavior>(c => IOBehavior.Asynchronous);
        //        download.DownloadFileAsync()
        //    }
        //    //Task.Run(download)
        //    //wait 0.5 s. Cancel download
        //    //check whether fila still exist
        //}
    }
}
