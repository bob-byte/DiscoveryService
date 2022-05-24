using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LUC.Services.Implementation
{
    partial class BackgroundSynchronizer
    {
        private class CheckServerChangesEventArgsCollection : IEnumerable<CheckServerChangesEventArgs>
        {
            private readonly ConcurrentQueue<CheckServerChangesEventArgs> m_queue;

            public CheckServerChangesEventArgsCollection()
            {
                m_queue = new ConcurrentQueue<CheckServerChangesEventArgs>();
            }

            public void Add( CheckServerChangesEventArgs eventArgs ) =>
                m_queue.Enqueue( eventArgs );

            public void TryRemoveFirstItem( out Boolean isRemoved ) =>
                isRemoved = m_queue.TryDequeue( result: out _ );

            public void Clear()
            {
                while ( !m_queue.IsEmpty )
                {
                    m_queue.TryDequeue( out _ );
                }
            }

            public IEnumerator<CheckServerChangesEventArgs> GetEnumerator() =>
                m_queue.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                GetEnumerator();
        }
    }
}
