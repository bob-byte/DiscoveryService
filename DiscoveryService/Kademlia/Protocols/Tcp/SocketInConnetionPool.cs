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
    class SocketInConnetionPool : DiscoveryServiceSocket
    {
        private readonly Object m_lock = new Object();
        private SocketState m_state;
        private WeakReference<TcpConnection> owningConnection;

        /// <inheritdoc/>
        public SocketInConnetionPool(SocketType socketType, ProtocolType protocolType)
            : this(default, socketType, protocolType, ID.RandomID.Value)
        {
            ;//do nothing
        }

        /// <inheritdoc/>
        public SocketInConnetionPool(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
            : this(addressFamily, socketType, protocolType, ID.RandomID.Value)
        {
            ;//do nothing
        }

        public SocketInConnetionPool(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, BigInteger contactId)
            : base(addressFamily, socketType, protocolType)
        {
            ContactId = contactId;
        }

        public WeakReference<TcpConnection> OwningConnection 
        { 
            get
            {
                if(!Connected)
                {
                    owningConnection = null;
                }

                return owningConnection;
            }
            set
            {
                //if (value == null)
                //{
                //    Disconnect(reuseSocket: true);
                //}

                owningConnection = value;
            }
        }

        public UInt32 CreatedTicks { get; set; } = unchecked((UInt32)Environment.TickCount);

        public async Task DisposeAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
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

            if (state == SocketState.Closing)
            {
                try
                {
                    //Log.Info("Session{0} sending QUIT command", m_logArguments);
                    //m_payloadHandler.StartNewConversation();
                    //await m_payloadHandler.WritePayloadAsync(QuitPayload.Instance.Memory, ioBehavior).ConfigureAwait(false);
                }
                catch (IOException)
                {
                }
                catch (NotSupportedException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (SocketException)
                {
                }
            }

            ShutdownSocket();
            lock (m_lock)
            {
                m_state = SocketState.Closed;
            }
        }

        private void ShutdownSocket()
        {
            //            Log.Info("Session{0} closing stream/socket", m_logArguments);
            Shutdown(SocketShutdown.Both);
            Dispose(disposing: true);

//            Utility.Dispose(ref m_payloadHandler);
//            Utility.Dispose(ref m_stream);
//            SafeDispose(ref m_tcpClient);
//            SafeDispose(ref m_socket);
//#if NET45
//			m_clientCertificate?.Reset();
//			m_clientCertificate = null;
//#else
//            Utility.Dispose(ref m_clientCertificate);
//#endif
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
