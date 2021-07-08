using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia.Protocols.Tcp
{
	/// <summary>
	/// <see cref="TcpConnection"/> represents a connection to a MySQL database.
	/// </summary>
	public sealed class TcpConnection
#if !NETSTANDARD1_3
		, ICloneable
#endif
	{
		public TcpConnection()
			: this(default)
		{
		}

		public TcpConnection(string? connectionString)
		{
			GC.SuppressFinalize(this);
		}

		public override void Close() => CloseAsync(changeState: true, IOBehavior.Synchronous).GetAwaiter().GetResult();
#if NET45 || NET461 || NET471 || NETSTANDARD1_3 || NETSTANDARD2_0 || NETCOREAPP2_1
		public Task CloseAsync() => CloseAsync(changeState: true, SimpleAsyncIOBehavior);
#else
		public override Task CloseAsync() => CloseAsync(changeState: true, SimpleAsyncIOBehavior);
#endif
		internal Task CloseAsync(IOBehavior ioBehavior) => CloseAsync(changeState: true, ioBehavior);

#pragma warning disable CA2012 // Safe because method completes synchronously
		public bool Ping() => PingAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore CA2012
		public Task<bool> PingAsync(CancellationToken cancellationToken = default) => PingAsync(SimpleAsyncIOBehavior, cancellationToken).AsTask();

		private async ValueTask<bool> PingAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
		{
			if (m_session is null)
				return false;
			try
			{
				if (await m_session.TryPingAsync(logInfo: true, ioBehavior, cancellationToken).ConfigureAwait(false))
					return true;
			}
			catch (InvalidOperationException)
			{
			}

			SetState(ConnectionState.Closed);
			return false;
		}

		public override void Open() => OpenAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();

		public override Task OpenAsync(CancellationToken cancellationToken) => OpenAsync(default, cancellationToken);

		internal async Task OpenAsync(IOBehavior? ioBehavior, CancellationToken cancellationToken)
		{
			VerifyNotDisposed();
			if (State != ConnectionState.Closed)
				throw new InvalidOperationException("Cannot Open when State is {0}.".FormatInvariant(State));

			var openStartTickCount = Environment.TickCount;

			SetState(ConnectionState.Connecting);

			var pool = ConnectionPool.GetPool(m_connectionString);
			m_connectionSettings ??= pool?.ConnectionSettings ?? new ConnectionSettings(new TcpConnectionStringBuilder(m_connectionString));

#if !NETSTANDARD1_3
			// check if there is an open session (in the current transaction) that can be adopted
			if (m_connectionSettings.AutoEnlist && System.Transactions.Transaction.Current is not null)
			{
				var existingConnection = FindExistingEnlistedSession(System.Transactions.Transaction.Current);
				if (existingConnection is not null)
				{
					TakeSessionFrom(existingConnection);
					m_hasBeenOpened = true;
					SetState(ConnectionState.Open);
					return;
				}
			}
#endif
			try
			{
				m_session = await CreateSessionAsync(pool, openStartTickCount, ioBehavior, cancellationToken).ConfigureAwait(false);

				m_hasBeenOpened = true;
				SetState(ConnectionState.Open);
			}
			catch (TcpException)
			{
				SetState(ConnectionState.Closed);
				cancellationToken.ThrowIfCancellationRequested();
				throw;
			}
			catch (SocketException ex)
			{
				SetState(ConnectionState.Closed);
				throw new TcpException(MySqlErrorCode.UnableToConnectToHost, "Unable to connect to any of the specified MySQL hosts.", ex);
			}

#if !NETSTANDARD1_3
			if (m_connectionSettings.AutoEnlist && System.Transactions.Transaction.Current is not null)
				EnlistTransaction(System.Transactions.Transaction.Current);
#endif
		}

		/// <summary>
		/// Resets the session state of the current open connection; this clears temporary tables and user-defined variables.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A <c>ValueTask</c> representing the asynchronous operation.</returns>
		/// <remarks>This is an optional feature of the MySQL protocol and may not be supported by all servers.
		/// It's known to be supported by MySQL Server 5.7.3 (and later) and MariaDB 10.2.4 (and later).
		/// Other MySQL-compatible servers or proxies may not support this command.</remarks>
#if NET45 || NET461 || NET471 || NETSTANDARD1_3 || NETSTANDARD2_0
		public async Task ResetConnectionAsync(CancellationToken cancellationToken = default)
#else
		public async ValueTask ResetConnectionAsync(CancellationToken cancellationToken = default)
#endif
		{
			var session = Session;
			Log.Debug("Session{0} resetting connection", session.Id);
			await session.SendAsync(ResetConnectionPayload.Instance, AsyncIOBehavior, cancellationToken).ConfigureAwait(false);
			var payload = await session.ReceiveReplyAsync(AsyncIOBehavior, cancellationToken).ConfigureAwait(false);
			OkPayload.Create(payload.Span, session.SupportsDeprecateEof, session.SupportsSessionTrack);
		}

		[AllowNull]
		public override string ConnectionString
		{
			get
			{
				if (!m_hasBeenOpened)
					return m_connectionString;
				var connectionStringBuilder = GetConnectionSettings().ConnectionStringBuilder;
				return connectionStringBuilder.GetConnectionString(connectionStringBuilder.PersistSecurityInfo);
			}
			set
			{
				if (m_connectionState == ConnectionState.Open)
					throw new InvalidOperationException("Cannot change the connection string on an open connection.");
				m_hasBeenOpened = false;
				m_connectionString = value ?? "";
				m_connectionSettings = null;
			}
		}

		public override string Database => m_session?.DatabaseOverride ?? GetConnectionSettings().Database;

		public override ConnectionState State => m_connectionState;

		public override string DataSource => GetConnectionSettings().ConnectionStringBuilder.Server;

		public override string ServerVersion => Session.ServerVersion.OriginalString;

		/// <summary>
		/// The connection ID from MySQL Server.
		/// </summary>
		public int ServerThread => Session.ConnectionId;

		/// <summary>
		/// Clears the connection pool that <paramref name="connection"/> belongs to.
		/// </summary>
		/// <param name="connection">The <see cref="TcpConnection"/> whose connection pool will be cleared.</param>
		public static void ClearPool(TcpConnection connection) => ClearPoolAsync(connection, IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();

		/// <summary>
		/// Asynchronously clears the connection pool that <paramref name="connection"/> belongs to.
		/// </summary>
		/// <param name="connection">The <see cref="TcpConnection"/> whose connection pool will be cleared.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
		public static Task ClearPoolAsync(TcpConnection connection, CancellationToken cancellationToken = default) => ClearPoolAsync(connection, connection.AsyncIOBehavior, cancellationToken);

		/// <summary>
		/// Clears all connection pools.
		/// </summary>
		public static void ClearAllPools() => ConnectionPool.ClearPoolsAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();

		/// <summary>
		/// Asynchronously clears all connection pools.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
		public static Task ClearAllPoolsAsync(CancellationToken cancellationToken = default) => ConnectionPool.ClearPoolsAsync(IOBehavior.Asynchronous, cancellationToken);

		private static async Task ClearPoolAsync(TcpConnection connection, IOBehavior ioBehavior, CancellationToken cancellationToken)
		{
			if (connection is null)
				throw new ArgumentNullException(nameof(connection));

			var pool = ConnectionPool.GetPool(connection.m_connectionString);
			if (pool is not null)
				await pool.ClearAsync(ioBehavior, cancellationToken).ConfigureAwait(false);
		}

		protected override DbCommand CreateDbCommand() => new MySqlCommand(this, null);

#if !NETSTANDARD1_3
		protected override DbProviderFactory DbProviderFactory => MySqlConnectorFactory.Instance;

#pragma warning disable CA2012 // Safe because method completes synchronously
		/// <summary>
		/// Returns schema information for the data source of this <see cref="TcpConnection"/>.
		/// </summary>
		/// <returns>A <see cref="DataTable"/> containing schema information.</returns>
		public override DataTable GetSchema() => GetSchemaProvider().GetSchemaAsync(IOBehavior.Synchronous, default).GetAwaiter().GetResult();

		/// <summary>
		/// Returns schema information for the data source of this <see cref="TcpConnection"/>.
		/// </summary>
		/// <param name="collectionName">The name of the schema to return.</param>
		/// <returns>A <see cref="DataTable"/> containing schema information.</returns>
		public override DataTable GetSchema(string collectionName) => GetSchemaProvider().GetSchemaAsync(IOBehavior.Synchronous, collectionName, default).GetAwaiter().GetResult();

		/// <summary>
		/// Returns schema information for the data source of this <see cref="TcpConnection"/>.
		/// </summary>
		/// <param name="collectionName">The name of the schema to return.</param>
		/// <param name="restrictionValues">The restrictions to apply to the schema; this parameter is currently ignored.</param>
		/// <returns>A <see cref="DataTable"/> containing schema information.</returns>
		public override DataTable GetSchema(string collectionName, string?[] restrictionValues) => GetSchemaProvider().GetSchemaAsync(IOBehavior.Synchronous, collectionName, default).GetAwaiter().GetResult();
#pragma warning restore CA2012

		/// <summary>
		/// Asynchronously returns schema information for the data source of this <see cref="TcpConnection"/>.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A <see cref="Task{DataTable}"/> containing schema information.</returns>
		/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
#if NET45 || NET461 || NET471 || NETSTANDARD1_3 || NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP2_1 || NETCOREAPP3_1
		public Task<DataTable> GetSchemaAsync(CancellationToken cancellationToken = default)
#else
		public override Task<DataTable> GetSchemaAsync(CancellationToken cancellationToken = default)
#endif
			=> GetSchemaProvider().GetSchemaAsync(AsyncIOBehavior, cancellationToken).AsTask();

		/// <summary>
		/// Asynchronously returns schema information for the data source of this <see cref="TcpConnection"/>.
		/// </summary>
		/// <param name="collectionName">The name of the schema to return.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A <see cref="Task{DataTable}"/> containing schema information.</returns>
		/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
#if NET45 || NET461 || NET471 || NETSTANDARD1_3 || NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP2_1 || NETCOREAPP3_1
		public Task<DataTable> GetSchemaAsync(string collectionName, CancellationToken cancellationToken = default)
#else
		public override Task<DataTable> GetSchemaAsync(string collectionName, CancellationToken cancellationToken = default)
#endif
			=> GetSchemaProvider().GetSchemaAsync(AsyncIOBehavior, collectionName, cancellationToken).AsTask();

		/// <summary>
		/// Asynchronously returns schema information for the data source of this <see cref="TcpConnection"/>.
		/// </summary>
		/// <param name="collectionName">The name of the schema to return.</param>
		/// <param name="restrictionValues">The restrictions to apply to the schema; this parameter is currently ignored.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A <see cref="Task{DataTable}"/> containing schema information.</returns>
		/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
#if NET45 || NET461 || NET471 || NETSTANDARD1_3 || NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP2_1 || NETCOREAPP3_1
		public Task<DataTable> GetSchemaAsync(string collectionName, string?[] restrictionValues, CancellationToken cancellationToken = default)
#else
		public override Task<DataTable> GetSchemaAsync(string collectionName, string?[] restrictionValues, CancellationToken cancellationToken = default)
#endif
			=> GetSchemaProvider().GetSchemaAsync(AsyncIOBehavior, collectionName, cancellationToken).AsTask();

		private SchemaProvider GetSchemaProvider() => m_schemaProvider ??= new(this);

		SchemaProvider? m_schemaProvider;
#endif

		/// <summary>
		/// Gets the time (in seconds) to wait while trying to establish a connection
		/// before terminating the attempt and generating an error. This value
		/// is controlled by <see cref="TcpConnectionStringBuilder.ConnectionTimeout"/>,
		/// which defaults to 15 seconds.
		/// </summary>
		public override int ConnectionTimeout => GetConnectionSettings().ConnectionTimeout;

		public event MySqlInfoMessageEventHandler? InfoMessage;

		/// <summary>
		/// Creates a <see cref="MySqlBatch"/> object for executing batched commands.
		/// </summary>
		/// <returns></returns>
		public MySqlBatch CreateBatch() => CreateDbBatch();
		private MySqlBatch CreateDbBatch() => new(this);

		/// <summary>
		/// Creates a <see cref="MySqlBatchCommand"/> object (that can be used with <see cref="MySqlBatch.BatchCommands"/>).
		/// </summary>
		/// <returns></returns>
		public MySqlBatchCommand CreateBatchCommand() => CreateDbBatchCommand();
#pragma warning disable CA1822 // Mark members as static
		private MySqlBatchCommand CreateDbBatchCommand() => new();
		public bool CanCreateBatch => true;
#pragma warning restore CA1822 // Mark members as static

		protected override void Dispose(bool disposing)
		{
			try
			{
				if (disposing)
					CloseAsync(changeState: true, IOBehavior.Synchronous).GetAwaiter().GetResult();
			}
			finally
			{
				m_isDisposed = true;
				base.Dispose(disposing);
			}
		}

#if NET45 || NET461 || NET471 || NETSTANDARD1_3 || NETSTANDARD2_0 || NETCOREAPP2_1
		public async Task DisposeAsync()
#else
		public override async ValueTask DisposeAsync()
#endif
		{
			try
			{
				await CloseAsync(changeState: true, SimpleAsyncIOBehavior).ConfigureAwait(false);
			}
			finally
			{
				m_isDisposed = true;
			}
		}

		public TcpConnection Clone() => new(m_connectionString, m_hasBeenOpened);

#if !NETSTANDARD1_3
		object ICloneable.Clone() => Clone();
#endif

		/// <summary>
		/// Returns an unopened copy of this connection with a new connection string. If the <c>Password</c>
		/// in <paramref name="connectionString"/> is not set, the password from this connection will be used.
		/// This allows creating a new connection with the same security information while changing other options,
		/// such as database or pooling.
		/// </summary>
		/// <param name="connectionString">The new connection string to be used.</param>
		/// <returns>A new <see cref="TcpConnection"/> with different connection string options but
		/// the same password as this connection (unless overridden by <paramref name="connectionString"/>).</returns>
		public TcpConnection CloneWith(string connectionString)
		{
			var newBuilder = new TcpConnectionStringBuilder(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
			var currentBuilder = new TcpConnectionStringBuilder(m_connectionString);
			var shouldCopyPassword = newBuilder.Password.Length == 0 && (!newBuilder.PersistSecurityInfo || currentBuilder.PersistSecurityInfo);
			if (shouldCopyPassword)
				newBuilder.Password = currentBuilder.Password;
			return new TcpConnection(newBuilder.ConnectionString, m_hasBeenOpened && shouldCopyPassword && !currentBuilder.PersistSecurityInfo);
		}

		internal ServerSession Session
		{
			get
			{
				VerifyNotDisposed();
				if (m_session is null || State != ConnectionState.Open)
					throw new InvalidOperationException("Connection must be Open; current state is {0}".FormatInvariant(State));
				return m_session;
			}
		}

		internal void SetSessionFailed(Exception exception) => m_session!.SetFailed(exception);

		internal void Cancel(ICancellableCommand command, int commandId, bool isCancel)
		{
			if (m_session is null || State != ConnectionState.Open || !m_session.TryStartCancel(command))
			{
				Log.Info("Ignoring cancellation for closed connection or invalid CommandId {0}", commandId);
				return;
			}

			Log.Info("CommandId {0} for Session{1} has been canceled via {2}.", commandId, m_session.Id, isCancel ? "Cancel()" : "command timeout");

			try
			{
				// open a dedicated connection to the server to kill the active query
				var csb = new TcpConnectionStringBuilder(m_connectionString)
				{
					AutoEnlist = false,
					Pooling = false,
				};
				if (m_session.IPAddress is not null)
					csb.Server = m_session.IPAddress.ToString();
				var cancellationTimeout = GetConnectionSettings().CancellationTimeout;
				csb.ConnectionTimeout = cancellationTimeout < 1 ? 3u : (uint) cancellationTimeout;

				using var connection = new TcpConnection(csb.ConnectionString);
				connection.Open();
				using var killCommand = new MySqlCommand("KILL QUERY {0}".FormatInvariant(command.Connection!.ServerThread), connection);
				killCommand.CommandTimeout = cancellationTimeout < 1 ? 3 : cancellationTimeout;
				m_session.DoCancel(command, killCommand);
			}
			catch (InvalidOperationException ex)
			{
				// ignore a rare race condition where the connection is open at the beginning of the method, but closed by the time
				// KILL QUERY is executed: https://github.com/mysql-net/MySqlConnector/issues/1002
				Log.Info(ex, "Session{0} ignoring cancellation for closed connection.", m_session!.Id);
				m_session.AbortCancel(command);
			}
			catch (TcpException ex)
			{
				// cancelling the query failed; setting the state back to 'Querying' will allow another call to 'Cancel' to try again
				Log.Warn(ex, "Session{0} cancelling CommandId {1} failed", m_session!.Id, command.CommandId);
				m_session.AbortCancel(command);
			}
		}

		internal async Task<CachedProcedure?> GetCachedProcedure(string name, bool revalidateMissing, IOBehavior ioBehavior, CancellationToken cancellationToken)
		{
			if (Log.IsDebugEnabled())
				Log.Debug("Session{0} getting cached procedure Name={1}", m_session!.Id, name);
			if (State != ConnectionState.Open)
				throw new InvalidOperationException("Connection is not open.");

			var cachedProcedures = m_session!.Pool?.GetProcedureCache() ?? m_cachedProcedures;
			if (cachedProcedures is null)
			{
				Log.Warn("Session{0} pool Pool{1} doesn't have a shared procedure cache; procedure will only be cached on this connection", m_session.Id, m_session.Pool?.Id);
				cachedProcedures = m_cachedProcedures = new();
			}

			var normalized = NormalizedSchema.MustNormalize(name, Database);
			if (string.IsNullOrEmpty(normalized.Schema))
			{
				Log.Warn("Session{0} couldn't normalize Database={1} Name={2}; not caching procedure", m_session.Id, Database, name);
				return null;
			}

			CachedProcedure? cachedProcedure;
			bool foundProcedure;
			lock (cachedProcedures)
				foundProcedure = cachedProcedures.TryGetValue(normalized.FullyQualified, out cachedProcedure);
			if (!foundProcedure || (cachedProcedure is null && revalidateMissing))
			{
				cachedProcedure = await CachedProcedure.FillAsync(ioBehavior, this, normalized.Schema!, normalized.Component!, cancellationToken).ConfigureAwait(false);
				if (Log.IsWarnEnabled())
				{
					if (cachedProcedure is null)
						Log.Warn("Session{0} failed to cache procedure Schema={1} Component={2}", m_session.Id, normalized.Schema, normalized.Component);
					else
						Log.Info("Session{0} caching procedure Schema={1} Component={2}", m_session.Id, normalized.Schema, normalized.Component);
				}
				int count;
				lock (cachedProcedures)
				{
					cachedProcedures[normalized.FullyQualified] = cachedProcedure;
					count = cachedProcedures.Count;
				}
				if (Log.IsInfoEnabled())
					Log.Info("Session{0} procedure cache Count={1}", m_session.Id, count);
			}

			if (Log.IsWarnEnabled())
			{
				if (cachedProcedure is null)
					Log.Warn("Session{0} did not find cached procedure Schema={1} Component={2}", m_session.Id, normalized.Schema, normalized.Component);
				else
					Log.Debug("Session{0} returning cached procedure Schema={1} Component={2}", m_session.Id, normalized.Schema, normalized.Component);
			}
			return cachedProcedure;
		}

		internal MySqlTransaction? CurrentTransaction { get; set; }
		internal bool AllowLoadLocalInfile => GetInitializedConnectionSettings().AllowLoadLocalInfile;
		internal bool AllowUserVariables => GetInitializedConnectionSettings().AllowUserVariables;
		internal bool AllowZeroDateTime => GetInitializedConnectionSettings().AllowZeroDateTime;
		internal bool ConvertZeroDateTime => GetInitializedConnectionSettings().ConvertZeroDateTime;
		internal DateTimeKind DateTimeKind => GetInitializedConnectionSettings().DateTimeKind;
		internal int DefaultCommandTimeout => GetConnectionSettings().DefaultCommandTimeout;
		internal MySqlGuidFormat GuidFormat => GetInitializedConnectionSettings().GuidFormat;
#if NETSTANDARD1_3
		internal bool IgnoreCommandTransaction => GetInitializedConnectionSettings().IgnoreCommandTransaction;
#else
		internal bool IgnoreCommandTransaction => GetInitializedConnectionSettings().IgnoreCommandTransaction || m_enlistedTransaction is StandardEnlistedTransaction;
#endif
		internal bool IgnorePrepare => GetInitializedConnectionSettings().IgnorePrepare;
		internal bool NoBackslashEscapes => GetInitializedConnectionSettings().NoBackslashEscapes;
		internal bool TreatTinyAsBoolean => GetInitializedConnectionSettings().TreatTinyAsBoolean;
		internal IOBehavior AsyncIOBehavior => GetConnectionSettings().ForceSynchronous ? IOBehavior.Synchronous : IOBehavior.Asynchronous;

		// Defaults to IOBehavior.Synchronous if the connection hasn't been opened yet; only use if it's a no-op for a closed connection.
		internal IOBehavior SimpleAsyncIOBehavior => (m_connectionSettings?.ForceSynchronous ?? false) ? IOBehavior.Synchronous : IOBehavior.Asynchronous;

		internal MySqlSslMode SslMode => GetInitializedConnectionSettings().SslMode;

		internal int? ActiveCommandId => m_session?.ActiveCommandId;

		internal bool HasActiveReader => m_activeReader is not null;

		internal void SetActiveReader(MySqlDataReader dataReader)
		{
			if (dataReader is null)
				throw new ArgumentNullException(nameof(dataReader));
			if (m_activeReader is not null)
				throw new InvalidOperationException("Can't replace active reader.");
			m_activeReader = dataReader;
		}

		internal void FinishQuerying(bool hasWarnings)
		{
			m_session!.FinishQuerying();
			m_activeReader = null;

			if (hasWarnings && InfoMessage is not null)
			{
				var errors = new List<MySqlError>();
				using (var command = new MySqlCommand("SHOW WARNINGS;", this))
				{
					command.Transaction = CurrentTransaction;
					using var reader = command.ExecuteReader();
					while (reader.Read())
						errors.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetString(2)));
				}

				InfoMessage(this, new MySqlInfoMessageEventArgs(errors));
			}
		}

		private async ValueTask<ServerSession> CreateSessionAsync(ConnectionPool? pool, int startTickCount, IOBehavior? ioBehavior, CancellationToken cancellationToken)
		{
			var connectionSettings = GetInitializedConnectionSettings();
			var actualIOBehavior = ioBehavior ?? (connectionSettings.ForceSynchronous ? IOBehavior.Synchronous : IOBehavior.Asynchronous);

			CancellationTokenSource? timeoutSource = null;
			CancellationTokenSource? linkedSource = null;
			try
			{
				// the cancellation token for connection is controlled by 'cancellationToken' (if it can be cancelled), ConnectionTimeout
				// (from the connection string, if non-zero), or a combination of both
				if (connectionSettings.ConnectionTimeout != 0)
					timeoutSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(1, connectionSettings.ConnectionTimeoutMilliseconds - unchecked(Environment.TickCount - startTickCount))));
				if (cancellationToken.CanBeCanceled && timeoutSource is not null)
					linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
				var connectToken = linkedSource?.Token ?? timeoutSource?.Token ?? cancellationToken;

				// get existing session from the pool if possible
				if (pool is not null)
				{
					// this returns an open session
					return await pool.GetSessionAsync(this, startTickCount, actualIOBehavior, connectToken).ConfigureAwait(false);
				}
				else
				{
					// only "fail over" and "random" load balancers supported without connection pooling
					var loadBalancer = connectionSettings.LoadBalance == MySqlLoadBalance.Random && connectionSettings.HostNames!.Count > 1 ?
						RandomLoadBalancer.Instance : FailOverLoadBalancer.Instance;

					var session = new ServerSession();
					session.OwningConnection = new WeakReference<TcpConnection>(this);
					Log.Info("Created new non-pooled Session{0}", session.Id);
					await session.ConnectAsync(connectionSettings, startTickCount, loadBalancer, actualIOBehavior, connectToken).ConfigureAwait(false);
					return session;
				}
			}
			catch (OperationCanceledException ex) when (timeoutSource?.IsCancellationRequested ?? false)
			{
				var messageSuffix = (pool?.IsEmpty ?? false) ? " All pooled connections are in use." : "";
				throw new TcpException(MySqlErrorCode.UnableToConnectToHost, "Connect Timeout expired." + messageSuffix, ex);
			}
			catch (TcpException ex) when ((timeoutSource?.IsCancellationRequested ?? false) || (ex.ErrorCode == MySqlErrorCode.CommandTimeoutExpired))
			{
				throw new TcpException(MySqlErrorCode.UnableToConnectToHost, "Connect Timeout expired.", ex);
			}
			finally
			{
				linkedSource?.Dispose();
				timeoutSource?.Dispose();
			}
		}

		internal bool SslIsEncrypted => m_session!.SslIsEncrypted;

		internal bool SslIsSigned => m_session!.SslIsSigned;

		internal bool SslIsAuthenticated => m_session!.SslIsAuthenticated;

		internal bool SslIsMutuallyAuthenticated => m_session!.SslIsMutuallyAuthenticated;

		internal SslProtocols SslProtocol => m_session!.SslProtocol;

		internal void SetState(ConnectionState newState)
		{
			if (m_connectionState != newState)
			{
				var previousState = m_connectionState;
				m_connectionState = newState;
				var eventArgs =
					previousState == ConnectionState.Closed && newState == ConnectionState.Connecting ? s_stateChangeClosedConnecting :
					previousState == ConnectionState.Connecting && newState == ConnectionState.Open ? s_stateChangeConnectingOpen :
					previousState == ConnectionState.Open && newState == ConnectionState.Closed ? s_stateChangeOpenClosed :
					new(previousState, newState);
				OnStateChange(eventArgs);
			}
		}

		private TcpConnection(string connectionString, bool hasBeenOpened)
			: this(connectionString)
		{
			m_hasBeenOpened = hasBeenOpened;
		}

		private void VerifyNotDisposed()
		{
			if (m_isDisposed)
				throw new ObjectDisposedException(GetType().Name);
		}

		private async Task CloseAsync(bool changeState, IOBehavior ioBehavior)
		{
			// check fast path
			if (m_activeReader is null &&
				CurrentTransaction is null &&
#if !NETSTANDARD1_3
				m_enlistedTransaction is null &&
#endif
				(m_connectionSettings?.Pooling ?? false))
			{
				m_cachedProcedures = null;
				if (m_session is not null)
				{
					await m_session.ReturnToPoolAsync(ioBehavior, this).ConfigureAwait(false);
					m_session = null;
				}
				if (changeState)
					SetState(ConnectionState.Closed);

				return;
			}

			await DoCloseAsync(changeState, ioBehavior).ConfigureAwait(false);
		}

		private async Task DoCloseAsync(bool changeState, IOBehavior ioBehavior)
		{
#if !NETSTANDARD1_3
			// If participating in a distributed transaction, keep the connection open so we can commit or rollback.
			// This handles the common pattern of disposing a connection before disposing a TransactionScope (e.g., nested using blocks)
			if (m_enlistedTransaction is not null)
			{
				// make sure all DB work is done
				if (m_activeReader is not null)
					await m_activeReader.DisposeAsync(ioBehavior, CancellationToken.None).ConfigureAwait(false);
				m_activeReader = null;

				// This connection is being closed, so create a new TcpConnection that will own the ServerSession
				// (which remains open). This ensures the ServerSession always has a valid OwningConnection (even
				// if 'this' is GCed.
				var connection = new TcpConnection
				{
					m_connectionString = m_connectionString,
					m_connectionSettings = m_connectionSettings,
					m_connectionState = m_connectionState,
					m_hasBeenOpened = true,
				};
				connection.TakeSessionFrom(this);

				// put the new, idle, connection into the list of sessions for this transaction (replacing this TcpConnection)
				lock (s_lock)
				{
					foreach (var enlistedTransaction in s_transactionConnections[connection.m_enlistedTransaction!.Transaction])
					{
						if (enlistedTransaction.Connection == this)
						{
							enlistedTransaction.Connection = connection;
							enlistedTransaction.IsIdle = true;
							break;
						}
					}
				}

				if (changeState)
					SetState(ConnectionState.Closed);
				return;
			}
#endif

			m_cachedProcedures = null;

			try
			{
				if (m_activeReader is not null || CurrentTransaction is not null)
					await CloseDatabaseAsync(ioBehavior, CancellationToken.None).ConfigureAwait(false);
			}
			finally
			{
				if (m_session is not null)
				{
					if (GetInitializedConnectionSettings().Pooling)
					{
						await m_session.ReturnToPoolAsync(ioBehavior, this).ConfigureAwait(false);
					}
					else
					{
						await m_session.DisposeAsync(ioBehavior, CancellationToken.None).ConfigureAwait(false);
						m_session.OwningConnection = null;
					}
					m_session = null;
				}

				if (changeState)
					SetState(ConnectionState.Closed);
			}
		}

