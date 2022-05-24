using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

using FluentAssertions;

using LUC.Interfaces.Discoveries;

using NUnit.Framework;

namespace DiscoveryServices.Test.InternalTests.Kademlia
{
    class KademliaIdTest
    {
        [Test]
        public void RandomId_GenerateLotOfIds_AllShouldBeDifferent()
        {
            Int32 countOfIds = 1000 * 1000;
            Int32 maxAvailableDegreeOfParallelism = Environment.ProcessorCount * 1000;

            var parallelOptions = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = countOfIds > maxAvailableDegreeOfParallelism ? 
                    maxAvailableDegreeOfParallelism : 
                    countOfIds 
            };
            var ids = new ConcurrentDictionary<KademliaId, Object>();

            Parallel.For( 
                fromInclusive: 0, 
                toExclusive: countOfIds, 
                parallelOptions, 
                body: ( numId, loopState ) =>
                {
                    var rndId = KademliaId.Random();
                
                    Boolean isAdded = ids.TryAdd( rndId, value: null );
                    if(!isAdded)
                    {
                        Assert.IsTrue( isAdded, message: $"Found the same key at {ids.Count}" );
                        loopState.Stop();
                    }
                } 
            );
        }

        [Test]
        public void EqualOperator_CreateRandomIdAndCompareWithItValue_ShouldBeEqual()
        {
            var id = KademliaId.Random();
            BigInteger bigInteger = id.Value;

            Boolean isEqual = id == bigInteger;
            isEqual.Should().BeTrue();
        }

        [Test]
        public void EqualOperator_CreateRandomIdAndCompareWithDefaultBigInteger_ShouldNotBeEqual()
        {
            var id = KademliaId.Random();
            BigInteger bigInteger = BigInteger.Zero;

            Boolean isEqual = id == bigInteger;
            isEqual.Should().BeFalse();
        }
    }
}
