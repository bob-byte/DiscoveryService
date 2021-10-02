using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Messages;
using LUC.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

//
// Client socket, maintained by the Connection Pool
//
namespace LUC.DiscoveryService.Kademlia.ClientPool
{
    ///<inheritdoc/>
    class ConnectionPoolSocket : DiscoveryServiceSocket
    {
        private SocketStateInPool m_stateInPool;
        private readonly AutoResetEvent m_removedFromPool;

        /// <inheritdoc/>
        public ConnectionPoolSocket( SocketType socketType, ProtocolType protocolType, EndPoint remoteEndPoint, ConnectionPool belongPool, ILoggingService log )
            : base( remoteEndPoint.AddressFamily, socketType, protocolType, log )
        {
            Id = remoteEndPoint;
            Pool = belongPool;
            m_removedFromPool = new AutoResetEvent( initialState: false );
            m_stateInPool = SocketStateInPool.NeverWasInPool;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytesToSend"></param>
        /// <param name="timeoutToSend"></param>
        /// <param name="timeoutToConnect"></param>
        /// <param name="ioBehavior"></param>
        /// <param name="client"></param>
        /// <returns>
        /// If <paramref name="bytesToSend"/> is immediately sent, method will return <paramref name="client"/>, else it will return new created <see cref="ConnectionPoolSocket"/>
        /// </returns>
        public async Task<ConnectionPoolSocket> DsSendWithAvoidErrorsInNetworkAsync( Byte[] bytesToSend,
            TimeSpan timeoutToSend, TimeSpan timeoutToConnect, IOBehavior ioBehavior )
        {
            ConnectionPoolSocket sendingBytesSocket;
            try
            {
                await DsSendAsync( bytesToSend, timeoutToSend, ioBehavior ).ConfigureAwait( continueOnCapturedContext: false );
                sendingBytesSocket = this;
            }
            catch ( SocketException e )
            {
                Log.LogError( $"Failed to send message, try only one more: {e}" );

                sendingBytesSocket = new ConnectionPoolSocket( SocketType, ProtocolType, Id, Pool, Log );
                await sendingBytesSocket.DsConnectAsync( remoteEndPoint: sendingBytesSocket.Id, timeoutToConnect, ioBehavior ).ConfigureAwait( false );

                await sendingBytesSocket.DsSendAsync( bytesToSend, timeoutToSend, ioBehavior ).ConfigureAwait( false );
            }

            return sendingBytesSocket;
        }

        public UInt32 CreatedTicks { get; } = unchecked((UInt32)Environment.TickCount);

        public UInt32 LastReturnedTicks { get; private set; }

        public EndPoint Id { get; }

        public ConnectionPool Pool { get; }

        public SocketStateInPool StateInPool
        {
            get => m_stateInPool;
            set
            {
                lock ( m_removedFromPool )
                {
                    if(m_stateInPool == SocketStateInPool.NeverWasInPool)
                    {
                        m_stateInPool = value;
                    }
                    else
                    {
                        m_stateInPool = value;

                        if ( m_stateInPool == SocketStateInPool.TakenFromPool )
                        {
                            Boolean isReturned = m_removedFromPool.WaitOne( Constants.TimeWaitReturnToPool );
                            if ( isReturned )
                            {
                                Log.LogInfo( $"\n*************************\nSocket with id {Id} successfully taken from pool\n*************************\n" );
                            }
                            else
                            {
                                //case when BackgroundConnectionResetHelper wait to start finish reset connections
                                Log.LogError( $"\n*************************\nSocket with id {Id} isn\'t returned to pool by some thread\n*************************\n" );
                            }
                        }
                        else
                        {
                            //we don't use ReturnedInPool, otherwise we will have an deadlock
                            m_removedFromPool.Set();
                        }
                    }
                }
            }
        }

        public AutoResetEvent RemovedFromPool
        {
            get
            {
                lock( m_removedFromPool )
                {
                    return m_removedFromPool;
                }
            }
        }

        //public async Task ConnectAsync()
        //{
        //    //check status in lock
        //    //add changing status in all methods
        //    //take work with SslProtocols and TLS
        //    //maybe should to check whether remoteEndPoint is Windows
        //    //check whether remoteEndPoint supports SSL (send message to ask)
        //    //call ConnectAsync with timeout
        //    //change state to connected if it is, otherwise to failed
        //}

        public SocketHealth SocketHealth( ConnectionSettings connectionSettings )
        {
            SocketHealth socketHealth;

            try
            {
                VerifyConnected();
            }
            catch ( SocketException )
            {
                socketHealth = ClientPool.SocketHealth.IsNotConnected;
                return socketHealth;
            }

            if ( ( connectionSettings.ConnectionLifeTime > 0 ) &&
               ( unchecked((UInt32)Environment.TickCount) - CreatedTicks >= connectionSettings.ConnectionLifeTime ) )
            {
                socketHealth = ClientPool.SocketHealth.Expired;
            }
            else
            {
                socketHealth = ClientPool.SocketHealth.Healthy;
            }

            return socketHealth;
        }

        public Boolean ReturnedToPool()
        {
#if DEBUG
            {
                Log.LogInfo( $"Socket with id \"{Id}\" returning to Pool" );
            }
#endif
            LastReturnedTicks = unchecked((UInt32)Environment.TickCount);

            if ( Pool == null )
            {
                return false;
            }
            //we shouldn't check IsInPool because ConnectionPool.semaphoreSocket.Release needed to be called, 
            //because after long time ConnectionPool.semaphoreSocket.CurrentCount can be 0 without this, that will cause the recovering sockets without necessity
            Boolean isReturned = Pool.ReturnedToPool( Id );

            return isReturned;
        }

        public async Task<Boolean> TryRecoverConnectionAsync( Boolean returnToPool, Boolean reuseSocket, 
            IOBehavior ioBehavior, CancellationToken cancellationToken = default )
        {
            Boolean isRecoveredConnection = false;

            try
            {
                VerifyWorkState();

                await DsDisconnectAsync( ioBehavior, reuseSocket, Constants.DisconnectTimeout, cancellationToken ).
                        ConfigureAwait( continueOnCapturedContext: false );
            }
            catch(SocketException)
            {
                ;//do nothing
            }
            catch(TimeoutException)
            {
                ;//do nothing
            }
            finally
            {
                ConnectionPoolSocket newSocket = null;
                try
                {
                    newSocket = new ConnectionPoolSocket( SocketType, ProtocolType, Id, Pool, Log );

                    await newSocket.DsConnectAsync( Id, Constants.ConnectTimeout, ioBehavior, cancellationToken ).ConfigureAwait( false );

                    //if we don't recovered connection, we will have an exception
                    isRecoveredConnection = true;
                }
                catch ( SocketException )
                {
                    ;//do nothing
                }
                catch ( TimeoutException )
                {
                    ;//do nothing
                }
                finally
                {
                    if ( ( returnToPool ) && ( Pool != null ) )
                    {
                        newSocket.ReturnedToPool();
                    }
                }
            }

            return isRecoveredConnection;
        }

        public new void Dispose()
        {
            StateInPool = SocketStateInPool.IsFailed;
            base.Dispose();
        }
    }
}
