﻿using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Messages;
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

namespace LUC.DiscoveryServices.Kademlia.ClientPool
{
    /// <summary>
    /// Client socket, maintained by the Connection Pool
    /// </summary>
    class ConnectionPoolSocket : AsyncSocket
    {
        private readonly Object m_lockStateInPool;

        private readonly AutoResetEvent m_canBeTakenFromPool;

        private SocketStateInPool m_stateInPool;

        /// <inheritdoc/>
        public ConnectionPoolSocket( EndPoint remoteEndPoint, ConnectionPool belongPool, ILoggingService log, SocketStateInPool socketStateInPool = SocketStateInPool.NeverWasInPool )
            : base( remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp, log )
        {
            Id = remoteEndPoint;
            Pool = belongPool;

            m_canBeTakenFromPool = new AutoResetEvent( initialState: false );

            //to always allow init ConnectionPoolSocket.StateInPool without waiting(see method StateInPool.set)
            m_stateInPool = SocketStateInPool.NeverWasInPool;

            //it should be initialized before StateInPool property
            m_lockStateInPool = new Object();
            StateInPool = socketStateInPool;
        }

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

        public EndPoint Id { get; }

        public ConnectionPool Pool { get; }

        public SocketStateInPool StateInPool
        {
            get => m_stateInPool;

            set
            {
                lock ( m_lockStateInPool )
                {
                    SocketStateInPool previousState = m_stateInPool;

                    //if socket was created in pool and taken from there
                    //((previousState == SocketStateInPool.NeverWasInPool) && (value == SocketStateInPool.TakenFromPool)),
                    //then of course we don't have to wait for it to come back to the pool
                    if ( previousState == SocketStateInPool.NeverWasInPool || value != SocketStateInPool.TakenFromPool )
                    {
                        m_stateInPool = value;

                        if ( previousState == SocketStateInPool.NeverWasInPool )
                        {
                            return;
                        }
                    }
                }

                if ( value == SocketStateInPool.TakenFromPool )
                {
                    Boolean isReturned;
                    //every thread should wait the same time from point when socket is taken(e.g. if two threads wait to take socket when anyone did that, the next one will keep waiting and it will be less than Constants.TimeWaitSocketReturnedToPool)
                    lock ( m_canBeTakenFromPool)
                    {
                        isReturned = m_canBeTakenFromPool.WaitOne( Constants.TimeWaitSocketReturnedToPool );
                    }

                    lock ( m_lockStateInPool )
                    {
                        m_stateInPool = value;
                    }

                    if ( isReturned )
                    {
                        Log.LogInfo( logRecord: Display.StringWithAttention( $"Socket with id {Id} successfully taken from pool" ) );
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
                    m_canBeTakenFromPool.Set();
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

        public override Boolean Equals( Object obj )
        {
            Boolean isEqual;
            if(obj is ConnectionPoolSocket socket)
            {
                isEqual = Id.Equals( socket.Id );
            }
            else
            {
                isEqual = false;
            }

            return isEqual;
        }

        public override Int32 GetHashCode() => 
            Id.GetHashCode();

        public SocketHealth SocketHealth()
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
            catch(ObjectDisposedException)
            {
                ;//do nothing, because check whether it is disposed is in ( m_state >= SocketState.Failed )
            }

            if ( ( m_stateInPool == SocketStateInPool.IsFailed ) || ( m_state >= SocketState.Failed ) )
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
