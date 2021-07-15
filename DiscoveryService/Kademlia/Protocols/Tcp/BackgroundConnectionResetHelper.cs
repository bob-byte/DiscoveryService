using LUC.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia.Protocols.Tcp
{
    static class BackgroundConnectionResetHelper
    {
		static readonly Object s_lock = new Object();
		static readonly SemaphoreSlim s_semaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);
		static readonly CancellationTokenSource s_cancellationTokenSource = new CancellationTokenSource();
		static readonly List<Task<Boolean>> s_resetTasks = new List<Task<Boolean>>();
		static Task s_workerTask;

		public static void AddSocket(SocketInConnectionPool socket, ILoggingService log)
        {
            SocketAsyncEventArgs disconnetArgs = new SocketAsyncEventArgs();
            var resetTask = socket.TryResetConnectionAsync(returnToPool: true, reuseSocket: true, IOBehavior.Asynchronous);
			lock (s_lock)
				s_resetTasks.Add(resetTask);

#if DEBUG
			log.LogInfo($"Started Session {socket.Id} reset in background; waiting TaskCount: {s_resetTasks.Count}.");
#endif

			// release only if it is likely to succeed
			if (s_semaphore.CurrentCount == 0)
			{
#if DEBUG
				log.LogInfo("Releasing semaphore.");
#endif

				try
				{
					s_semaphore.Release();
				}
				catch (SemaphoreFullException)
				{
					// ignore
				}
			}
		}
    }
}