#if NET45 || NET461 || NET471 || NETSTANDARD1_3 || NETSTANDARD2_0 || NETCOREAPP2_1
		private async Task CloseDatabaseAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
#else
		private async ValueTask CloseDatabaseAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
#endif
		{
			if (m_activeReader is not null)
				await m_activeReader.DisposeAsync(ioBehavior, cancellationToken).ConfigureAwait(false);
			if (CurrentTransaction is not null && m_session!.IsConnected)
			{
				await CurrentTransaction.DisposeAsync(ioBehavior, cancellationToken).ConfigureAwait(false);
				CurrentTransaction = null;
			}
		}

		private ConnectionSettings GetConnectionSettings() =>
			m_connectionSettings ??= new(new TcpConnectionStringBuilder(m_connectionString));

		// This method may be called when it's known that the connection settings have been initialized.
		private ConnectionSettings GetInitializedConnectionSettings() => m_connectionSettings!;

		static readonly IMySqlConnectorLogger Log = MySqlConnectorLogManager.CreateLogger(nameof(TcpConnection));
		static readonly StateChangeEventArgs s_stateChangeClosedConnecting = new(ConnectionState.Closed, ConnectionState.Connecting);
		static readonly StateChangeEventArgs s_stateChangeConnectingOpen = new(ConnectionState.Connecting, ConnectionState.Open);
		static readonly StateChangeEventArgs s_stateChangeOpenClosed = new(ConnectionState.Open, ConnectionState.Closed);
#if !NETSTANDARD1_3
		static readonly object s_lock = new();
		static readonly Dictionary<System.Transactions.Transaction, List<EnlistedTransactionBase>> s_transactionConnections = new();
#endif

		string m_connectionString;
		ConnectionSettings? m_connectionSettings;
		ServerSession? m_session;
		ConnectionState m_connectionState;
		bool m_hasBeenOpened;
		bool m_isDisposed;
		Dictionary<string, CachedProcedure?>? m_cachedProcedures;
		MySqlDataReader? m_activeReader;
	}
}
