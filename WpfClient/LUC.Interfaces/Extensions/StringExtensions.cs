using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace LUC.Interfaces.Extensions
{
    public static class StringExtensions
    {
        public static String ToHexString( this String value )
        {
            Byte[] bytes = Encoding.UTF8.GetBytes( value );

            String hexStringWithDashes = BitConverter.ToString( bytes );
            String result = hexStringWithDashes.Replace( "-", "" );

            return result;
        }

        public static String FromHexString( this String hex )
        {
            if ( hex == null )
            {
                _ = MessageBox.Show( new System.Diagnostics.StackTrace().ToString() );
                return String.Empty;
            }

            String[] hexParts = hex.Split( '/' ).Where( x => !String.IsNullOrEmpty( x ) ).ToArray();
            var resultParts = new List<String>();

            foreach ( String hexPart in hexParts )
            {
                Byte[] raw = new Byte[ hexPart.Length / 2 ];
                for ( Int32 i = 0; i < raw.Length; i++ )
                {
                    raw[ i ] = Convert.ToByte( hexPart.Substring( i * 2, 2 ), 16 );
                }

                String unHex = Encoding.UTF8.GetString( raw );
                resultParts.Add( unHex );
            }

            String result = String.Join( "\\", resultParts );
            return result;
        }

        public static String LastHexPrefixPart( this String value )
        {
            if ( String.IsNullOrEmpty( value ) )
            {
                throw new ArgumentNullException( nameof( value ), $"Method {nameof( LastHexPrefixPart )}" );
            }

            String[] parts = value.TrimEnd( '/' ).Split( '/' );

            String result = parts.Last();

            return result;
        }

        public static String ToHexPrefix( this String prefix )
        {
            if ( prefix == String.Empty )
            {
                return String.Empty;
            }
            else
            {
                var result = new StringBuilder();

                foreach ( String item in prefix.Split( new[] { '\\' } ) )
                {
                    result.Append( item.ToHexString() + "/" );
                }

                return result.ToString();
            }
        }

        public static String WithAttention( this String logRecord ) =>
            $"\n*************************\n{logRecord}\n*************************\n";

        public static Byte[] ToUtf8( this String str ) =>
            Encoding.UTF8.GetBytes( str );

        public static Int32 ToInt32( this String str ) =>
            Convert.ToInt32( str );

        public static Boolean IsJunctionDirectory( this String directoryPath )
        {
            var dirInfo = new DirectoryInfo( directoryPath );

            Boolean isReparsePoint = dirInfo.Exists && ( ( dirInfo.Attributes & FileAttributes.ReparsePoint ) != 0 );
            return isReparsePoint;
        }

        public static String Base64Encode( this String plainText )
        {
            Byte[] plainTextBytes = Encoding.UTF8.GetBytes( plainText );
            return Convert.ToBase64String( plainTextBytes );
        }

        public static String Base64Decode( this String base64EncodedData )
        {
            Byte[] base64EncodedBytes = Convert.FromBase64String( base64EncodedData );
            return Encoding.UTF8.GetString( base64EncodedBytes );
        }
    }
}
