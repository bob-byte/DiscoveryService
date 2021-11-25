using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LUC.DiscoveryServices.Messages.KademliaRequests;
using AutoFixture;
using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Common;
using FluentAssertions;
using NUnit.Framework;
using LUC.DiscoveryServices.Test.InternalTests.Builders;
using LUC.DiscoveryServices.Test.InternalTests.Requests;

namespace LUC.DiscoveryServices.Test.InternalTests
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
