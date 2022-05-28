using System;
using System.Collections.Generic;
using System.Threading;

namespace LUC.Interfaces.Helpers
{
    public partial class AsyncHelper
    {
        private class ExclusiveSynchronizationContext : SynchronizationContext
        {
            private readonly AutoResetEvent m_workItemsWaiting;

            private readonly Queue<Tuple<SendOrPostCallback, Object>> m_items;

            private Boolean m_done;

            public ExclusiveSynchronizationContext()
            {
                m_workItemsWaiting = new AutoResetEvent( initialState: false );
                m_items = new Queue<Tuple<SendOrPostCallback, Object>>();
            }

            public Exception InnerException { get; set; }

            public override void Send( SendOrPostCallback sendOrPostCallback, Object state ) =>
                throw new NotSupportedException( message: "We cannot send to our same thread" );

            public override void Post( SendOrPostCallback sendOrPostCallback, Object state )
            {
                lock ( m_items )
                {
                    m_items.Enqueue( Tuple.Create( sendOrPostCallback, state ) );
                }

                m_workItemsWaiting.Set();
            }

            public override SynchronizationContext CreateCopy() =>
                this;

            public void EndMessageLoop() =>
                Post( sendOrPostCallback: _ => m_done = true, state: null );

            public void BeginMessageLoop()
            {
                while ( !m_done )
                {
                    Tuple<SendOrPostCallback, Object> task = null;

                    lock ( m_items )
                    {
                        if ( m_items.Count > 0 )
                        {
                            task = m_items.Dequeue();
                        }
                    }

                    if ( task != null )
                    {
                        task.Item1( task.Item2 );

                        if ( InnerException != null ) // the method threw an exception
                        {
                            throw new AggregateException( "AsyncHelpers.Run method threw an exception.", InnerException );
                        }
                    }
                    else
                    {
                        m_workItemsWaiting.WaitOne();
                    }
                }
            }
        }
    }
}
