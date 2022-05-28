using AutoFixture;

using FluentAssertions;

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia.ClientPool;
using LUC.DiscoveryServices.Test.Builders;
using LUC.DiscoveryServices.Test.InternalTests.Attributes;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Helpers;

using NUnit.Framework;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Test.InternalTests
{
    class ConnectionPoolTest
    {
        [SetUp]
        public void SetupDs() =>
            DsSetUpTests.DiscoveryService.Start();

        [Test, SocketConventions( BuildEndPointRequest.ReachableDsEndPoint )]
        public void SocketAsync_FewThreadsWantToTakeSocketAndOnlyOneDoesIt_WaitingOfTakingFromPoolIsGreaterThanFrequencySetItInPool( ConnectionPool.Socket socket )
        {
            var connectionPool = ConnectionPool.Instance;
            Action takeFromPool = () =>
            {
                Interlocked.Exchange(ref socket, connectionPool.SocketAsync(socket.Id, DsConstants.ConnectTimeout, IoBehavior.Synchronous, DsConstants.TimeWaitSocketReturnedToPool).GetAwaiter().GetResult());
            };

            DsSetUpTests.TestOfChangingStateInTime(
                countOfNewThreads: 1,
                initTest: takeFromPool,
                opWhichIsExecutedByThreadSet: takeFromPool,
                opWhichMainThreadExecutes: () =>
                {
                    socket.ReturnToPoolAsync(
                        DsConstants.ConnectTimeout,
                        IoBehavior.Synchronous
                    ).GetAwaiter().GetResult();
                },
                timeThreadSleep: TimeSpan.FromSeconds( value: 0.5 ),
                precisionOfExecution: TimeSpan.FromSeconds( value: 0.7 )
            );
        }

        [Test]
        public async Task ReturnedToPool_TakeSocketFromPoolThenSetStateInPoolInIsFailedAndReturnToPool_ShouldBeReturned()
        {
            var connectionPool = ConnectionPool.Instance;

            var endPointBuilder = new EndPointsBuilder( BuildEndPointRequest.ReachableDsEndPoint );
            EndPoint socketId = endPointBuilder.Create<IPEndPoint>();

            ConnectionPool.Socket socket = await connectionPool.SocketAsync( socketId, DsConstants.ConnectTimeout, IoBehavior.Synchronous, DsConstants.TimeWaitSocketReturnedToPool ).
                ConfigureAwait( continueOnCapturedContext: false );

            socket.DisposeUnmanagedResources();
            Boolean isReturned = await socket.ReturnToPoolAsync( DsConstants.ConnectTimeout, IoBehavior.Asynchronous ).ConfigureAwait(false);

            isReturned.Should().BeTrue(because: "Pool recover Socket when it is not connected");
        }
    }
}
