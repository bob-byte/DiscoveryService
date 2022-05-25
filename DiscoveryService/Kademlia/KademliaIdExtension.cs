using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;

namespace LUC.DiscoveryServices.Kademlia
{
    internal static class KademliaIdExtension
    {
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
            IEnumerable<Boolean> lowBits = new KademliaId( bucket.Low ).Bytes.Bits().Reverse();
            IEnumerable<Boolean> highBits = new KademliaId( bucket.High ).Bytes.Bits().Reverse();

            // We randomize "below" this High prefix range.
            Int32 highPrefix = highBits.TakeWhile( b => !b ).Count() + 1;
            // Up to the prefix of the Low range.
            // This sets up a mask of 0's for the LSB's in the Low prefix.
            Int32 lowPrefix = lowBits.TakeWhile( b => !b ).Count();
            // RandomizeBeyond is little endian for "bits after" so reverse high/low prefixes.
            KademliaId id = KademliaId.Zero.RandomizeBeyond( DsConstants.KID_LENGTH_BITS - highPrefix, DsConstants.KID_LENGTH_BITS - lowPrefix, forceBit1 );

            // The we add the low range.
            id = new KademliaId( bucket.Low + id.Value );

            return id;
        }
    }
}
