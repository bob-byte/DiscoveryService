using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FluentAssertions;

using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;

using NUnit.Framework;

namespace LUC.DiscoveryService.Test.InternalTests.Kademlia
{
    class KademliaIdTest
    {
        [Test]
        public void RandomId_GenerateLotOfIds_AllShouldBeDifferent()
        {
            Int32 countOfIds = 10;

            List<KademliaId> ids = new List<KademliaId>(countOfIds);
            Parallel.For( fromInclusive: 0, toExclusive: countOfIds, ( numId ) =>
             {
                 KademliaId rndId = KademliaId.RandomID;

                 lock(ids)
                 {
                     String message = $"Found the same key at {ids.Count}";
                     Assert.IsFalse( ids.Contains( rndId ),  message);

                     ids.Add( rndId );
                 }
             } );
        }
    }
}
