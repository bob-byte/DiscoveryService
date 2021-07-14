using LUC.Interfaces;
using System;
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
    internal sealed class ConnectionPool
    {
        private static ConnectionPool instance;
        private ILoggingService log;
        private UInt32 lastRecoveryTime;

        readonly SemaphoreSlim m_cleanSemaphore;
        readonly SemaphoreSlim m_sessionSemaphore;
        readonly ConcurrentDictionary<EndPoint, SocketInConnetionPool> m_sessions;

        readonly ConcurrentDictionary<EndPoint, SocketInConnetionPool> m_leasedSessions;
        readonly Dictionary<string, int> m_hostSessions;
        readonly object[] m_logArguments;
        Task m_reaperTask;
        uint m_lastRecoveryTime;
        int m_lastSessionId;
        //Dictionary<string, CachedProcedure?>? m_procedureCache;

        //static ConnectionPool()
        //{
        //    AppDomain.CurrentDomain.DomainUnload += OnAppDomainShutDown;
        //    AppDomain.CurrentDomain.ProcessExit += OnAppDomainShutDown;
        //}

        private ConnectionPool(ConnectionSettings connectionSettings, ILoggingService loggingService)
        {
            log = loggingService;
            ConnectionSettings = connectionSettings;

            m_cleanSemaphore = new SemaphoreSlim(initialCount: 1);
            m_sessionSemaphore = new SemaphoreSlim(connectionSettings.MaximumPoolSize);

            m_sessions = new ConcurrentDictionary<EndPoint, SocketInConnetionPool>();
            m_leasedSessions = new ConcurrentDictionary<EndPoint, SocketInConnetionPool>();

        }

        public ConnectionSettings ConnectionSettings { get; }

        /// <summary>
		/// Returns <c>true</c> if the connection pool is empty, i.e., all connections are in use. Note that in a highly-multithreaded
		/// environment, the value of this property may be stale by the time it's returned.
		/// </summary>
		internal bool IsEmpty => m_sessionSemaphore.CurrentCount == 0;

        //static void OnAppDomainShutDown(Object sender, EventArgs eventArguments)
        //{
        //    ClearPoolAsync(IOBehavior.Synchronous)
        //    BackgroundConnectionResetHelper.Stop();
        //}

        public static ConnectionPool Instance(ILoggingService loggingService)
        {
            if(instance == null)
            {
                instance = new ConnectionPool(new ConnectionSettings(), loggingService);
            }

            return instance;
        }

        public async ValueTask<SocketInConnetionPool> SocketAsync(EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsEmpty && unchecked(((UInt32)Environment.TickCount) - m_lastRecoveryTime) >= 1000u)
            {
                log.LogInfo("Pool is empty; recovering leaked sessions");

                await RecoverLeakedSocketsAsync(ioBehavior, timeoutToConnect).ConfigureAwait(continueOnCapturedContext: false);
            }

            // wait for an open slot (until the cancellationToken is cancelled, which is typically due to timeout)
#if DEBUG
            log.LogInfo("Pool waiting for an available session");
#endif
            if (ioBehavior == IOBehavior.Asynchronous)
            {
                await m_sessionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                m_sessionSemaphore.Wait(cancellationToken);
            }

            //try get from dict
            if(m_sessions.TryRemove(remoteEndPoint, out var desiredSocket))
            {
                if(!desiredSocket.Connected)
                {
                    await Connect(desiredSocket, remoteEndPoint, timeoutToConnect, ioBehavior);
                }
            }
            else
            {
                desiredSocket = new SocketInConnetionPool(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp, remoteEndPoint, instance, log);
                await Connect(desiredSocket, remoteEndPoint, timeoutToConnect, ioBehavior);
            }

            m_leasedSessions.TryAdd(remoteEndPoint, desiredSocket);

            return desiredSocket;
            //if it is in pool, delete from dict m_sessions and check connection
            //if not - create, add to dict m_sessions and connect to remoteEndPoint
        }

        private async ValueTask Connect(SocketInConnetionPool socket, EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior)
        {
            Boolean isConnected;
            if(ioBehavior == IOBehavior.Asynchronous)
            {
                isConnected = await socket.ConnectAsync(remoteEndPoint, timeoutToConnect).ConfigureAwait(continueOnCapturedContext: false);
            }
            else if(ioBehavior == IOBehavior.Synchronous)
            {
                socket.Connect(remoteEndPoint, timeoutToConnect, out isConnected);
            }
            else
            {
                throw new ArgumentException($"ioBehavior has incorrect value");
            }

            if (!isConnected)
            {
                throw new TimeoutException($"Timeout to connect to {remoteEndPoint}");
            }
        }

        /// <summary>
		/// Examines all the <see cref="ServerSession"/> objects in <see cref="m_leasedSessions"/> to determine if any
		/// have an owning <see cref="MySqlConnection"/> that has been garbage-collected. If so, assumes that the connection
		/// was not properly disposed and returns the session to the pool.
		/// </summary>
		private async Task RecoverLeakedSocketsAsync(IOBehavior ioBehavior, TimeSpan timeoutToConnect)
        {
            var recoveredSockets = new List<SocketInConnetionPool>();
            lock (m_leasedSessions)
            {
                m_lastRecoveryTime = unchecked((uint)Environment.TickCount);

                foreach (var socket in m_leasedSessions.Values)
                {
                    if (!socket.Connected)
                    {
                        socket.Connect(m_leasedSessions.Single(c => c.Value.Id.ToString() == socket.Id.ToString()).Key, timeoutToConnect, out var isConnected);

                        if(isConnected)
                        {
                            recoveredSockets.Add(socket);
                        }
                    }
                }
            }

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
                socket.ReturnToPool(ioBehavior, out _);
            }
        }

        

