using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LUC.DiscoveryService.Messages.KademliaRequests;
using AutoFixture;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Common;
using FluentAssertions;
using NUnit.Framework;
using LUC.DiscoveryService.Test.InternalTests.Builders;
using LUC.DiscoveryService.Test.InternalTests.Requests;

namespace LUC.DiscoveryService.Test.InternalTests
{
    class RequestTest
    {
        [Test]
        public void ResultAsync_SendPingRequestSyncLotOfTimes_ValueTasksAreImmediatelyCompleted()
        {
            SetUpTests.DiscoveryService.Start();

            String rndMachineId = SetUpTests.Fixture.Create<String>();
            PingRequest pingRequest = new PingRequest( KademliaId.RandomID.Value, rndMachineId );

            ContactBuilder contactBuilder = new ContactBuilder( SetUpTests.DiscoveryService, BuildContactRequest.OurContactWithIpAddresses );
            Contact ourContact = contactBuilder.Create<Contact>();

            Int32 attemptCountToForceError = 30;

            ParallelOptions parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Constants.MAX_THREADS
            };

            Parallel.For( fromInclusive: 0, attemptCountToForceError, parallelOptions, ( numAttempt ) =>
             {
                 ValueTask<(PingResponse, RpcError)> valueTask = pingRequest.ResultAsync<PingResponse>( ourContact, IOBehavior.Synchronous, SetUpTests.DefaultProtocolVersion );
                 valueTask.IsCompleted.Should().BeTrue();
             } );
        }
    }
}
