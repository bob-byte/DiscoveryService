using System;
using System.Collections.Concurrent;
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
        //    public Object Create( Object request, ISpecimenContext context )
        //    {

        //    }
        //}

        public class DsBuilder : ISpecimenBuilder
        {
            public enum Request
            {
                RandomGroups
            }

            public Object Create(Object request, ISpecimenContext context)
            {
                if(request is Request dsRequest)
                {
                    DiscoveryService ds = context.Create<DiscoveryService>();

                    if ( dsRequest == Request.RandomGroups)
                    {
                        Random random = new Random();
                        ds.GroupsSupported.TryAdd(random.RandomSymbols( count: 10 ), random.RandomSymbols( count: 10 ) );
                    }

                    return ds;
                }
                else
                {
                    return new NoSpecimen();
                }
            }
        }


        class PathCustomization : ICustomization
        {
            public void Customize( IFixture fixture )
            {

            }

        }

        [Test]
        public void A()
        {
            //create 6 MB file
            Random random = new Random();

            Int32 chunkCount = 6;
            Int32 bytesCount = Constants.MAX_CHUNK_SIZE * chunkCount;
            Byte[] fileBytes = new Byte[ bytesCount ];

            random.NextBytes( fileBytes );
            String rndFullFileName = Path.GetTempFileName();
            using ( FileStream fileStream = File.Create( rndFullFileName ) )
            {
                fileStream.Write( fileBytes, offset: 0, bytesCount );

                Fixture specimens = new Fixture();
                DsBuilder dsBuilder = new DsBuilder();
                var ds = (DiscoveryService)dsBuilder.Create(DsBuilder.Request.RandomGroups, (ISpecimenContext)specimens );
                var download = (Download)specimens.Build<Download>().With( c => IOBehavior.Asynchronous );
                
                //download.DownloadFileAsync(localFolderPath: Path.GetDirectoryName(rndFullFileName), bucketName: ds.GroupsSupported.First().)
            }
            //Task.Run(download)
            //wait 0.5 s. Cancel download
            //check whether fila still exist
        }
    }
}
