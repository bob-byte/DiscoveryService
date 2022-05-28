using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;

namespace LUC.Services.Implementation.Helpers
{
    partial class DispatcherWinFormsCompatAdapter : ISynchronizeInvoke
    {
        private readonly Dispatcher m_dispatcher;

        public DispatcherWinFormsCompatAdapter( Dispatcher dispatcher )
        {
            m_dispatcher = dispatcher;
        }

        public Boolean InvokeRequired => m_dispatcher.Thread != Thread.CurrentThread;

        public IAsyncResult BeginInvoke( Delegate method, Object[] args )
        {
            IAsyncResult asyncResult;
            DispatcherPriority dispatcherPriority = DispatcherPriority.Normal;

            if ( args?.Length > 1 )
            {
                Object[] argsWithoutFirst = ArgsAfterFirst( args );
                DispatcherOperation dispatcherOperation = m_dispatcher.BeginInvoke( dispatcherPriority, method, args[ 0 ], argsWithoutFirst );

                asyncResult = new DispatcherAsyncResultAdapter( dispatcherOperation );
            }
            else if ( args != null )
            {
                asyncResult = new DispatcherAsyncResultAdapter( m_dispatcher.BeginInvoke( dispatcherPriority, method, args[ 0 ] ) );
            }
            else
            {
                asyncResult = new DispatcherAsyncResultAdapter( m_dispatcher.BeginInvoke( method, dispatcherPriority ) );
            }

            return asyncResult;
        }

        private static Object[] ArgsAfterFirst( Object[] args )
        {
            Object[] result = new Object[ args.Length - 1 ];
            Array.Copy( args, sourceIndex: 1, destinationArray: result, destinationIndex: 0, args.Length - 1 );

            return result;
        }

        public Object EndInvoke( IAsyncResult resultOfBeginOp )
        {
            var adaptedResult = resultOfBeginOp as DispatcherAsyncResultAdapter;
            if ( adaptedResult != null )
            {
                adaptedResult.WaitOperationCompletion();
                return adaptedResult.DispatcherOperation.Result;
            }
            else
            {
                throw new InvalidCastException( $"Cannot cast {resultOfBeginOp} to necessary type" );
            }
        }

        public Object Invoke( Delegate method, Object[] args )
        {
            DispatcherPriority dispatcherPriority = DispatcherPriority.Normal;
            Object result;

            if ( args?.Length > 1 )
            {
                Object[] argsWithoutFirst = ArgsAfterFirst( args );
                result = m_dispatcher.Invoke( dispatcherPriority, method, args[ 0 ], argsWithoutFirst );
            }
            else if ( args != null )
            {
                result = m_dispatcher.Invoke( dispatcherPriority, method, args[ 0 ] );
            }
            else
            {
                result = m_dispatcher.Invoke( dispatcherPriority, method );
            }

            return result;
        }
    }
}
