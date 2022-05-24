using LUC.Interfaces.Constants;
using LUC.Interfaces.Exceptions;
using LUC.Interfaces.Extensions;

using Newtonsoft.Json;

using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace LUC.Interfaces.Discoveries
{
    public class KademliaId : IComparable
    {
        private static readonly Random s_rndObj = new Random();

        public BigInteger Value { get; set; }

        /// <summary>
        /// The array returned is in little-endian order (lsb at index 0)
        /// </summary>
        [JsonIgnore]
        public Byte[] Bytes
        {
            get
            {
                // Zero-pad msb's if ToByteArray length != Constants.LENGTH_BYTES
                Byte[] bytes = new Byte[ Constants.DsConstants.KID_LENGTH_BYTES ];
                Byte[] partial = Value.ToByteArray().Take( DsConstants.KID_LENGTH_BYTES ).ToArray();    // remove msb 0 at index 20.
                partial.CopyTo( bytes, 0 );

                return bytes;
            }
        }

        [JsonIgnore]
        public String AsBigEndianString => String.Join( "", Bytes.Bits().Reverse().Select( b => b ? "1" : "0" ) );

        public Boolean[] AsBigEndianBool() => 
            Bytes.Bits().Reverse().ToArray();

        /// <summary>
        /// Produce a random ID distributed evenly across the 160 bit space.
        /// </summary>
        [JsonIgnore]
        public static KademliaId RandomIDInKeySpace
        {
            get
            {
                Byte[] data = new Byte[ DsConstants.KID_LENGTH_BYTES ];
                var id = new KademliaId( data );
                // Uniform random bucket index.
                Int32 idx = s_rndObj.Next( DsConstants.KID_LENGTH_BITS );
                // 0 <= idx <= 159
                // Remaining bits are randomized to get unique ID.
                id.SetBit( idx );
                id = id.RandomizeBeyond( idx );

                return id;
            }
        }

        /// <summary>
        /// Produce a random ID.
        /// </summary>
        public static KademliaId Random()
        {
            //var guid = Guid.NewGuid();
            //Byte[] guidBuffer = guid.ToByteArray();

            Byte[] rndBuffer = new Byte[ DsConstants.KID_LENGTH_BYTES/* - guidBuffer.Length*/ ];
            s_rndObj.NextBytes( rndBuffer );

            //Byte[] idBuffer = guidBuffer.Concat( rndBuffer );

            var randomId = new KademliaId( rndBuffer /*idBuffer*/ );
            return randomId;
        }

        [JsonIgnore]
        public static KademliaId Zero => new KademliaId( new Byte[ DsConstants.KID_LENGTH_BYTES ] );

        [JsonIgnore]
        public static KademliaId One
        {
            get
            {
                Byte[] data = new Byte[ DsConstants.KID_LENGTH_BYTES ];
                data[ 0 ] = 0x01;

                return new KademliaId( data );
            }
        }

        [JsonIgnore]
        public static KademliaId Mid
        {
            get
            {
                Byte[] data = new Byte[ DsConstants.KID_LENGTH_BYTES ];
                data[ DsConstants.KID_LENGTH_BYTES - 1 ] = 0x80;

                return new KademliaId( data );
            }
        }

        [JsonIgnore]
        public static KademliaId Max => new KademliaId( Enumerable.Repeat( (Byte)0xFF, DsConstants.KID_LENGTH_BYTES ).ToArray() );

        /// <summary>
        /// For serialization.
        /// </summary>
        public KademliaId()
        {
        }

        /// <summary>
        /// Construct the ID from a byte array.
        /// </summary>
        public KademliaId( Byte[] data )
        {
            IDInit( data );
        }

        /// <summary>
        /// Construct the ID from another BigInteger value.
        /// </summary>
        public KademliaId( BigInteger bi )
        {
            Value = bi;
        }

        public KademliaId( String strid )
        {
            Boolean ok = BigInteger.TryParse( strid, out BigInteger id );
            Validate.IsTrue<BadIDException>( ok, "ID string is not valid." );
            Value = id;
        }

        public static KademliaId FromString( String str )
        {
            Byte[] bytes = Encoding.UTF8.GetBytes( str );
            SHA1 sha = new SHA1CryptoServiceProvider();
            Byte[] id = sha.ComputeHash( bytes );

            return new KademliaId( id );
        }

        /// <summary>
        /// Initialize the ID from a byte array, appending a 0 to force unsigned values.
        /// </summary>
        protected void IDInit( Byte[] data )
        {
            Validate.IsTrue<IDLengthException>( data.Length == DsConstants.KID_LENGTH_BYTES, "ID must be " + DsConstants.KID_LENGTH_BYTES + " bytes in length." );
            Value = new BigInteger( data.Append0() );
        }

        /// <summary>
        /// Little endian randomization of of an ID beyond the specified (little endian) bit number.
        /// The optional parameter forceBit1 is for our unit tests.
        /// This CLEARS bits from bit+1 to ID_LENGTH_BITS!
        /// </summary>
        public KademliaId RandomizeBeyond( Int32 bit, Int32 minLsb = 0, Boolean forceBit1 = false )
        {
            Byte[] randomized = Bytes;

            var newid = new KademliaId( randomized );

            // TODO: Optimize
            for ( Int32 i = bit + 1; i < DsConstants.KID_LENGTH_BITS; i++ )
            {
                newid.ClearBit( i );
            }

            // TODO: Optimize
            for ( Int32 i = minLsb; i < bit; i++ )
            {
                if ( ( s_rndObj.NextDouble() < 0.5 ) || forceBit1 )
                {
                    newid.SetBit( i );
                }
            }

            return newid;
        }

        /// <summary>
        /// Clears the bit n, from the little-endian LSB.
        /// </summary>
        private KademliaId ClearBit( Int32 n )
        {
            Byte[] bytes = Bytes;
            bytes[ n / 8 ] &= (Byte)( ( 1 << ( n % 8 ) ) ^ 0xFF );
            Value = new BigInteger( bytes.Append0() );

            // for continuations.
            return this;
        }

        /// <summary>
        /// Sets the bit n, from the little-endian LSB.
        /// </summary>
        private KademliaId SetBit( Int32 n )
        {
            Byte[] bytes = Bytes;
            bytes[ n / 8 ] |= (Byte)( 1 << ( n % 8 ) );
            Value = new BigInteger( bytes.Append0() );

            // for continuations.
            return this;
        }

        // IComparable required methods.

        /// <summary>
        /// (From zencoders implemementation)
        /// Method used to get the hash code according to the algorithm: 
        /// http://stackoverflow.com/questions/16340/how-do-i-generate-a-hashcode-from-a-byte-array-in-c/425184#425184
        /// </summary>
        /// <returns>Integer representing the hashcode</returns>
        public override Int32 GetHashCode() => Value.GetHashCode();

        public override Boolean Equals( Object obj )
        {
            Validate.IsTrue<NotAnIDException>( obj is KademliaId, "Cannot compare non-ID objects to an ID" );

            return this == (KademliaId)obj;
        }

        public override String ToString() => Value.ToString();

        /// <summary>
        /// Compare one ID with another.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>-1 if this ID < test ID, 0 if equal, 1 if this ID > test ID.</test></returns>
        public Int32 CompareTo( Object obj )
        {
            Validate.IsTrue<NotAnIDException>( obj is KademliaId, "Cannot compare non-ID objects to an ID" );
            var test = (KademliaId)obj;

            if ( this == test )
            {
                return 0;
            }

            switch (this < test)
            {
                case true:
                    return -1;
                default:
                    return 1;
            }
        }

        // Operators:

        public static KademliaId operator ^( KademliaId a, KademliaId b ) => new KademliaId( a.Value ^ b.Value );

        public static KademliaId operator ^( BigInteger a, KademliaId b ) => new KademliaId( a ^ b.Value );

        public static Boolean operator <( KademliaId a, KademliaId b ) => a.Value < b.Value;

        public static Boolean operator >( KademliaId a, KademliaId b ) => a.Value > b.Value;

        public static Boolean operator <=( KademliaId a, KademliaId b ) => a.Value <= b.Value;

        public static Boolean operator >=( KademliaId a, KademliaId b ) => a.Value >= b.Value;

        public static Boolean operator <( BigInteger a, KademliaId b ) => a < b.Value;

        public static Boolean operator >( BigInteger a, KademliaId b ) => a > b.Value;

        public static Boolean operator <=( BigInteger a, KademliaId b ) => a <= b.Value;

        public static Boolean operator >=( BigInteger a, KademliaId b ) => a >= b.Value;

        public static Boolean operator <( KademliaId a, BigInteger b ) => a.Value < b;

        public static Boolean operator >( KademliaId a, BigInteger b ) => a.Value > b;

        public static Boolean operator <=( KademliaId a, BigInteger b ) => a.Value <= b;

        public static Boolean operator >=( KademliaId a, BigInteger b ) => a.Value >= b;

        public static Boolean operator ==( KademliaId a, KademliaId b )
        {
            Boolean isEqual;
            if ( ( a is null ) && ( b is null ) )
            {
                isEqual = true;
            }
            else
            {
                isEqual = !( ( a is null ) || ( b is null ) ) && ( a.Value == b.Value );
            }

            return isEqual;
        }

        public static Boolean operator ==( KademliaId a, BigInteger b )
        {
            Boolean isEqual;
            if ( ( a is null ) && ( b == default ) )
            {
                isEqual = true;
            }
            else
            {
                isEqual = !( a is null ) && ( a.Value == b );
            }

            return isEqual;
        }

        public static Boolean operator ==( BigInteger a, KademliaId b )
        {
            Boolean isEqual;
            if ( ( a == default ) && ( b is null ) )
            {
                isEqual = true;
            }
            else
            {
                isEqual = b == a;
            }

            return isEqual;
        }

        public static Boolean operator !=( BigInteger a, KademliaId b ) => !( a == b ); // Already have that

        public static Boolean operator !=( KademliaId a, KademliaId b ) => !( a == b ); // Already have that

        public static Boolean operator !=( KademliaId a, BigInteger b ) => !( a == b ); // Already have that

        public static KademliaId operator <<( KademliaId idobj, Int32 count ) => new KademliaId( idobj.Value << count );

        public static KademliaId operator >>( KademliaId idobj, Int32 count ) => new KademliaId( idobj.Value >> count );
    }
}
