using AutoFixture;

using DiscoveryServices.Common;
using DiscoveryServices.Kademlia.ClientPool;
using DiscoveryServices.Test.Builders;
using DiscoveryServices.Test.InternalTests.Attributes;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Helpers;

using NUnit.Framework;

using System;
using System.Net;
using System.Threading.Tasks;

namespace DiscoveryServices.Test.InternalTests
{
    class ConnectionPoolTest
    {
        [Test, SocketConventions( BuildEndPointRequest.ReachableDsEndPoint )]
        public void SocketAsync_FewThreadsWantToTakeSocketAndOnlyOneDoesIt_WaitingOfTakingFromPoolIsGreaterThanFrequencySetItInPool( ConnectionPool.Socket socket )
        {
            DsSetUpTests.DiscoveryService.Start();
            var connectionPool = ConnectionPool.Instance;
            Action takeFromPool = () => AsyncHelper.RunSync( async () => await connectionPool.SocketAsync( socket.Id, DsConstants.ConnectTimeout, IoBehavior.Synchronous, DsConstants.TimeWaitSocketReturnedToPool ).ConfigureAwait( continueOnCapturedContext: false ) );

            DsSetUpTests.TestOfChangingStateInTime(
                countOfNewThreads: DsConstants.MAX_THREADS,
                initTest: takeFromPool,
                opWhichIsExecutedByThreadSet: takeFromPool,
                opWhichMainThreadExecutes: () =>
                {
                    connectionPool.ReturnToPoolAsync(
                        socket,
                        DsConstants.ConnectTimeout,
                        IoBehavior.Synchronous
                    ).GetAwaiter().GetResult();
                },
                timeThreadSleep: TimeSpan.FromSeconds( value: 0.5 ),
                precisionOfExecution: TimeSpan.FromSeconds( value: 0.7 )
            );
        }

        [Test]
#pragma warning disable S2699 // Tests should include assertions
        public async Task ReturnedToPool_TakeSocketFromPoolThenSetStateInPoolInIsFailedAndReturnToPool_ShouldntBeReturned()
#pragma warning restore S2699 // Tests should include assertions
        {
            DsSetUpTests.DiscoveryService.Start();

            var connectionPool = ConnectionPool.Instance;

            var endPointBuilder = new EndPointsBuilder( BuildEndPointRequest.ReachableDsEndPoint );
            EndPoint socketId = endPointBuilder.Create<IPEndPoint>();

            ConnectionPool.Socket socket = await connectionPool.SocketAsync( socketId, DsConstants.ConnectTimeout, IoBehavior.Synchronous, DsConstants.TimeWaitSocketReturnedToPool ).
                ConfigureAwait( continueOnCapturedContext: false );

            DsSetUpTests.DiscoveryService.Stop();

            socket.DisposeUnmanagedResources();
            await socket.ReturnToPoolAsync( DsConstants.ConnectTimeout, IoBehavior.Asynchronous );
        }
    }
}
