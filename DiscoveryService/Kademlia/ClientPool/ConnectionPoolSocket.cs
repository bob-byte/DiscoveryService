using LUC.DiscoveryService.Extensions;
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
    class ConnectionPoolSocket : DiscoveryServiceSocket
    {
        private readonly Object m_lock = new Object();
        private SocketState m_state;
        private Boolean isInPool = false;

        /// <inheritdoc/>
        public ConnectionPoolSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, EndPoint remoteEndPoint, ConnectionPool belongPool, ILoggingService log)
            : base(addressFamily, socketType, protocolType, log)
        {
            Id = remoteEndPoint;
            Pool = belongPool;
        }

        public UInt32 CreatedTicks { get; } = unchecked((UInt32)Environment.TickCount);

        public UInt32 LastReturnedTicks { get; private set; }

        public EndPoint Id { get; }

        public ConnectionPool Pool { get; }

        public Boolean IsInPool 
        {
            get => isInPool;
            //maybe body of set method should be placed in a lock
            set
            {
                isInPool = value;

                if((!isInPool) && (ReturnedInPool.CurrentCount == 1))
                {
                    ReturnedInPool.Wait(millisecondsTimeout: 0);
                }
                else if((isInPool) && (ReturnedInPool.CurrentCount == 0))
                {
                    ReturnedInPool.Release();
                }
            }
        }

        public SemaphoreSlim ReturnedInPool { get; } = new SemaphoreSlim(initialCount: 0, maxCount: 1);

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

        public async ValueTask<Boolean> ReturnToPoolAsync(IOBehavior ioBehavior)
        {
#if DEBUG
            {
                Log.LogInfo($"Socket with id \"{Id}\" returning to Pool");
            }
#endif
            LastReturnedTicks = unchecked((uint)Environment.TickCount);

            if(Pool == null)
            {
                return false;
            }
            else if (!Pool.ConnectionSettings.ConnectionReset || Pool.ConnectionSettings.DeferConnectionReset)
            {
                return await Pool.ReturnToPoolAsync(ioBehavior, RemoteEndPoint).ConfigureAwait(continueOnCapturedContext: false);
            }
            else
            {
                BackgroundConnectionResetHelper.AddSocket(this, Log);
                return false;
            }
        }

        public new void Dispose()
        {
            // attempt to gracefully close the connection, ignoring any errors (it may have been closed already by the server, etc.)
            SocketState state;
            lock (m_lock)
            {
                if (m_state == SocketState.Connected || m_state == SocketState.Failed)
                {
                    m_state = SocketState.Closing;
                }

                state = m_state;
            }

            ShutdownSocket();
            lock (m_lock)
            {
                m_state = SocketState.Closed;
            }
        }

        public async Task<Boolean> TryResetConnectionAsync(Boolean returnToPool, Boolean reuseSocket, IOBehavior ioBehavior)
        {
            if (returnToPool && Pool != null)
            {
                await Pool.ReturnToPoolAsync(ioBehavior, RemoteEndPoint).ConfigureAwait(continueOnCapturedContext: false);
            }

            var waitIndefinitely = TimeSpan.FromMilliseconds(value: -1);
            var success = await DisconnectAsync(reuseSocket, waitIndefinitely).ConfigureAwait(continueOnCapturedContext: false);

            return success;
        }

        private void ShutdownSocket()
        {
            Log.LogInfo($"Closing socket with id \"{Id}\"");
            try
            {
                Shutdown(SocketShutdown.Both);
            }
            finally
            {
                Dispose(disposing: true);
            }
        }

        /// <summary>
	/// Disposes and sets <paramref name="disposable"/> to <c>null</c>, ignoring any
	/// <see cref="IOException"/> or <see cref="SocketException"/> that is thrown.
	/// </summary>
	/// <typeparam name="T">An <see cref="IDisposable"/> type.</typeparam>
	/// <param name="disposable">The object to dispose.</param>
	private static void SafeDispose<T>(ref T disposable)
            where T : class, IDisposable
        {
            if (disposable != null)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (IOException)
                {
                }
                catch (SocketException)
                {
                }
                disposable = null;
            }
        }
    }
}
