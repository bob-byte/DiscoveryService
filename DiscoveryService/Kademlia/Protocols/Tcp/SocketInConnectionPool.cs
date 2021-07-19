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

namespace LUC.DiscoveryService.Kademlia.Protocols.Tcp
{
    class SocketInConnectionPool : DiscoveryServiceSocket
    {
        private readonly Object m_lock = new Object();
        private SocketState m_state;
        private Boolean isInPool = false;

        /// <inheritdoc/>
        public SocketInConnectionPool(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, EndPoint remoteEndPoint, ConnectionPool belongPool, ILoggingService log)
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
            set
            {
                isInPool = value;

                if(!isInPool)
                {
                    ReturnedInPool.Wait(millisecondsTimeout: 0);
                }
                else if(isInPool && ReturnedInPool.CurrentCount == 0)
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
            //            Log.Info("Session{0} closing stream/socket", m_logArguments);
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

        //		static ReadOnlySpan<byte> BeginCertificateBytes => new byte[] { 45, 45, 45, 45, 45, 66, 69, 71, 73, 78, 32, 67, 69, 82, 84, 73, 70, 73, 67, 65, 84, 69, 45, 45, 45, 45, 45 }; // -----BEGIN CERTIFICATE-----
        //		static int s_lastId;
        //		static readonly ILoggingService Log;
        //		static readonly PayloadData s_setNamesUtf8Payload = QueryPayload.Create("SET NAMES utf8;");
        //		static readonly PayloadData s_setNamesUtf8mb4Payload = QueryPayload.Create("SET NAMES utf8mb4;");

        //		readonly object m_lock;
        //		readonly object?[] m_logArguments;
        //		readonly ArraySegmentHolder<byte> m_payloadCache;
        //		State m_state;
        //		TcpClient m_tcpClient;
        //		Socket m_socket;
        //		Stream m_stream;
        //		SslStream m_sslStream;
        //		X509Certificate2 m_clientCertificate;
        //		IPayloadHandler? m_payloadHandler;
        //		bool m_useCompression;
        //		bool m_isSecureConnection;
        //		bool m_supportsComMulti;
        //		bool m_supportsConnectionAttributes;
        //		bool m_supportsDeprecateEof;
        //		bool m_supportsSessionTrack;
        //		CharacterSet m_characterSet;
        //		PayloadData m_setNamesPayload;
        //		Dictionary<string, PreparedStatements>? m_preparedStatements;

        //		public SocketInConnPool()
        //            : this()
        //        {

        //        }

        //        public SocketInConnPool(ConnectionPool pool, Int32 id)
        //        {
        //			m_lock = new Object();
        //			Id = $"{(pool?.Id ?? 0)}.{id}";
        //			CreatedTicks = unchecked((UInt32)Environment.TickCount);
        //			Pool = pool;
        //			m_logArguments = new Object[] { "{0}".FormatInvariant(Id), null };
        //			//Log.Debug("Session{0} created new session", m_logArguments);
        //		}

        //		public string Id { get; }
        //		public int ActiveCommandId { get; private set; }
        //		public int CancellationTimeout { get; private set; }
        //		public int ConnectionId { get; set; }
        //		public byte[] AuthPluginData { get; set; }
        //		public uint CreatedTicks { get; }
        //		public ConnectionPool Pool { get; }
        //		public uint LastReturnedTicks { get; private set; }
        //		public string DatabaseOverride { get; set; }
        //		public string HostName { get; private set; }
        //		public IPAddress IPAddress => (m_tcpClient?.Client.RemoteEndPoint as IPEndPoint)?.Address;
        //		public WeakReference<TcpConnection> OwningConnection { get; set; }
        //		public bool SupportsComMulti => m_supportsComMulti;
        //		public bool SupportsDeprecateEof => m_supportsDeprecateEof;
        //		public bool SupportsSessionTrack => m_supportsSessionTrack;
        //		public bool ProcAccessDenied { get; set; }

        //		/// <summary>
        //		/// Use it when you take <see cref="SocketInConnPool"/> from <see cref="ConnectionPool"/> and want to return it there
        //		/// </summary>
        //		/// <param name="ioBehavior"></param>
        //		/// <param name="owningConnection"></param>
        //		/// <returns></returns>
        //#if NET45 || NET461 || NET471 || NETSTANDARD1_3 || NETSTANDARD2_0
        //        public ValueTask<Int32> ReturnToPoolAsync(IOBehavior ioBehavior, TcpConnection owningConnection)
        //#else
        //		public ValueTask ReturnToPoolAsync(IOBehavior ioBehavior, TcpConnection owningConnection)
        //#endif
        //		{
        //			//if (Log.IsDebugEnabled())
        //			//{
        //			//	m_logArguments[1] = Pool?.Id;
        //			//	Log.Debug("Session{0} returning to Pool{1}", m_logArguments);
        //			//}

        //			LastReturnedTicks = unchecked((UInt32)Environment.TickCount);
        //			if(!Pool.ConnectionSettings.ConnectionReset || Pool.ConnectionSettings.DeferConnectionReset)
        //            {
        //				return Pool.ReturnAsync(ioBehavior, this);
        //            }
        //			else if(!(Pool is null))
        //            {
        //				BackgroundConnectionResetHelper.AddSocket(this, owningConnection);
        //            }

        //			return default;
        //		}

        //		public async Task<Boolean> TryResetCo
    }
}
