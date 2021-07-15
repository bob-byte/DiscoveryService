using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;

namespace LUC.DiscoveryService.Kademlia.Protocols.Tcp
{
	sealed class ConnectionSettings
	{
		public ConnectionSettings()
		{
			// TODO: define SSL/TLS Options

			// Connection Pooling Options
			ConnectionLifeTime = uint.MaxValue;
			
			ConnectionReset = true; 
			
			ConnectionIdlePingTime = uint.MaxValue;

			// The amount of time (in seconds) that a connection can 
			// remain idle in the pool. Any connection that is idle 
			// for longer than <code>ConnectionIdleTimeout</code> 
			// is subject to being closed by a background task. 
			// The background task runs every minute, or half of 
			// <code>ConnectionIdleTimeout</code>, whichever is more 
			// frequent. A value of zero (0) means pooled connections 
			// will never incur a ConnectionIdleTimeout, and if the pool 
			// grows to its maximum size, it will never get smaller.
			ConnectionIdleTimeout = 180;

			// If <code>true</code>, the connection state is not reset until 
			// the connection is retrieved from the pool. The experimental 
			// value of <code>false</code> resets connections in the background 
			// after they’re closed which can make opening a connection faster, 
			// and releases server resources sooner; however, there are reports 
			// of connection pool exhaustion when using this value.
			DeferConnectionReset = true;

			// Other Options

			// The length of time (in seconds) to wait for a query to be canceled 
			// when <code>MySqlCommand.CommandTimeout</code> expires, or zero for 
			// no timeout. If a response isn’t received from the server in this
			// time, the local socket will be closed and a <code>MySqlException</code> will be thrown.
			CancellationTimeout = 2;

			// The length of time (in seconds) to wait for a connection to the server before terminating the attempt and generating an error.
			ConnectionTimeout = 15;

			// TCP Keepalive idle time (in seconds). A value of 0 indicates that the OS Default keepalive settings are used;
			// a value greater than 0 is the idle connection time (in seconds) before the first keepalive packet is sent.
			// On Windows, this option is always supported. On non-Windows platforms, this option only takes effect in 
			// .NET Core 3.0 and later. For earlier versions of .NET Core, the OS Default keepalive settings are used instead.
			Keepalive = 0;

			MaximumPoolSize = 16;
			MinimumPoolSize = 0;
		}

		// Connection Pooling Options
		public uint ConnectionLifeTime { get; }

		/// <summary>
		/// all connections retrieved from the pool will have been reset. 
		/// The default value of <a href="true"></a> ensures that the 
		/// connection is in the same state whether it’s newly created 
		/// or retrieved from the pool. A value of <a href="false"></a>
		/// avoids making an additional server round trip to reset the 
		/// connection, but the connection state is not reset, meaning 
		/// that session variables and other session state changes 
		/// from any previous use of the connection are carried over.
		/// </summary>
		public bool ConnectionReset { get; }

		/// <summary>
		/// When a connection is retrieved from the pool, and <seealso cref="ConnectionReset"/> is <code>false</code>, the server
		/// will be pinged if the connection has been idle in the pool for longer than <code>ConnectionIdlePingTime</code> seconds.
		/// If pinging the server fails, a new connection will be opened automatically by the connection pool. This ensures that the
		/// <code>MySqlConnection</code> is in a valid, open state after the call to <code>Open</code>/<code>OpenAsync</code>,
		/// at the cost of an extra server roundtrip. For high-performance scenarios, you may wish to set <code>ConnectionIdlePingTime</code>
		/// to a non-zero value to make the connection pool assume that recently-returned connections are still open. If the
		/// connection is broken, it will throw from the first call to <code>ExecuteNonQuery</code>, <code>ExecuteReader</code>,
		/// etc.; your code should handle that failure and retry the connection. This option has no effect if <code>ConnectionReset</code>
		///  is <code>true</code>, as that will cause a connection reset packet to be sent to the server, making ping redundant.
		/// </summary>
		public uint ConnectionIdlePingTime { get; }
		public int ConnectionIdleTimeout { get; }
		public bool DeferConnectionReset { get; }

		public int CancellationTimeout { get; }
		public int ConnectionTimeout { get; }
		public uint Keepalive { get; }

		public int MinimumPoolSize { get; }
		public int MaximumPoolSize { get; }

		// Helper Functions
		int? m_connectionTimeoutMilliseconds;
		public int ConnectionTimeoutMilliseconds
		{
			get
			{
				if (!m_connectionTimeoutMilliseconds.HasValue)
				{
					try
					{
						checked
						{
							m_connectionTimeoutMilliseconds = ConnectionTimeout * 1000;
						}
					}
					catch (OverflowException)
					{
						m_connectionTimeoutMilliseconds = Int32.MaxValue;
					}
				}
				return m_connectionTimeoutMilliseconds.Value;
			}
		}
	}
}
