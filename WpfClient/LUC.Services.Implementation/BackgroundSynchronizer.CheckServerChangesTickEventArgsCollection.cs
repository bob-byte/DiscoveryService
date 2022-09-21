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
            private readonly Stack<CheckServerChangesEventArgs> m_collection;

            public CheckServerChangesEventArgsCollection()
            {
                m_collection = new Stack<CheckServerChangesEventArgs>();
            }

            public void Add( CheckServerChangesEventArgs eventArgs ) =>
                m_collection.Push( eventArgs );

            public void GetLast(out CheckServerChangesEventArgs takenItem) =>
                takenItem = m_collection.Peek();

            public void Clear() =>
                m_collection.Clear();

            public IEnumerator<CheckServerChangesEventArgs> GetEnumerator() =>
                m_collection.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                GetEnumerator();
        }
    }
}
