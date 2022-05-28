using System;
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
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Constants;

namespace LUC.DiscoveryServices.Test.InternalTests
{
    class RequestTest
    {
        [Test]
        public void ResultAsync_SendPingRequestSyncLotOfTimes_ValueTasksAreImmediatelyCompleted()
        {
            DsSetUpTests.DiscoveryService.Start();

            String rndMachineId = DsSetUpTests.Fixture.Create<String>();
            var pingRequest = new PingRequest( KademliaId.Random().Value, rndMachineId );

            var contactBuilder = new ContactBuilder( DsSetUpTests.DiscoveryService, BuildContactRequest.OurContactWithIpAddresses );
            IContact ourContact = contactBuilder.Create<IContact>();

            Int32 attemptCountToForceError = DsConstants.MAX_THREADS;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = DsConstants.MAX_THREADS
            };

            Parallel.For( fromInclusive: 0, attemptCountToForceError, parallelOptions, ( numAttempt ) =>
             {
                 ValueTask<(PingResponse, RpcError)> valueTask = pingRequest.ResultAsync<PingResponse>( ourContact, IoBehavior.Synchronous, DsSetUpTests.DefaultProtocolVersion );
                 valueTask.IsCompleted.Should().BeTrue();
             } );
        }
    }
}
