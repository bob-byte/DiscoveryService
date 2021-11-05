using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia.Exceptions;

using Newtonsoft.Json;

namespace LUC.DiscoveryService.Kademlia
{
    public class KademliaId : IComparable
    {
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
                Byte[] bytes = new Byte[ Constants.ID_LENGTH_BYTES ];
                Byte[] partial = Value.ToByteArray().Take( Constants.ID_LENGTH_BYTES ).ToArray();    // remove msb 0 at index 20.
                partial.CopyTo( bytes, 0 );

                return bytes;
            }
        }

        [JsonIgnore]
        public String AsBigEndianString => String.Join( "", Bytes.Bits().Reverse().Select( b => b ? "1" : "0" ) );

        [JsonIgnore]
        public Boolean[] AsBigEndianBool => Bytes.Bits().Reverse().ToArray();

        /// <summary>
        /// Produce a random ID distributed evenly across the 160 bit space.
        /// </summary>
        [JsonIgnore]
        public static KademliaId RandomIDInKeySpace
        {
            get
            {
                Byte[] data = new Byte[ Constants.ID_LENGTH_BYTES ];
                KademliaId id = new KademliaId( data );
                // Uniform random bucket index.
                Int32 idx = rnd.Next( Constants.ID_LENGTH_BITS );
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
        [JsonIgnore]
        public static KademliaId RandomID
        {
            get
            {
                Byte[] buffer = new Byte[ Constants.ID_LENGTH_BYTES ];
                rnd.NextBytes( buffer );

                return new KademliaId( buffer );
            }
        }

        [JsonIgnore]
        public static KademliaId Zero => new KademliaId( new Byte[ Constants.ID_LENGTH_BYTES ] );

        [JsonIgnore]
        public static KademliaId One
        {
            get
            {
                Byte[] data = new Byte[ Constants.ID_LENGTH_BYTES ];
                data[ 0 ] = 0x01;

                return new KademliaId( data );
            }
        }

        [JsonIgnore]
        public static KademliaId Mid
        {
            get
            {
                Byte[] data = new Byte[ Constants.ID_LENGTH_BYTES ];
                data[ Constants.ID_LENGTH_BYTES - 1 ] = 0x80;

                return new KademliaId( data );
            }
        }

        [JsonIgnore]
        public static KademliaId Max => new KademliaId( Enumerable.Repeat( (Byte)0xFF, Constants.ID_LENGTH_BYTES ).ToArray() );

#if DEBUG
        public static Random rnd = new Random();
#else
        private static Random rnd = new Random();
#endif

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
        /// Returns an ID within the range of the bucket's Low and High range.
        /// The optional parameter forceBit1 is for our unit tests.
        /// This works because the bucket low-high range will always be a power of 2!
        /// </summary>
        public static KademliaId RandomIDWithinBucket( KBucket bucket, Boolean forceBit1 = false )
        {
            // Simple case:
            // High = 1000
            // Low  = 0010
            // We want random values between 0010 and 1000

            // Low and High will always be powers of 2.
            System.Collections.Generic.IEnumerable<Boolean> lowBits = new KademliaId( bucket.Low ).Bytes.Bits().Reverse();
            System.Collections.Generic.IEnumerable<Boolean> highBits = new KademliaId( bucket.High ).Bytes.Bits().Reverse();

            // We randomize "below" this High prefix range.
            Int32 highPrefix = highBits.TakeWhile( b => !b ).Count() + 1;
            // Up to the prefix of the Low range.
            // This sets up a mask of 0's for the LSB's in the Low prefix.
            Int32 lowPrefix = lowBits.TakeWhile( b => !b ).Count();
            // RandomizeBeyond is little endian for "bits after" so reverse high/low prefixes.
            KademliaId id = Zero.RandomizeBeyond( Constants.ID_LENGTH_BITS - highPrefix, Constants.ID_LENGTH_BITS - lowPrefix, forceBit1 );

            // The we add the low range.
            id = new KademliaId( bucket.Low + id.Value );

            return id;
        }

        /// <summary>
        /// Initialize the ID from a byte array, appending a 0 to force unsigned values.
        /// </summary>
        protected void IDInit( Byte[] data )
        {
            Validate.IsTrue<IDLengthException>( data.Length == Constants.ID_LENGTH_BYTES, "ID must be " + Constants.ID_LENGTH_BYTES + " bytes in length." );
            Value = new BigInteger( data.Append0() );
        }

        /// <summary>
        /// Little endian randomization of of an ID beyond the specified (little endian) bit number.
        /// The optional parameter forceBit1 is for our unit tests.
        /// This CLEARS bits from bit+1 to ID_LENGTH_BITS!
        /// </summary>
#if DEBUG
        public KademliaId RandomizeBeyond( Int32 bit, Int32 minLsb = 0, Boolean forceBit1 = false )
#else
        protected ID RandomizeBeyond(int bit, int minLsb = 0, bool forceBit1 = false)
#endif
        {
            Byte[] randomized = Bytes;

            KademliaId newid = new KademliaId( randomized );

            // TODO: Optimize
            for ( Int32 i = bit + 1; i < Constants.ID_LENGTH_BITS; i++ )
            {
                newid.ClearBit( i );
            }

            // TODO: Optimize
            for ( Int32 i = minLsb; i < bit; i++ )
            {
                if ( ( rnd.NextDouble() < 0.5 ) || forceBit1 )
                {
                    newid.SetBit( i );
                }
            }

            return newid;
        }

        /// <summary>
        /// Clears the bit n, from the little-endian LSB.
        /// </summary>
        public KademliaId ClearBit( Int32 n )
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
        public KademliaId SetBit( Int32 n )
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
            KademliaId test = (KademliaId)obj;

            return this == test ? 0 : this < test ? -1 : 1;
        }

        // Operators:

        public static KademliaId operator ^( KademliaId a, KademliaId b )
        {
            return new KademliaId( a.Value ^ b.Value );
        }

        public static KademliaId operator ^( BigInteger a, KademliaId b )
        {
            return new KademliaId( a ^ b.Value );
        }

        public static Boolean operator <( KademliaId a, KademliaId b )
        {
            return a.Value < b.Value;
        }

        public static Boolean operator >( KademliaId a, KademliaId b )
        {
            return a.Value > b.Value;
        }

        public static Boolean operator <=( KademliaId a, KademliaId b )
        {
            return a.Value <= b.Value;
        }

        public static Boolean operator >=( KademliaId a, KademliaId b )
        {
            return a.Value >= b.Value;
        }

        public static Boolean operator <( BigInteger a, KademliaId b )
        {
            return a < b.Value;
        }

        public static Boolean operator >( BigInteger a, KademliaId b )
        {
            return a > b.Value;
        }

        public static Boolean operator <=( BigInteger a, KademliaId b )
        {
            return a <= b.Value;
        }

        public static Boolean operator >=( BigInteger a, KademliaId b )
        {
            return a >= b.Value;
        }

        public static Boolean operator <( KademliaId a, BigInteger b )
        {
            return a.Value < b;
        }

        public static Boolean operator >( KademliaId a, BigInteger b )
        {
            return a.Value > b;
        }

        public static Boolean operator <=( KademliaId a, BigInteger b )
        {
            return a.Value <= b;
        }

        public static Boolean operator >=( KademliaId a, BigInteger b )
        {
            return a.Value >= b;
        }

        public static Boolean operator ==( KademliaId a, KademliaId b )
        {
            Validate.IsFalse<NullIDException>( ReferenceEquals( a, null ), "ID a cannot be null." );
            Validate.IsFalse<NullIDException>( ReferenceEquals( b, null ), "ID b cannot be null." );

            return a.Value == b.Value;
        }

        public static Boolean operator ==( KademliaId a, BigInteger b )
        {
            Validate.IsFalse<NullIDException>( ReferenceEquals( a, null ), "ID a cannot be null." );
            Validate.IsFalse<NullIDException>( ReferenceEquals( b, null ), "ID b cannot be null." );

            return a.Value == b;
        }

        public static Boolean operator ==(BigInteger a, KademliaId b)
        {
            Boolean isEqual;
            if(a == default && b == null)
            {
                isEqual = true;
            }
            else
            {
               isEqual = b == a;
            }

            return isEqual;
        }

        public static Boolean operator !=( BigInteger a, KademliaId b )
        {
            return !( a == b ); // Already have that
        }

        public static Boolean operator !=( KademliaId a, KademliaId b )
        {
            return !( a == b ); // Already have that
        }

        public static Boolean operator !=( KademliaId a, BigInteger b )
        {
            return !( a == b ); // Already have that
        }

        public static KademliaId operator <<( KademliaId idobj, Int32 count )
        {
            return new KademliaId( idobj.Value << count );
        }

        public static KademliaId operator >>( KademliaId idobj, Int32 count )
        {
            return new KademliaId( idobj.Value >> count );
        }
    }
}
