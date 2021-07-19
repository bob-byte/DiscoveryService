using LUC.Interfaces;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia.Protocols.Tcp
{
    sealed class ConnectionPool
    {
        private const UInt32 PoolRecoveryFrequencyInMs = 1000;

        private static ConnectionPool instance;
        private readonly ILoggingService log;

        private readonly SemaphoreSlim cleanSemaphore;
        private readonly SemaphoreSlim socketSemaphore;
        private readonly SemaphoreLocker lockLeasedSockets;

        private readonly Dictionary<EndPoint, SocketInConnectionPool> sockets;
        private readonly Dictionary<EndPoint, SocketInConnectionPool> leasedSockets;

        private UInt32 lastRecoveryTime;

        static ConnectionPool()
        {
            AppDomain.CurrentDomain.DomainUnload += CleanPool;
            AppDomain.CurrentDomain.ProcessExit += CleanPool;
        }

        private static void CleanPool(Object sender, EventArgs e)
        {
            instance?.ClearPoolAsync(IOBehavior.Synchronous, respectMinPoolSize: false, CancellationToken.None).GetAwaiter().GetResult();
            //BackgroundConnectionResetHelper.Stop();
        }

        private ConnectionPool(ConnectionSettings connectionSettings, ILoggingService loggingService)
        {
            log = loggingService;
            ConnectionSettings = connectionSettings;

            cleanSemaphore = new SemaphoreSlim(initialCount: 1);
            socketSemaphore = new SemaphoreSlim(connectionSettings.MaximumPoolSize);

            sockets = new Dictionary<EndPoint, SocketInConnectionPool>();
            leasedSockets = new Dictionary<EndPoint, SocketInConnectionPool>();
            lockLeasedSockets = new SemaphoreLocker();
        }

        public ConnectionSettings ConnectionSettings { get; }

        /// <summary>
		/// Returns <c>true</c> if the connection pool is empty, i.e., all connections are in use. Note that in a highly-multithreaded
		/// environment, the value of this property may be stale by the time it's returned.
		/// </summary>
		internal Boolean IsEmpty => socketSemaphore.CurrentCount == 0;

        public static ConnectionPool Instance(ILoggingService loggingService)
        {
            if(instance == null)
            {
                instance = new ConnectionPool(new ConnectionSettings(), loggingService);
            }

            return instance;
        }

        public async ValueTask<SocketInConnectionPool> SocketAsync(EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, TimeSpan timeWaitToReturnToPool)
        {
            //cancellationToken.ThrowIfCancellationRequested();

            if (IsEmpty && (unchecked(((UInt32)Environment.TickCount) - lastRecoveryTime) >= PoolRecoveryFrequencyInMs))
            {
                log.LogInfo("Pool is empty; recovering leaked sessions");

                await RecoverLeakedSocketsAsync(ioBehavior, timeoutToConnect).ConfigureAwait(continueOnCapturedContext: false);
            }

            // wait for an open slot (until the cancellationToken is cancelled, which is typically due to timeout)
#if DEBUG
            log.LogInfo("Pool waiting for a taking from the pool");
#endif
            if (ioBehavior == IOBehavior.Asynchronous)
            {
                await socketSemaphore.WaitAsync(timeWaitToReturnToPool).ConfigureAwait(false);
            }
            else
            {
                socketSemaphore.Wait(timeWaitToReturnToPool);
            }

            SocketInConnectionPool desiredSocket = null;
            //if IOBehavior.Synchronous then use simple lock without await desiredSocket.ReturnedInPool.WaitAsync 
            await lockLeasedSockets.LockAsync(async () =>
            {
                if (leasedSockets.ContainsKey(remoteEndPoint))
                {
                    desiredSocket = leasedSockets[remoteEndPoint];

                    Boolean isReturned = false;
                    //wait in different way
                    if (ioBehavior == IOBehavior.Asynchronous)
                    {
                        isReturned = await desiredSocket.ReturnedInPool.WaitAsync(timeWaitToReturnToPool);
                    }
                    else if (ioBehavior == IOBehavior.Synchronous)
                    {
                        isReturned = desiredSocket.ReturnedInPool.Wait(timeWaitToReturnToPool);
                    }

                    if(!isReturned)
                    {
                        desiredSocket = null;
                    }
                    desiredSocket = await ConnectedSocketAsync(remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket).ConfigureAwait(false);
                }
            });

            if(desiredSocket != null)
            {
                return desiredSocket;
            }

            Boolean isInPool = sockets.ContainsKey(remoteEndPoint);
            if (isInPool)
            {
                lock (sockets)
                {
                    desiredSocket = sockets[remoteEndPoint];
                    sockets.Remove(remoteEndPoint);
                }

                desiredSocket = await ConnectedSocketAsync(remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket).ConfigureAwait(false);
            }
            else
            {
                desiredSocket = new SocketInConnectionPool(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp, remoteEndPoint, instance, log);
                await ConnectInDifferentWayAsync(desiredSocket, remoteEndPoint, timeoutToConnect, ioBehavior);
            }

            lock (leasedSockets)
            {
                leasedSockets.Add(remoteEndPoint, desiredSocket);
                desiredSocket.IsInPool = false;
            }

            return desiredSocket;
        }

        private async ValueTask<SocketInConnectionPool> ConnectedSocketAsync(EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, SocketInConnectionPool socket)
        {
            var connectedSocket = socket;
            if (socket != null)
            {
                try
                {
                    connectedSocket.VerifyConnected();
                }
                catch
                {
                    try
                    {
                        await ConnectInDifferentWayAsync(connectedSocket, remoteEndPoint, timeoutToConnect, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
                    }
                    catch
                    {
                        try
                        {
                            connectedSocket = new SocketInConnectionPool(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp, remoteEndPoint, instance, log);
                            await ConnectInDifferentWayAsync(connectedSocket, remoteEndPoint, timeoutToConnect, ioBehavior).ConfigureAwait(false);
                        }
                        catch(Exception ex)
                        {
                            log.LogError(ex.Message);
                        }
                    }
                }
            }
            else
            {
                connectedSocket = new SocketInConnectionPool(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp, remoteEndPoint, instance, log);
                await ConnectInDifferentWayAsync(connectedSocket, remoteEndPoint, timeoutToConnect, ioBehavior);
            }

            return connectedSocket;
        }

        private async ValueTask ConnectInDifferentWayAsync(SocketInConnectionPool socket, EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior)
        {
            Boolean isConnected;
            if (ioBehavior == IOBehavior.Asynchronous)
            {
                isConnected = await socket.ConnectAsync(remoteEndPoint, timeoutToConnect).ConfigureAwait(continueOnCapturedContext: false);
            }
            else if (ioBehavior == IOBehavior.Synchronous)
            {
                socket.Connect(remoteEndPoint, timeoutToConnect, out isConnected);
            }
            else
            {
                throw new ArgumentException($"{ioBehavior} has incorrect value");
            }

            if(!isConnected)
            {
                throw new SocketException((Int32)SocketError.ConnectionReset);
            }
        }

        /// <summary>
		/// Examines all the <see cref="ServerSession"/> objects in <see cref="leasedSockets"/> to determine if any
		/// have an owning <see cref="MySqlConnection"/> that has been garbage-collected. If so, assumes that the connection
		/// was not properly disposed and returns the session to the pool.
		/// </summary>
		private async ValueTask RecoverLeakedSocketsAsync(IOBehavior ioBehavior, TimeSpan timeoutToConnect)
        {
            var recoveredSockets = new List<SocketInConnectionPool>();
            await lockLeasedSockets.LockAsync(async () =>
            {
                lastRecoveryTime = unchecked((UInt32)Environment.TickCount);

                foreach (var socket in leasedSockets.Values)
                {
                    var recoveredSocket = await ConnectedSocketAsync(socket.Id, timeoutToConnect, ioBehavior, socket).ConfigureAwait(continueOnCapturedContext: false);
                    try
                    {
                        recoveredSocket.VerifyConnected();
                    }
                    catch
                    {
                        recoveredSockets.Add(socket);
                    }
                }
            });

            if(recoveredSockets.Count == 0)
            {
#if DEBUG
                log.LogInfo($"Pool recovered no sockets");
#endif
            }
            else
            {
                log.LogInfo($"Pool{0}: RecoveredSessionCount = {recoveredSockets.Count}");
            }

            foreach (var socket in recoveredSockets)
            {
                await socket.ReturnToPoolAsync(IOBehavior.Synchronous).ConfigureAwait(continueOnCapturedContext: false);
            }
        }

        public async ValueTask<Boolean> ReturnToPoolAsync(IOBehavior ioBehavior, EndPoint remoteEndPoint)
        {
#if DEBUG
            log.LogInfo($"Pool receiving Session with id {remoteEndPoint} back");
#endif

            Boolean isReturned = false;
            try
            {
                Boolean wasInPool = false;
                SocketInConnectionPool socketInPool = null;
                await lockLeasedSockets.LockAsync(() =>
                {
                    wasInPool = leasedSockets.ContainsKey(remoteEndPoint);

                    if (wasInPool)
                    {
                        socketInPool = leasedSockets[remoteEndPoint];
                        leasedSockets.Remove(remoteEndPoint);
                    }

                    return Task.CompletedTask;
                });

                if(wasInPool)
                {                 
                    var socketHealth = SocketHealth(socketInPool);
                    if (socketHealth == Tcp.SocketHealth.Healthy)
                    {
                        lock(sockets)
                        {
                            sockets.Add(remoteEndPoint, socketInPool);
                            socketInPool.IsInPool = true;
                            isReturned = true;
                        }
                    }
                    else
                    {
                        if (socketHealth == Tcp.SocketHealth.IsNotConnected)
                        {
                            log.LogInfo($"Pool received invalid Socket {socketInPool.Id}; destroying it");
                        }
                        else if(socketHealth == Tcp.SocketHealth.Expired)
                        {
                            log.LogInfo($"Pool received expired Socket {socketInPool.Id}; destroying it");
                        }
                        socketInPool.Dispose();

                        isReturned = false;
                    }
                }
                else
                {
                    isReturned = false;
                }
            }
            catch(Exception ex)
            {
                isReturned = false;
                log.LogError(ex, ex.Message);
            }
            finally
            {
                socketSemaphore.Release();
            }

            return isReturned;
        }

        private SocketHealth SocketHealth(SocketInConnectionPool socket)
        {
            SocketHealth socketHealth;

            try
            {
                socket.VerifyConnected();
            }
            catch
            {
                socketHealth = Tcp.SocketHealth.IsNotConnected;
                return socketHealth;
            }

            if((ConnectionSettings.ConnectionLifeTime > 0) && 
               (unchecked((UInt32)Environment.TickCount) - socket.CreatedTicks >= ConnectionSettings.ConnectionLifeTime))
            {
                socketHealth = Tcp.SocketHealth.Expired;
            }
            else
            {
                socketHealth = Tcp.SocketHealth.Healthy;
            }

            return socketHealth;
        }

        public async Task ClearPoolAsync(IOBehavior ioBehavior, bool respectMinPoolSize, CancellationToken cancellationToken)
        {
            log.LogInfo($"Pool clearing connection pool");

            // synchronize access to this method as only one clean routine should be run at a time
            if (ioBehavior == IOBehavior.Asynchronous)
            {
                await cleanSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                cleanSemaphore.Wait(cancellationToken);
            }

            try
            {
                var waitTimeout = TimeSpan.FromMilliseconds(10);
                while (true)
                {
                    // if respectMinPoolSize is true, return if (leased sessions + waiting sessions <= minPoolSize)
                    if (respectMinPoolSize)
                    {
                        lock (sockets)
                        {
                            if (ConnectionSettings.MaximumPoolSize - socketSemaphore.CurrentCount + sockets.Count <= ConnectionSettings.MinimumPoolSize)
                            {
                                return;
                            }
                        }
                    }

                    // try to get an open slot; if this fails, connection pool is full and sessions will be disposed when returned to pool
                    if (ioBehavior == IOBehavior.Asynchronous)
                    {
                        if (!await socketSemaphore.WaitAsync(waitTimeout, cancellationToken).ConfigureAwait(false))
                        {
                            return;
                        }
                    }
                    else
                    {
                        if (!socketSemaphore.Wait(waitTimeout, cancellationToken))
                        {
                            return;
                        }
                    }

                    try
                    {
                        // check for a waiting session
                        lock (sockets)
                        {
                            var waitingSocket = sockets.FirstOrDefault();
                            if(!waitingSocket.Equals(default(KeyValuePair<EndPoint, SocketInConnectionPool>)))
                            {
                                waitingSocket.Value.Dispose();
                            }
                        }
                    }
                    finally
                    {
                        socketSemaphore.Release();
                    }
                }
            }
            finally
            {
                cleanSemaphore.Release();
            }
        }
    }
}
