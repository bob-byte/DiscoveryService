using AutoFixture;

using FluentAssertions;

using LUC.Interfaces.Enums;
using LUC.Interfaces.Models;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.UnitTests
{
    public class ServerObjectDescriptionTest
    {
        [Test]
        public void CompareFileOnServerAndLocal_VersionIsEmptyFileDoesntExistOnLocalPc_ComparationResultShouldBeDoesntExistLocallyAndDoesntExistOnServer()
        {
            var serverObjectDescr = SetUpTests.Fixture.Create<ServerObjectDescription>();
            serverObjectDescr.IsSuccess = true;
            serverObjectDescr.Version = String.Empty;

            //create random file name(it path is D:\\)
            String rndFileName = SetUpTests.Fixture.Create<String>();
            String fullFileName = $"D:\\{rndFileName}.txt";
            var fileInfo = new FileInfo( fullFileName );

            serverObjectDescr.CompareFileOnServerAndLocal( fileInfo, whetherCompareMd5: false, out ComparationLocalAndServerFileResult comparationResult );

            comparationResult.Should().Be( ComparationLocalAndServerFileResult.DoesntExistLocally | ComparationLocalAndServerFileResult.DoesntExistOnServer );
        }
    }
}
