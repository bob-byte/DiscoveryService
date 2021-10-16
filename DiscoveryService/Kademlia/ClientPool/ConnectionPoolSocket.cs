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
        private Object m_lockStateInPool;

        /// <inheritdoc/>
        public ConnectionPoolSocket( EndPoint remoteEndPoint, ConnectionPool belongPool, ILoggingService log, SocketStateInPool socketStateInPool = SocketStateInPool.NeverWasInPool )
            : base( remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp, log )
        {
            Id = remoteEndPoint;
            Pool = belongPool;

            RemovedFromPool = new AutoResetEvent( initialState: false );
            m_stateInPool = socketStateInPool;
            m_lockStateInPool = new Object();
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

                sendingBytesSocket = new ConnectionPoolSocket( Id, Pool, Log, SocketStateInPool.TakenFromPool );
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
            get
            {
                lock(m_lockStateInPool)
                {
                    return m_stateInPool;
                }
            }
            set
            {
                SocketStateInPool previousState;
                lock (m_lockStateInPool)
                {
                    previousState = m_stateInPool;
                    m_stateInPool = value;

                    if ( previousState == SocketStateInPool.NeverWasInPool )
                    {
                        return;
                    }
                }

                if ( value == SocketStateInPool.TakenFromPool )
                {
                    Boolean isReturned = RemovedFromPool.WaitOne( Constants.TimeWaitReturnToPool );
                    if ( isReturned )
                    {
                        Log.LogInfo( $"\n*************************\nSocket with id {Id} successfully taken from pool\n*************************\n" );
                    }
                    else
                    {
#if DEBUG
                        ThrowConcurrencyException.ThrowWithConnectionPoolSocketDescr( this );
#else
                        Log.LogError( Display.StringWithAttention( logRecord: $"Socket with id {Id} isn\'t returned to pool by some thread" );
#endif
                    }
                }
                else if ( value != SocketStateInPool.NeverWasInPool )//it is additional test to make this method absolutely thread-safe if logic will be changed
                {
                    RemovedFromPool.Set();
                }
            }
        }

        public AutoResetEvent RemovedFromPool { get; }

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

            Boolean hasLongBeenCreated = unchecked((UInt32)Environment.TickCount) - CreatedTicks >= connectionSettings.ConnectionLifeTime;
            if ( ( ( m_state >= SocketState.Closing ) && ( m_stateInPool != SocketStateInPool.IsFailed ) ) || 
                 ( ( connectionSettings.ConnectionLifeTime > 0 ) && ( hasLongBeenCreated ) ) )
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
                VerifyConnected();

                await DsDisconnectAsync( ioBehavior, reuseSocket, Constants.DisconnectTimeout, cancellationToken );
            }
            catch(SocketException)
            {
                ;//do nothing
            }
            catch(TimeoutException)
            {
                ;//do nothing
            }
            catch ( ObjectDisposedException )
            {
                ;//do nothing
            }
            finally
            {
                ConnectionPoolSocket newSocket = null;
                try
                {
                    newSocket = new ConnectionPoolSocket( Id, Pool, Log, SocketStateInPool.TakenFromPool );

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
                catch ( ObjectDisposedException )
                {
                    ;//do nothing
                }
                finally
                {
                    if ( ( !cancellationToken.IsCancellationRequested ) && ( returnToPool ) )
                    {
                        newSocket?.ReturnedToPool();
                    }
                    else if ( cancellationToken.IsCancellationRequested )
                    {
                        StateInPool = SocketStateInPool.IsFailed;
                    }
                }
            }

            return isRecoveredConnection;
        }

        public new void Dispose()
        {
            StateInPool = SocketStateInPool.IsFailed;
            m_state = SocketState.Closing;
            base.Dispose( disposing: false );
            m_state = SocketState.Closed;
        }
    }
}
