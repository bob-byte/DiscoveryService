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
using Nito.AsyncEx.Synchronous;

namespace LUC.DiscoveryServices.Test.InternalTests
{
    class RequestTest
    {
#if RECEIVE_UDP_FROM_OURSELF
        [Test]
        public void ResultAsync_SendPingRequestSyncLotOfTimes_ValueTasksAreImmediatelyCompleted()
        {
            DsSetUpTests.DiscoveryService.Start();

            //var contactBuilder = new ContactBuilder( DsSetUpTests.DiscoveryService, BuildContactRequest.OurContactWithIpAddresses );
            IContact ourContact = DsSetUpTests.DiscoveryService.OurContact;

            Int32 attemptCountToForceError = DsConstants.MAX_THREADS * 2;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = DsConstants.MAX_THREADS
            };

            Parallel.For( fromInclusive: 0, attemptCountToForceError, parallelOptions, ( numAttempt, loopState ) =>
             {
                 var pingRequest = new PingRequest( ourContact.KadId.Value, ourContact.MachineId );

                 Task<(PingResponse, RpcError)> taskWithResponse = pingRequest.ResultAsync<PingResponse>( ourContact, IoBehavior.Synchronous, DsSetUpTests.DefaultProtocolVersion ).AsTask();

                 Boolean isSyncCompleted = taskWithResponse.IsCompleted;

                 var rpcError = new RpcError();
                 if (isSyncCompleted)
                 {
                     (_, rpcError) = taskWithResponse.GetAwaiter().GetResult();
                 }

                 isSyncCompleted.Should().BeTrue();
                 rpcError.HasError.Should().BeFalse( rpcError.ToString() );

                 if ( !isSyncCompleted || rpcError.HasError )
                 {
                     loopState.Stop();
                 }
             } );
        }
#endif
    }
}
