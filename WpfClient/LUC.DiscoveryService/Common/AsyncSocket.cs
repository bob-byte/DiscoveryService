using LUC.DiscoveryServices.Common.Extensions;
using LUC.Interfaces.Constants;

using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;

using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Common
{
    /// <summary>
    /// Async socket with cancellable operations.
    /// </summary>
    /// <remarks>
    /// New socket methods are marked <a href="Ds"/> in the front of the name, so <see cref="State"/> property will be changed only there.
    /// </remarks>
    public partial class AsyncSocket : Socket
    {
        private Int32 m_state;

        public AsyncSocket( AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType )
                    : base( addressFamily, socketType, protocolType )
        {
            ReceiveTimeout = (Int32)DsConstants.ReceiveOneChunkTimeout.TotalMilliseconds;
            SendTimeout = (Int32)DsConstants.SendTimeout.TotalMilliseconds;
            State = SocketState.Created;
        }

        public SocketState State
        {
            get => (SocketState)m_state;
            protected set => Interlocked.Exchange( ref m_state, (Int32)value );
        }

        public void DsAccept( TimeSpan timeout, out Socket acceptedSocket )
        {
            VerifyWorkState();

            var acceptDone = new AutoResetEvent( initialState: false );
            var stateAccept = new StateObjectForAccept( this, acceptDone );
            State = SocketState.Accepting;

            try
            {
                BeginAccept( new AsyncCallback( AcceptCallback ), stateAccept );
                Boolean isAccepted = acceptDone.WaitOne( timeout );

                if ( isAccepted )
                {
                    acceptedSocket = stateAccept.AcceptedSocket;
                }
                else
                {
                    throw new TimeoutException( message: $"Accept socket timeout occured" );
                }
            }
            finally
            {
                stateAccept.Dispose();
            }
        }

        //public SslStream DsSslConnect( EndPoint remoteEndPoint, TimeSpan timeout, CancellationToken cancellationToken = default )
        //{
        //    var networkStream = new NetworkStream( this );
        //    var sslStream = new SslStream( networkStream );
        //    sslStream.AuthenticateAsClient()
        //}

        public void DsConnect( EndPoint remoteEndPoint, TimeSpan timeout, CancellationToken cancellationToken = default ) =>
            SocketOperation(
                SocketAsyncOperation.Connect,
                ( asyncCallback ) =>
                {
                    DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Start to connect with {remoteEndPoint}" );
                    return BeginConnect( remoteEndPoint, asyncCallback, state: this );
                },
                timeout, cancellationToken
            );

        public async Task DsConnectAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IoBehavior ioBehavior, CancellationToken cancellationToken = default )
        {
            if ( ioBehavior == IoBehavior.Asynchronous )
            {
                await DsConnectAsync( remoteEndPoint, timeoutToConnect, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IoBehavior.Synchronous )
            {
                DsConnect( remoteEndPoint, timeoutToConnect, cancellationToken );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }
        }

        public Task DsConnectAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, CancellationToken cancellationToken = default ) =>
            Task.Run( () => DsConnect( remoteEndPoint, timeoutToConnect, cancellationToken ) );

        public void DsDisconnect( Boolean reuseSocket, TimeSpan timeout, CancellationToken cancellationToken = default ) =>
            SocketOperation(
                SocketAsyncOperation.Disconnect,
                beginOperation: ( asyncCallback ) => BeginDisconnect( reuseSocket, asyncCallback, state: this ),
                timeout,
                cancellationToken
            );

        public async Task DsDisconnectAsync( IoBehavior ioBehavior, Boolean reuseSocket,
            TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            if ( ioBehavior == IoBehavior.Asynchronous )
            {
                await DsDisconnectAsync( reuseSocket, timeout, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IoBehavior.Synchronous )
            {
                DsDisconnect( reuseSocket, timeout, cancellationToken );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} = {default( IoBehavior )}" );
            }
        }

        public async Task DsDisconnectAsync( Boolean reuseSocket, TimeSpan timeout, CancellationToken cancellationToken = default ) =>
            await Task.Run( () => DsDisconnect( reuseSocket, timeout, cancellationToken ) ).ConfigureAwait( continueOnCapturedContext: false );

        public async Task<Byte[]> DsReceiveAsync( IoBehavior ioBehavior, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            Byte[] bytesOfResponse;
            if ( ioBehavior == IoBehavior.Asynchronous )
            {
                bytesOfResponse = await DsReceiveAsync( timeout, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IoBehavior.Synchronous )
            {
                bytesOfResponse = DsReceive( timeout, cancellationToken );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }

            return bytesOfResponse;
        }

        public async Task<Byte[]> DsReceiveAsync( TimeSpan timeout, CancellationToken cancellationToken = default ) =>
            await Task.Run( () => DsReceive( timeout, cancellationToken ) ).ConfigureAwait( continueOnCapturedContext: false );

        public Byte[] DsReceive( TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            Boolean isInTimeCompleted;
            Task<Byte[]> taskReadBytes;
            var taskDone = new AsyncAutoResetEvent( set: false );
            var cancelSource = new CancellationTokenSource();

            try
            {
                taskReadBytes = ReadMessageBytesAsync( socketToRead: this, taskDone, cancelSource.Token );

                isInTimeCompleted = IsInTimeCompleted( taskDone, timeout, cancellationToken );
                if ( !isInTimeCompleted )
                {
                    throw new TimeoutException( message: $"Receive bytes timeout occured" );
                }
            }
            finally
            {
                cancelSource.Cancel();
                cancelSource.Dispose();
            }

            Byte[] readBytes = taskReadBytes.WaitAndUnwrapException();
            return readBytes;
        }

        public void DsSend( Byte[] bytesToSend, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            SocketOperation( SocketAsyncOperation.Send, ( asyncCallback ) =>
                BeginSend(
                    bytesToSend,
                    offset: 0,
                    bytesToSend.Length,
                    SocketFlags.None,
                    asyncCallback,
                    state: this
                ), timeout, cancellationToken );

            DsLoggerSet.DefaultLogger.LogInfo( $"Sent {bytesToSend.Length} bytes" );
        }

        public async Task DsSendAsync( Byte[] bytesToSend, TimeSpan timeoutToSend, IoBehavior ioBehavior, CancellationToken cancellationToken = default )
        {
            if ( ioBehavior == IoBehavior.Asynchronous )
            {
                await DsSendAsync( bytesToSend, timeoutToSend, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IoBehavior.Synchronous )
            {
                DsSend( bytesToSend, timeoutToSend, cancellationToken );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }
        }

        public async Task DsSendAsync( Byte[] bytesToSend, TimeSpan timeout, CancellationToken cancellationToken = default ) =>
            await Task.Run( () => DsSend( bytesToSend, timeout, cancellationToken ) ).ConfigureAwait( continueOnCapturedContext: false );

        public void SocketOperation( SocketAsyncOperation socketOp, Func<AsyncCallback, IAsyncResult> beginOperation, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            switch ( socketOp )
            {
                case SocketAsyncOperation.Connect:
                {
                    VerifyWorkState();
                    State = SocketState.Connecting;

                    break;
                }

                case SocketAsyncOperation.Send:
                {
                    VerifyConnected();
                    State = SocketState.SendingBytes;

                    break;
                }

                case SocketAsyncOperation.Disconnect:
                {
                    VerifyConnected();
                    State = SocketState.Disconnecting;
                    DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Start disconnection" );

                    break;
                }

                default:
                {
                    throw new NotImplementedException( message: $"{socketOp} isn't supported" );
                }
            }

            var operationDone = new AsyncAutoResetEvent( set: false );

            var socketException = new SocketException( (Int32)SocketError.Success );
            ObjectDisposedException disposedException = null;

            AsyncCallback asyncCallback = ( asyncResult ) => SocketOperationCallback( socketOp, asyncResult, operationDone, out disposedException, out socketException );

            beginOperation( asyncCallback );
            WaitResultOfAsyncSocketOperation( socketOp, operationDone, timeout, cancellationToken, ref socketException, ref disposedException );
        }

        //It throws exceptions, not <see cref="Boolean"/> value, because if socket isn't connected we should immediately end method where VerifyConnected is call
        public void VerifyConnected()
        {
            if ( State >= SocketState.Closing )
            {
                throw new ObjectDisposedException( nameof( AsyncSocket ) );
            }
            else if ( !Connected || ( ( SocketState.Disconnected <= State ) && ( State < SocketState.Closing ) ) )
            {
                throw new SocketException( (Int32)SocketError.NotConnected );
            }
        }

        protected override void Dispose( Boolean disposing )
        {
            if ( State < SocketState.Closing )
            {
                State = SocketState.Closing;

                base.Dispose( disposing );

                State = SocketState.Closed;
            }
        }

        protected void VerifyWorkState()
        {
            if ( ( SocketState.Failed <= State ) && ( State <= SocketState.Closed ) )
            {
                //Wanted to use idle socket
                throw new SocketException( (Int32)SocketError.SocketError );
            }
        }

        private void AcceptCallback( IAsyncResult asyncResult )
        {
            //Get the socket that handles the client request
            var stateAccept = (StateObjectForAccept)asyncResult.AsyncState;
            stateAccept.AcceptedSocket = stateAccept.Listener.EndAccept( asyncResult );
            State = SocketState.Accepted;

            //another thread can receive timeout and it will close stateAccept.AcceptDone
            if ( !stateAccept.AcceptDone.SafeWaitHandle.IsClosed )
            {
                //Signal that the connection has been made
                stateAccept.AcceptDone.Set();
            }
        }

        private void HandleExceptionOfEndSocketOp<T>( Exception inEx, AsyncAutoResetEvent operationDone, out T outEx )
                    where T : Exception
        {
            //in case if ThreadAbortException is thrown (the whole finally block will be finished first, as it is a critical section). 
            try
            {
                ;//do nothing
            }
            finally
            {
                outEx = null;
                Interlocked.Exchange( ref outEx, inEx as T );
                State = SocketState.Failed;

                operationDone.Set();
            }
        }

        private Boolean IsInTimeCompleted( AsyncAutoResetEvent eventWait, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            Boolean isInTimeCompleted = eventWait.Wait( timeout, cancellationToken );
            return isInTimeCompleted;
        }

        /// <summary>
        ///   Reads all available data
        /// </summary>
        private async Task<Byte[]> ReadMessageBytesAsync( Socket socketToRead, AsyncAutoResetEvent receiveDone, CancellationToken cancellationToken )
        {
            State = SocketState.Reading;
            Byte[] readBytes;

            try
            {
                readBytes = await socketToRead.ReadMessageBytesAsync(
                    receiveDone,
                    DsConstants.MAX_CHUNK_READ_PER_ONE_TIME,
                    DsConstants.MAX_AVAILABLE_READ_BYTES,
                    cancellationToken
                ).ConfigureAwait( continueOnCapturedContext: false );
            }
            catch ( SocketException ex )
            {
                HandleExceptionOfEndSocketOp<SocketException>( ex, receiveDone, outEx: out _ );
                DsLoggerSet.DefaultLogger.LogCriticalError( ex );

                throw;
            }
            catch ( InvalidOperationException ex )
            {
                HandleExceptionOfEndSocketOp<InvalidOperationException>( ex, receiveDone, outEx: out _ );
                DsLoggerSet.DefaultLogger.LogCriticalError( ex );

                throw;
            }

            if ( readBytes.Length == 0 )
            {
                DsLoggerSet.DefaultLogger.LogFatal( message: $"Read 0 bytes" );
            }

            State = SocketState.AlreadyRead;

            return readBytes;
        }

        //Now it is only for Connect, Send and Disconnect 
        private void SocketOperationCallback( SocketAsyncOperation socketOp, IAsyncResult asyncResult, AsyncAutoResetEvent operationDone, out ObjectDisposedException disposedException, out SocketException socketException )
        {
            var socket = (Socket)asyncResult.AsyncState;
            socketException = new SocketException( (Int32)SocketError.Success );
            disposedException = null;

            try
            {
                switch ( socketOp )
                {
                    case SocketAsyncOperation.Connect:
                    {
                        socket.EndConnect( asyncResult );
                        DsLoggerSet.DefaultLogger.LogInfo( $"Connection successfully made" );

                        State = SocketState.Connected;

                        break;
                    }

                    case SocketAsyncOperation.Send:
                    {
                        socket.EndSend( asyncResult );
                        State = SocketState.SentBytes;

                        break;
                    }

                    case SocketAsyncOperation.Disconnect:
                    {
                        socket.EndDisconnect( asyncResult );
                        DsLoggerSet.DefaultLogger.LogInfo( $"Disconnection successfully made" );

                        State = SocketState.Disconnected;

                        break;
                    }

                    default:
                    {
                        throw new NotImplementedException( message: $"{socketOp} doesn't supported" );
                    }
                }

                operationDone.Set();
            }
            catch ( SocketException ex )
            {
                HandleExceptionOfEndSocketOp( ex, operationDone, out socketException );
            }
            catch ( InvalidOperationException ex )
            {
                HandleExceptionOfEndSocketOp( ex, operationDone, out disposedException );
            }
        }

        /// <param name="socketException">
        /// If it is not ref parameter, it will not be updated by another thread
        /// </param>
        private void WaitResultOfAsyncSocketOperation( SocketAsyncOperation socketOp, AsyncAutoResetEvent operationDone, TimeSpan timeout, CancellationToken cancellationToken, ref SocketException socketException, ref ObjectDisposedException disposedException )
        {
            Boolean isInTime = operationDone.Wait( timeout, cancellationToken );

            Exception exception;

            if ( ( socketException.SocketErrorCode == SocketError.Success ) && isInTime )
            {
                return;
            }
            else if ( socketException.SocketErrorCode != SocketError.Success )
            {
                exception = socketException;
            }
            else if ( !isInTime )
            {
                exception = new TimeoutException( message: $"Timeout during {socketOp} occured" );
            }
            else if ( disposedException != null )
            {
                exception = disposedException;
            }
            else
            {
                //it can't be
                exception = new InvalidProgramException();
            }

            throw exception;
        }
    }
}