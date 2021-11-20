using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Kernel;

using FluentAssertions;

using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Test.Builders;
using LUC.DiscoveryService.Test.InternalTests.Attributes;
using LUC.Interfaces;
using LUC.Services.Implementation;

using Moq;

using NUnit.Framework;

namespace LUC.DiscoveryService.Test.InternalTests
{
    [TestFixture]
    class ConnectionPoolSocketTest
    {
#if RECEIVE_TCP_FROM_OURSELF
        [Test, ConnectionPoolSocketConventions( BuildEndPointRequest.ReachableDsEndPoint )]
        public async Task DsReceiveAsync_SendPingRequestThenStopDsThenReceiveResponse_GetResponse(ConnectionPoolSocket socket)
        {
            DiscoveryService discoveryService = SetUpTests.DiscoveryService;
            discoveryService.Start();

            AutoResetEvent receiveDone = new AutoResetEvent( initialState: false );
            discoveryService.NetworkEventInvoker.PingReceived += ( sender, eventArgs ) => receiveDone.Set();

            socket.DsConnect( socket.Id, Constants.ConnectTimeout );
            PingRequest pingRequest = new PingRequest( KademliaId.RandomIDInKeySpace.Value, discoveryService.MachineId );

            socket.DsSend( pingRequest.ToByteArray(), Constants.SendTimeout );

            //wait until ds handle request
            receiveDone.WaitOne( Constants.SendTimeout );

            //wait until ds send response
            //Thread.Sleep( Constants.ReceiveTimeout );

            Task.Run( () => discoveryService.Stop() ).GetAwaiter();

            Byte[] bytes = await socket.DsReceiveAsync( Timeout.InfiniteTimeSpan );
            bytes.Should().NotBeNullOrEmpty();
        }
#endif

        [Test, ConnectionPoolSocketConventions(BuildEndPointRequest.RandomEndPoint)]
        public void StateInPool_FewTasksWantToTakeSocketAndOnlyOneSetsItInPool_WaitingOfTakingFromPoolIsGreaterThanFrequencySetItInPool( ConnectionPoolSocket socket )
        {
            //set TakenFromPool
            socket.StateInPool = SocketStateInPool.TakenFromPool;
            Int32 countOfThreads = 10;

            TimeSpan waitSeconds = TimeSpan.FromSeconds( value: 0.5 );
            TimeSpan precision = TimeSpan.FromMilliseconds( 100 );
            TimeSpan timeExecution = TimeSpan.FromMilliseconds( waitSeconds.TotalMilliseconds * countOfThreads );
            TimeSpan waitSecondsForThread = waitSeconds;

            Thread[] threadsThatTakingFromPool = new Thread[ countOfThreads ];
            AutoResetEvent allThreadIsEnded = new AutoResetEvent(initialState: false);

            Task.Factory.StartNew( () =>
             {
                 //{countOfThreads} another threads also do it
                 for ( Int32 numThread = 0; numThread < countOfThreads; numThread++ )
                 {

                     threadsThatTakingFromPool[ numThread ] = new Thread( () =>
                       {
                           DateTime start = DateTime.Now;
                           socket.StateInPool = SocketStateInPool.TakenFromPool;
                           DateTime end = DateTime.Now;

                           end.Subtract( start ).Should().BeCloseTo( waitSecondsForThread, precision );
                           waitSecondsForThread = TimeSpan.FromMilliseconds( waitSecondsForThread.TotalMilliseconds + waitSeconds.TotalMilliseconds );

                           Boolean isLastIteration = ( ( timeExecution.TotalMilliseconds - precision.TotalMilliseconds ) <= waitSecondsForThread.TotalMilliseconds ) &&
                                  ( waitSecondsForThread.TotalMilliseconds <= ( timeExecution.TotalMilliseconds + precision.TotalMilliseconds ) );
                           if ( isLastIteration )
                           {
                               allThreadIsEnded.Set();
                           }
                       } );
                     threadsThatTakingFromPool[ numThread ].Start();
                 }
             } );

            Int32 settingIsInPoolCount = countOfThreads;
            for ( Int32 numSettingIsInPool = 0; numSettingIsInPool < settingIsInPoolCount; numSettingIsInPool++ )
            {
                Thread.Sleep( waitSeconds );
                socket.StateInPool = SocketStateInPool.IsInPool;
            }

            Boolean isExecutedLastIterationInTime = allThreadIsEnded.WaitOne((Int32)waitSeconds.TotalMilliseconds * countOfThreads);
            isExecutedLastIterationInTime.Should().BeTrue();
        }

        [Test]
        public async Task ReturnedToPool_TakeSocketFromPoolThenSetStateInPoolInIsFailedAndReturnToPool_ShouldntBeReturned()
        {
            SetUpTests.DiscoveryService.Start();

            Fixture specimens = new Fixture();
            ConnectionPool connectionPool = specimens.Create<ConnectionPool>();

            EndPointBuilder endPointBuilder = new EndPointBuilder( BuildEndPointRequest.ReachableDsEndPoint );
            EndPoint socketId = endPointBuilder.Create<IPEndPoint>();

            var socket = await connectionPool.SocketAsync( socketId, Constants.ConnectTimeout, IOBehavior.Synchronous, Constants.TimeWaitSocketReturnedToPool ).
                ConfigureAwait( continueOnCapturedContext: false );
            
            SetUpTests.DiscoveryService.Stop();

            socket.Dispose();
            socket.ReturnedToPool()/*.Should().BeFalse()*/;




        }
    }
}
