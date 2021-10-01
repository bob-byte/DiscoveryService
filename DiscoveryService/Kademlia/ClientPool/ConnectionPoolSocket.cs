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
        private Boolean m_isInPool = false;

        /// <inheritdoc/>
        public ConnectionPoolSocket( SocketType socketType, ProtocolType protocolType, EndPoint remoteEndPoint, ConnectionPool belongPool, ILoggingService log )
            : base( remoteEndPoint.AddressFamily, socketType, protocolType, log )
        {
            Id = remoteEndPoint;
            Pool = belongPool;
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

        public Boolean IsInPool
        {
            get => m_isInPool;
            //maybe body of set method should be placed in a lock
            set
            {
                lock(ReturnedInPool)
                {
                    m_isInPool = value;

                    if ( ( !m_isInPool ) && ( ReturnedInPool.CurrentCount == 1 ) )
                    {
                        ReturnedInPool.Wait( millisecondsTimeout: 0 );
                    }
                    else if ( ( m_isInPool ) && ( ReturnedInPool.CurrentCount == 0 ) )
                    {
                        ReturnedInPool.Release();
                    }
                }
            }
        }

        public SemaphoreSlim ReturnedInPool { get; } = new SemaphoreSlim( initialCount: 0, maxCount: 1 );

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
            IsInPool = isReturned;

            return isReturned;
        }

        public async Task<Boolean> TryRecoverConnectionAsync( Boolean returnToPool, Boolean reuseSocket, IOBehavior ioBehavior )
        {
            VerifyWorkState();

            try
            {
                await DsDisconnectAsync( ioBehavior, reuseSocket, Constants.DisconnectTimeout ).
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

            ConnectionPoolSocket newSocket = new ConnectionPoolSocket( SocketType, ProtocolType, Id, Pool, Log );
            Boolean isRecoveredConnection = false;

            try
            {
                await newSocket.DsConnectAsync( Id, Constants.ConnectTimeout, ioBehavior ).ConfigureAwait( false );

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

            return isRecoveredConnection;
        }
    }
}
