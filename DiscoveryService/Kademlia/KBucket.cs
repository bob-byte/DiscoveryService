using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using LUC.DiscoveryServices.Kademlia.Exceptions;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Extensions;

using Newtonsoft.Json;

namespace LUC.DiscoveryServices.Kademlia
{
    public class KBucket
    {
        /// <summary>
        /// Initializes a k-bucket with the default range of 0 - 2^160
        /// </summary>
        public KBucket()
        {
            Contacts = new SynchronizedCollection<IContact>();
            Low = 0;
            High = BigInteger.Pow( new BigInteger( 2 ), 160 );
        }

        /// <summary>
        /// Initializes a k-bucket with a specific ID range.
        /// </summary>
        public KBucket( BigInteger low, BigInteger high )
        {
            Contacts = new SynchronizedCollection<IContact>();
            Low = low;
            High = high;
        }

        public DateTime TimeStamp { get; set; }

        public IList<IContact> Contacts { get; set; }

        public BigInteger Low { get; set; }

        public BigInteger High { get; set; }

        /// <summary>
        /// We are going to assume that the "key" for this bucket is it's high range.
        /// </summary>
        [JsonIgnore]
        public BigInteger Key => High;

        [JsonIgnore]
        public Boolean IsBucketFull =>
            Contacts.Count == DsConstants.K;

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void Touch() =>
            TimeStamp = DateTime.UtcNow;

        /// <summary>
        /// True if ID is in range of this bucket.
        /// </summary>
        public Boolean HasInRange( KademliaId id ) =>
            ( Low <= id ) && ( id < High );

        public Boolean HasInRange( BigInteger id ) =>
            ( Low <= id ) && ( id < High );

        /// <summary>
        /// True if a contact matches this ID.
        /// </summary>
        public Boolean Contains( IContact contact ) =>
            Contacts.Any( c => c.Equals( contact ) );

        /// <summary>
        /// Add a contact to the bucket, at the end, as this is the most recently seen contact.
        /// A full bucket throws an exception.
        /// </summary>
        public void AddContact( IContact contact )
        {
            Validate.IsTrue<TooManyContactsException>( Contacts.Count < DsConstants.K, "Bucket is full" );
            Contacts.Add( contact );
        }

        public void EvictContact( IContact contact ) =>
            Contacts.Remove( contact );

        /// <summary>
        /// Replaces the contact with the new contact, thus updating the LastSeen and network addressinfo. 
        /// </summary>
        public void ReplaceContact( IContact contact )
        {
            IContact contactInBucket = Contacts.Single( c => c.Equals( contact ) );
            contactInBucket.UpdateAccordingToNewState( contact );
        }

        /// <summary>
        /// Splits the kbucket into returning two new kbuckets filled with contacts separated by the new midpoint
        /// </summary>
        public (KBucket, KBucket) Split()
        {
            BigInteger midpoint = ( Low + High ) / 2;
            var k1 = new KBucket( Low, midpoint );
            var k2 = new KBucket( midpoint, High );

            Contacts.ForEach( c =>
             {
                // <, because the High value is exclusive in the HasInRange test.
                KBucket k = c.KadId < midpoint ? k1 : k2;
                 k.AddContact( c );
             } );

            return (k1, k2);
        }

        /// <summary>
        /// Returns number of bits that are in common across all contacts.
        /// If there are no contacts, or no shared bits, the return is 0.
        /// </summary>
        public Int32 Depth()
        {
            Boolean[] bits = new Boolean[ 0 ];

            if ( Contacts.Count > 0 )
            {
                // Start with the first contact.
                bits = Contacts[ 0 ].KadId.Bytes.Bits().ToArray();

                Contacts.Skip( count: 1 ).ForEach( c => bits = SharedBits( bits, c.KadId ) );
            }

            return bits.Length;
        }

        /// <summary>
        /// Returns a new bit array of just the shared bits.
        /// </summary>
        protected Boolean[] SharedBits( Boolean[] bits, KademliaId id )
        {
            Boolean[] idbits = id.Bytes.Bits().ToArray();

            // Useful for viewing the bit arrays.
            //string sbits1 = System.String.Join("", bits.Select(b => b ? "1" : "0"));
            //string sbits2 = System.String.Join("", idbits.Select(b => b ? "1" : "0"));

            Int32 q = DsConstants.KID_LENGTH_BITS - 1;
            Int32 n = bits.Length - 1;
            var sharedBits = new List<Boolean>();

            while ( n >= 0 && bits[ n ] == idbits[ q ] )
            {
                sharedBits.Insert( 0, bits[ n ] );
                --n;
                --q;
            }

            return sharedBits.ToArray();
        }        
    }
}
