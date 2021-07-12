using LUC.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        readonly LinkedList<SocketInConnetionPool> m_sessions;
        readonly Dictionary<IPEndPoint, SocketInConnetionPool> m_leasedSessions;
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
            m_sessionSemaphore = new SemaphoreSlim(20);

            m_sessions = new LinkedList<SocketInConnetionPool>();
            m_leasedSessions = new Dictionary<IPEndPoint, SocketInConnetionPool>();
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

        //public async ValueTask<SocketInConnetionPool> SocketAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
        //{
        //    cancellationToken.ThrowIfCancellationRequested();

        //    if(IsEmpty && unchecked(((UInt32)Environment.TickCount) - m_lastRecoveryTime) >= 1000u)
        //    {
        //        //Log.Info("Pool{0} is empty; recovering leaked sessions", m_logArguments);

        //        await RecoverLeakedSocketsAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
        //    }
        //}

        /// <summary>
		/// Examines all the <see cref="ServerSession"/> objects in <see cref="m_leasedSessions"/> to determine if any
		/// have an owning <see cref="MySqlConnection"/> that has been garbage-collected. If so, assumes that the connection
		/// was not properly disposed and returns the session to the pool.
		/// </summary>
		private async Task RecoverLeakedSocketsAsync(IOBehavior ioBehavior)
        {
            var recoveredSockets = new List<SocketInConnetionPool>();
            lock (m_leasedSessions)
            {
                m_lastRecoveryTime = unchecked((uint)Environment.TickCount);
                foreach (var socket in m_leasedSessions.Values)
                {
                    if (!socket.Connected)
                    {
                        recoveredSockets.Add(socket);
                    }
                }
            }
            //if (recoveredSessions.Count == 0)
            //    Log.Debug("Pool{0} recovered no sessions", m_logArguments);
            //else
            //    Log.Warn("Pool{0}: RecoveredSessionCount={1}", m_logArguments[0], recoveredSessions.Count);
            //foreach (var recoveredSocket in recoveredSockets)
            //{
            //    await recoveredSocket.ReturnToPoolAsync(ioBehavior, null).ConfigureAwait(false);
            //}
        }



#if NET45 || NET461 || NET471 || NETSTANDARD1_3 || NETSTANDARD2_0
		public async ValueTask<int> ReturnAsync(IOBehavior ioBehavior, ServerSession session)
#else
        public async ValueTask ReturnAsync(IOBehavior ioBehavior, SocketInConnetionPool socket)
#endif
        {

            //if (Log.IsDebugEnabled())
            //    Log.Debug("Pool{0} receiving Session{1} back", m_logArguments[0], session.Id);

            try
            {
                lock (m_leasedSessions)
                {
                    m_leasedSessions.Remove(socket.RemoteEndPoint as IPEndPoint);
                }
                    
                socket.OwningConnection = null;//why do we need this row?
                var socketHealth = SocketHealth(socket);
                if (socketHealth == Tcp.SocketHealth.Healthy)
                {
                    lock (m_sessions)
                        m_sessions.AddFirst(socket);
                }
                else
                {
                    //if (socketHealth == Tcp.SocketHealth.IsNotConnected)
                    //    Log.Warn("Pool{0} received invalid Session{1}; destroying it", m_logArguments[0], socket.Id);
                    //else
                    //    Log.Info("Pool{0} received expired Session{1}; destroying it", m_logArguments[0], socket.Id);
                    //AdjustHostConnectionCount(socket, -1);
                    await socket.DisposeAsync(ioBehavior, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch(Exception ex)
            {
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
