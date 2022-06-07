using System;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia.ClientPool;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Test.Builders;
using LUC.DiscoveryServices.Test.InternalTests.Attributes;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;

using NUnit.Framework;

using static LUC.DiscoveryServices.Kademlia.ClientPool.ConnectionPool;

namespace LUC.DiscoveryServices.Test.InternalTests
{
    [TestFixture]
    class ConnectionPoolSocketTest
    {
//#if RECEIVE_UDP_FROM_OURSELF
//        [Test, SocketConventions( BuildEndPointRequest.ReachableDsEndPoint )]
//        public async Task DsReceiveAsync_SendPingRequestThenStopDsThenReceiveResponse_GetResponse(Socket socket)
//        {
//            DiscoveryService discoveryService = DsSetUpTests.DiscoveryService;
//            discoveryService.Start();

//            AutoResetEvent receiveDone = new AutoResetEvent( initialState: false );
//            discoveryService.NetworkEventInvoker.PingReceived += ( sender, eventArgs ) => receiveDone.Set();

//            socket.DsConnect( socket.Id, DsConstants.ConnectTimeout );
//            var pingRequest = new PingRequest( KademliaId.RandomIDInKeySpace.Value, discoveryService.MachineId );

//            socket.DsSend( pingRequest.ToByteArray(), DsConstants.SendTimeout );

//            //wait until ds handle request
//            receiveDone.WaitOne( DsConstants.SendTimeout );

//            discoveryService.Stop();

//            socket.DisposeUnmanagedResources();
//            Byte[] bytes = await socket.DsReceiveAsync( Timeout.InfiniteTimeSpan );
//            bytes.Should().NotBeNullOrEmpty();
//        }
//#endif

        [Test, SocketConventions(BuildEndPointRequest.RandomEndPoint)]
        public void StateInPool_FewTasksWantToTakeSocketAndOnlyOneSetsItInPool_WaitingOfTakingFromPoolIsGreaterThanFrequencySetItInPool( Socket socket )
        {
            Action setTakeFromPoolState = () => socket.TakeFromPoolAsync( IoBehavior.Synchronous, DsConstants.TimeWaitSocketReturnedToPool ).ConfigureAwait( continueOnCapturedContext: false );

            DsSetUpTests.TestOfChangingStateInTime(
                countOfNewThreads: DsConstants.MAX_THREADS,
                initTest: setTakeFromPoolState,
                opWhichIsExecutedByThreadSet:
                setTakeFromPoolState,
                opWhichMainThreadExecutes: () => socket.AllowTakeSocket( SocketStateInPool.IsInPool ),
                timeThreadSleep: TimeSpan.FromSeconds( value: 0.5 ),
                precisionOfExecution: TimeSpan.FromSeconds( 0.5 )
            );
        }
    }
}
