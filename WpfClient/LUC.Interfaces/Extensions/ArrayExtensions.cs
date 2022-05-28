using LUC.Interfaces.Constants;
using LUC.Interfaces.Helpers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LUC.Interfaces.Extensions
{
    public static class ArrayExtensions
    {
        /// <summary>
        /// Little endian conversion of bytes to bits.
        /// </summary>
		public static IEnumerable<Boolean> Bits( this Byte[] bytes )
        {
            IEnumerable<Boolean> Bits( Byte b )
            {
                Byte shifter = 0x01;

                for ( Int32 i = 0; i < 8; i++ )
                {
                    yield return ( b & shifter ) != 0;
                    shifter <<= 1;
                }
            }

            return bytes.SelectMany( Bits );
        }

        /// <summary>
        /// Append a 0 to the byte array so that when converting to a BigInteger, the value remains positive.
        /// </summary>
        public static Byte[] Append0( this Byte[] b ) =>
            b.Concat( new Byte[] { 0 } );

        public static T[] Concat<T>( this T[] first, params T[] second )
        {
            T[] result;

            if ( ( second != null ) && ( second.Length > 0 ) )
            {
                result = new T[ first.Length + second.Length ];
                first.CopyTo( result, index: 0 );
                second.CopyTo( result, first.Length );
            }
            else
            {
                result = first.ToArray();
            }

            return result;
        }

        public static IEnumerable<Byte[]> IterateFileChunks( String filePath )
        {
            using ( var fileStream = new FileStream( filePath, FileMode.Open, FileAccess.Read ) )
            {
                Byte[] buffer = new Byte[ UploadConstants.SINGLE_CHUNK_MAX_SIZE ];
                _ = fileStream.Seek( 0, SeekOrigin.Begin );
                Int32 bytesRead = fileStream.Read( buffer, 0, UploadConstants.SINGLE_CHUNK_MAX_SIZE );

                while ( bytesRead > 0 )
                {
                    if ( bytesRead < UploadConstants.SINGLE_CHUNK_MAX_SIZE )
                    {
                        buffer = buffer.Take( bytesRead ).ToArray();

                        yield return buffer;
                        break;
                    }

                    yield return buffer;

                    bytesRead = fileStream.Read( buffer, 0, UploadConstants.SINGLE_CHUNK_MAX_SIZE );
                }
            }
        }

        public static String CalculateTempCustomHash( String filePath, String serverMd5 )
        {
            FileInfo fileInfo = FileInfoHelper.TryGetFileInfo( filePath );

            if ( fileInfo == null )
            {
                return null;
            }

            Int64 chunkCount = ( fileInfo.Length / UploadConstants.SINGLE_CHUNK_MAX_SIZE ) + 1;

            if ( !serverMd5.Contains( '-' ) )
            {
                // TODO Release 2.0 Temp. Fix when fixed on server.
                return fileInfo.Length > 2000000 ? serverMd5 : CalculateMd5Hash( filePath );
            }

            var chunkHashes = new List<Byte>();

            foreach ( Byte[] buffer in IterateFileChunks( filePath ) )
            {
                using ( var md5 = MD5.Create() )
                {
                    chunkHashes.AddRange( md5.ComputeHash( buffer ) );
                }
            }

            Byte[] bytesHashed;

            using ( var md5 = MD5.Create() )
            {
                bytesHashed = md5.ComputeHash( chunkHashes.ToArray() );
            }

            var hex = new StringBuilder( bytesHashed.Length * 2 );
            foreach ( Byte bi in bytesHashed )
            {
                _ = hex.AppendFormat( "{0:x2}", bi );
            }

            return hex.ToString() + '-' + chunkCount;
        }

        public static String TryCalculateAmazonMd5Hash( String filePath )
        {
            FileInfo fileInfo = FileInfoHelper.TryGetFileInfo( filePath );

            Int64 chunkCount = ( fileInfo.Length / UploadConstants.SINGLE_CHUNK_MAX_SIZE ) + 1;

            if ( chunkCount == 1 )
            {
                return CalculateMd5Hash( filePath ).ToString();
            }

            var chunkHashes = new List<Byte>();

            foreach ( Byte[] buffer in IterateFileChunks( filePath ) )
            {
                using ( var md5 = MD5.Create() )
                {
                    chunkHashes.AddRange( md5.ComputeHash( buffer ) );
                }
            }

            Byte[] bytesHashed;

            using ( var md5 = MD5.Create() )
            {
                bytesHashed = md5.ComputeHash( chunkHashes.ToArray() );
            }

            var hex = new StringBuilder( bytesHashed.Length * 2 );
            foreach ( Byte bi in bytesHashed )
            {
                _ = hex.AppendFormat( "{0:x2}", bi );
            }

            return hex.ToString() + '-' + chunkCount;
        }

        public static String CalculateMd5Hash( String filename )
        {
            using ( var md5Hash = MD5.Create() )
            {
                var sb = new StringBuilder();
                foreach ( Byte data in md5Hash.ComputeHash( File.ReadAllBytes( filename ) ) )
                {
                    _ = sb.Append( data.ToString( "x2" ) );
                }

                return sb.ToString();
            }
        }

        public static String CalculateMd5Hash( Byte[] filename )
        {
            using ( var md5Hash = MD5.Create() )
            {
                var sb = new StringBuilder();
                foreach ( Byte data in md5Hash.ComputeHash( filename ) )
                {
                    _ = sb.Append( data.ToString( "x2" ) );
                }

                return sb.ToString();
            }
        }
    }
}
