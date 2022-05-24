using System;
using System.Text;

namespace LUC.Interfaces.Extensions
{
    public static class BuildUriExtensions
    {
        private const String RIAK = "riak";

        private const String DOWNLOAD_ID = "download";

        public static String DownloadUri( String host, String serverBucketName, String fileHexPrefix, String objectKey )
        {
            String requestUri = fileHexPrefix == String.Empty ?
                Combine( host, RIAK, DOWNLOAD_ID, serverBucketName, objectKey ) :
                Combine( host, RIAK, DOWNLOAD_ID, serverBucketName, fileHexPrefix, objectKey );

            return requestUri;
        }

        public static String PostLogUri( String host )
            => Combine( host, "record" );

        public static String PostLoginUri( String host )
            => Combine( host, RIAK, "login" );

        public static String GetLogoutUri( String host )
            => Combine( host, RIAK, "logout" );

        public static String PostUploadUri( String host, String bucketName )
            => Combine( host, RIAK, "upload", bucketName );

        public static String PostCreatePseudoDirectoryUri( String host, String bucketName )
            => Combine( host, RIAK, "list", bucketName );

        public static String DeleteObjectUri( String host, String bucketName )
            => Combine( host, RIAK, "list", bucketName );

        public static String PostMoveUri( String host, String bucketName )
            => Combine( host, RIAK, "move", bucketName );

        public static String PostCopyUri( String host, String bucketName )
            => Combine( host, RIAK, "copy", bucketName );

        public static String PostRenameUri( String host, String bucketName )
            => Combine( host, RIAK, "rename", bucketName );

        public static String GetListUri( String host, String bucketName, String prefix )
        {
            String result = String.IsNullOrEmpty( prefix )
                ? Combine( host, RIAK, "list", bucketName )
                : Combine( host, RIAK, "list", bucketName, "?prefix=" + prefix );

            return result;
        }

        public static String GetLockUri( String host, String bucketName )
            => Combine( host, RIAK, "list", bucketName );

        public static String Combine( params String[] uri )
        {
            uri[ 0 ] = uri[ 0 ].TrimEnd( '/' );

            var bld = new StringBuilder();
            bld.Append( uri[ 0 ] + "/" );

            for ( Int32 i = 1; i < uri.Length; i++ )
            {
                if ( uri[ i ] == null )
                {
                    continue;
                }

                uri[ i ] = uri[ i ].TrimStart( '/' ).TrimEnd( '/' );
                if ( uri[ i ] == "" )
                {
                    continue;
                }

                bld.Append( uri[ i ] + "/" );
            }

            return bld.ToString();
        }
    }
}