#if NET45 || NET461 || NET471 || NETSTANDARD1_3 || NETSTANDARD2_0
		public async ValueTask<int> ReturnAsync(IOBehavior ioBehavior, ServerSession session)
#else
        public void ReturnToPool(IOBehavior ioBehavior, EndPoint remoteEndPoint, out Boolean isReturned)
#endif
        {

            //if (Log.IsDebugEnabled())
            //    Log.Debug("Pool{0} receiving Session{1} back", m_logArguments[0], session.Id);

            try
            {
                Boolean wasInPool;
                SocketInConnetionPool socketInPool;
                lock (m_leasedSessions)
                {
                    wasInPool = m_leasedSessions.TryRemove(remoteEndPoint, out socketInPool);
                }

                if(wasInPool)
                {
                    //socket.OwningConnection = null;//why do we need this row?
                    var socketHealth = SocketHealth(socketInPool);
                    if (socketHealth == Tcp.SocketHealth.Healthy)
                    {
                        isReturned = m_sessions.TryAdd(socketInPool.RemoteEndPoint, socketInPool);
                    }
                    else
                    {
                        if (socketHealth == Tcp.SocketHealth.IsNotConnected)
                        {
                            log.LogInfo($"Pool received invalid Session{1}; destroying it");
                        }
                        else
                            log.LogInfo("Pool{0} received expired Session{1}; destroying it");
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
                m_sessionSemaphore.Release();
            }

#if NET45 || NET461 || NET471 || NETSTANDARD1_3 || NETSTANDARD2_0
			return default;
#endif
        }

        private SocketHealth SocketHealth(SocketInConnetionPool socket)
        {
            SocketHealth socketHealth;

            if(!socket.Connected)
            {
                socketHealth = Tcp.SocketHealth.IsNotConnected;
            }
            else if((ConnectionSettings.ConnectionLifeTime > 0) && 
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

        //public async ValueTask<DiscoveryServiceSocket> ClientAsync(IOBehavior ioBehavior)
        //{
        //    if(IsEmpty && (unchecked(((UInt32)Environment.TickCount) - lastRecoveryTime) >= 1000u))
        //    {
        //        log.LogInfo("Pool is empty; recovering leaked sessions");
        //        await RecoverLeakedSessionsAsync(ioBehavior)
        //    }
        //}

        //private async ValueTask<>
    }
}
