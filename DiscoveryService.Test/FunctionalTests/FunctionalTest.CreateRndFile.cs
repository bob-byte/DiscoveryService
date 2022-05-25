using System;
using System.IO;

using LUC.DiscoveryServices.Test.Extensions;
using LUC.Interfaces.Constants;

namespace LUC.DiscoveryServices.Test.FunctionalTests
{
    partial class FunctionalTest
    {
        private const Int32 BYTES_IN_ONE_MB = 1024 * 1024;

        private static void CreateFileWithRndBytes()
        {
            Int16 mbCount = UserIntersectionInConsole.ValidValueInputtedByUser( 
                requestToUser: "Input count MBs: ", 
                convert: ( userInput ) =>
                {
                    Int16 value;
                    try
                    {
                        value = checked(Convert.ToInt16( userInput ));
                    }
                    catch ( OverflowException )
                    {
                        Console.WriteLine( "Too big size" );
                        throw;
                    }
                
                    return value;
                },
                tryPredicateUserInput: ( value ) =>
                {
                    Boolean isValidInput = value > 0;
                    return isValidInput;
                }
            );

            //request path where we need to create file from root directory
            String pathToRootFolder = s_settingsService.ReadUserRootFolderPath();
            String requestedFullFileNameFromRootFolder = UserIntersectionInConsole.ValidValueInputtedByUser( $"Input path from {pathToRootFolder} and file name with extension which you want to create: ", ( userInput ) =>
            {
                String pathSeparator = LUC.Interfaces.Extensions.PathExtensions.PATH_SEPARATOR;
                if ( userInput.StartsWith( pathSeparator ) )
                {
                    userInput = userInput.TrimStart( pathSeparator.ToCharArray() );
                }

                String fullFileName = Path.Combine( pathToRootFolder, userInput );

                Boolean isValidFullFileName = PathExtensions.IsValidFullFileName( fullFileName );
                Boolean fileAlreadyExist = File.Exists( fullFileName );

                return isValidFullFileName && ( !fileAlreadyExist );
            } );

            String requestedFullFileName = Path.Combine( pathToRootFolder, requestedFullFileNameFromRootFolder );
            Int64 bytesCount = mbCount * (Int64)BYTES_IN_ONE_MB;

            lock ( UserIntersectionInConsole.Lock )
            {
                WriteRndFile( bytesCount, requestedFullFileName );

                Console.WriteLine( $"File {requestedFullFileName} successfully created" );
            }
        }

        private static void WriteRndFile( Int64 bytesCount, String fullFileName )
        {
            var random = new Random();
            using ( FileStream writer = File.OpenWrite( fullFileName ) )
            {
                Byte[] buffer = new Byte[ bytesCount ];
                random.NextBytes( buffer );

                Int32 chunkSize = bytesCount < DsConstants.MAX_CHUNK_SIZE ? (Int32)bytesCount : DsConstants.MAX_CHUNK_SIZE;
                for ( Int32 offset = 0; offset + chunkSize <= bytesCount && chunkSize > 0; )
                {
                    writer.Write( buffer, offset, chunkSize );
                    offset += chunkSize;

                    if ( offset + chunkSize > bytesCount )
                    {
                        chunkSize = (Int32)bytesCount - offset;
                    }
                }
            }
        }
    }
}
