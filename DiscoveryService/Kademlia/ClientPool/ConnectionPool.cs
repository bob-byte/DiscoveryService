using LUC.DiscoveryService.Common;
using LUC.Interfaces;
using LUC.Services.Implementation;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia.ClientPool
{
    sealed class ConnectionPool
    {
        private const UInt32 PoolRecoveryFrequencyInMs = 1000;

        private static ConnectionPool instance;
        private readonly ILoggingService log;

        private readonly SemaphoreSlim cleanSemaphore;
        private readonly SemaphoreSlim socketSemaphore;
        private readonly SemaphoreLocker lockLeasedSockets;

        private readonly Dictionary<EndPoint, ConnectionPoolSocket> sockets;
        private readonly Dictionary<EndPoint, ConnectionPoolSocket> leasedSockets;

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

        private ConnectionPool(ConnectionSettings connectionSettings)
        {
            log = new LoggingService
            {
                SettingsService = new SettingsService()
            };
            ConnectionSettings = connectionSettings;

            cleanSemaphore = new SemaphoreSlim(initialCount: 1);
            socketSemaphore = new SemaphoreSlim(connectionSettings.MaximumPoolSize);

            sockets = new Dictionary<EndPoint, ConnectionPoolSocket>();
            leasedSockets = new Dictionary<EndPoint, ConnectionPoolSocket>();
            lockLeasedSockets = new SemaphoreLocker();
        }

        public ConnectionSettings ConnectionSettings { get; }

        /// <summary>
		/// Returns <c>true</c> if the connection pool is empty, i.e., all connections are in use. Note that in a highly-multithreaded
		/// environment, the value of this property may be stale by the time it's returned.
		/// </summary>
		internal Boolean IsEmpty => socketSemaphore.CurrentCount == 0;

        public static ConnectionPool Instance()
        {
            if(instance == null)
            {
                instance = new ConnectionPool(new ConnectionSettings());
            }

            return instance;
        }

        public async ValueTask<ConnectionPoolSocket> SocketAsync(EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, TimeSpan timeWaitToReturnToPool)
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

            ConnectionPoolSocket desiredSocket = null;
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

            Boolean takenFromPool = desiredSocket != null;
            if (!takenFromPool)
            {
                Boolean isInPool;
                
                lock (sockets)
                {
                    isInPool = sockets.ContainsKey(remoteEndPoint);

                    if(isInPool)
                    {
                        desiredSocket = sockets[remoteEndPoint];
                        sockets.Remove(remoteEndPoint);
                    }
                }

                if (isInPool)
                {
                    desiredSocket = await ConnectedSocketAsync(remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket).ConfigureAwait(false);
                }
                else
                {
                    desiredSocket = new ConnectionPoolSocket(remoteEndPoint.AddressFamily, SocketType.Stream, 
                        ProtocolType.Tcp, remoteEndPoint, instance, log);
                    await ConnectInDifferentWayAsync(desiredSocket, remoteEndPoint, timeoutToConnect, ioBehavior);
                }

                lock (leasedSockets)
                {
                    if(!leasedSockets.ContainsKey(remoteEndPoint))
                    {
                        leasedSockets.Add(remoteEndPoint, desiredSocket);
                    }

                    desiredSocket.IsInPool = false;
                }
            }

            return desiredSocket;
        }

        private async ValueTask<ConnectionPoolSocket> ConnectedSocketAsync(EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, ConnectionPoolSocket socket)
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
                        connectedSocket = new ConnectionPoolSocket(remoteEndPoint.AddressFamily, SocketType.Stream,ProtocolType.Tcp, remoteEndPoint, instance, log);
                        await ConnectInDifferentWayAsync(connectedSocket, remoteEndPoint, timeoutToConnect, ioBehavior).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                connectedSocket = new ConnectionPoolSocket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp, remoteEndPoint, instance, log);
                await ConnectInDifferentWayAsync(connectedSocket, remoteEndPoint, timeoutToConnect, ioBehavior);
            }

            return connectedSocket;
        }

        private async ValueTask ConnectInDifferentWayAsync(ConnectionPoolSocket socket, EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior)
        {
            if (ioBehavior == IOBehavior.Asynchronous)
            {
                await socket.ConnectAsync(remoteEndPoint, timeoutToConnect).ConfigureAwait(continueOnCapturedContext: false);
            }
            else if (ioBehavior == IOBehavior.Synchronous)
            {
                socket.Connect(remoteEndPoint, timeoutToConnect);
            }
            else
            {
                throw new ArgumentException($"{ioBehavior} has incorrect value");
            }
        }

        /// <summary>
		/// Examines all the <see cref="ServerSession"/> objects in <see cref="leasedSockets"/> to determine if any
		/// have an owning <see cref="MySqlConnection"/> that has been garbage-collected. If so, assumes that the connection
		/// was not properly disposed and returns the session to the pool.
		/// </summary>
		private async ValueTask RecoverLeakedSocketsAsync(IOBehavior ioBehavior, TimeSpan timeoutToConnect)
        {
            var recoveredSockets = new List<ConnectionPoolSocket>();
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
                ConnectionPoolSocket socketInPool = null;
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
                    if (socketHealth == ClientPool.SocketHealth.Healthy)
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
                        if (socketHealth == ClientPool.SocketHealth.IsNotConnected)
                        {
                            log.LogInfo($"Pool received invalid Socket {socketInPool.Id}; destroying it");
                        }
                        else if(socketHealth == ClientPool.SocketHealth.Expired)
                        {
                            log.LogInfo($"Pool received expired Socket {socketInPool.Id}; destroying it");
                        }

                        if(socketInPool.State != SocketState.Closed)
                        {
                            socketInPool.Dispose();
                        }

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

        private SocketHealth SocketHealth(ConnectionPoolSocket socket)
        {
            SocketHealth socketHealth;

            try
            {
                socket.VerifyConnected();
            }
            catch
            {
                socketHealth = ClientPool.SocketHealth.IsNotConnected;
                return socketHealth;
            }

            if((ConnectionSettings.ConnectionLifeTime > 0) && 
               (unchecked((UInt32)Environment.TickCount) - socket.CreatedTicks >= ConnectionSettings.ConnectionLifeTime))
            {
                socketHealth = ClientPool.SocketHealth.Expired;
            }
            else
            {
                socketHealth = ClientPool.SocketHealth.Healthy;
            }

            return socketHealth;
        }

        public async Task ClearPoolAsync(IOBehavior ioBehavior, bool respectMinPoolSize, CancellationToken cancellationToken)
        {
            log.LogInfo($"Pool clearing connection pool");

            // synchronize access to this method as only one clean routine should be run at a time
            if (ioBehavior == IOBehavior.Asynchronous)
            {
                await cleanSemaphore.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            }
            else
            {
                cleanSemaphore.Wait(cancellationToken);
            }

            try
            {
                var waitTimeout = TimeSpan.FromMilliseconds(value: 10);
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
                            if(!waitingSocket.Equals(default(KeyValuePair<EndPoint, ConnectionPoolSocket>)))
                            {
                                waitingSocket.Value.Dispose();
                            }
                            else
                            {
                                return;
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
