﻿using LUC.DiscoveryServices.Common.Extensions;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Kademlia.ClientPool;
using LUC.DiscoveryServices.Messages;
using LUC.Interfaces;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Common
{
    /// <summary>
    /// New socket methods are marked <a href="Ds"/> in the front of the name, so only there <see cref="State"/> will be changed
    /// </summary>
    public class AsyncSocket : Socket
    {
        protected volatile SocketState m_state;

        [Import( typeof( ILoggingService ) )]
        internal static ILoggingService Log { get; private set; }

        /// <inheritdoc/>
        public AsyncSocket( AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, BigInteger contactId, ILoggingService loggingService )
            : this( addressFamily, socketType, protocolType, loggingService )
        {
            ContactId = contactId;
        }

        public AsyncSocket( AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, ILoggingService loggingService )
            : base( addressFamily, socketType, protocolType )
        {
            m_state = SocketState.Creating;
            Log = loggingService;

            m_state = SocketState.Created;
        }

        public BigInteger ContactId { get; set; }

        public SocketState State 
        { 
            get => m_state; 
            protected set => m_state = value; 
        }

        public void DsAccept( TimeSpan timeout, out Socket acceptedSocket )
        {
            VerifyWorkState();

            AutoResetEvent acceptDone = new AutoResetEvent( initialState: false );
            StateObjectForAccept stateAccept = new StateObjectForAccept( this, acceptDone );
            m_state = SocketState.Accepting;

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
                    throw new TimeoutException();
                }
            }
            finally
            {
                stateAccept.Dispose();
            }            
        }

        public async Task DsConnectAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, CancellationToken cancellationToken = default )
        {
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                await DsConnectAsync( remoteEndPoint, timeoutToConnect, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                DsConnect( remoteEndPoint, timeoutToConnect, cancellationToken );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }
        }

        public async Task DsConnectAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, CancellationToken cancellationToken = default ) =>
            await Task.Run( () => DsConnect( remoteEndPoint, timeoutToConnect, cancellationToken ) ).ConfigureAwait( continueOnCapturedContext: false );

        public void DsConnect( EndPoint remoteEndPoint, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            SocketOperation( SocketAsyncOperation.Connect, ( asyncCallback ) => BeginConnect( remoteEndPoint, asyncCallback, state: this ), timeout, cancellationToken );
        }

        public async Task<Byte[]> DsReceiveAsync( IOBehavior ioBehavior, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            Byte[] bytesOfResponse;
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                bytesOfResponse = await DsReceiveAsync( timeout, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                bytesOfResponse = DsReceive( timeout, cancellationToken );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }

            return bytesOfResponse;
        }

        //TODO: optimize it
        public async Task<Byte[]> DsReceiveAsync( TimeSpan timeout, CancellationToken cancellationToken = default ) =>
            await Task.Run( () => DsReceive( timeout, cancellationToken ) ).ConfigureAwait( continueOnCapturedContext: false );

        public Byte[] DsReceive( TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            Boolean isTaskEnded;
            Task<(Byte[], SocketException)> taskReadBytes;
            AutoResetEvent taskDone = new AutoResetEvent( initialState: false );
            CancellationTokenSource cancelSource = new CancellationTokenSource();

            try
            {
                taskReadBytes = ReadAllAvailableBytesAsync( socketToRead: this, taskDone, cancelSource.Token );

                isTaskEnded = IsInTimeCompleted( taskDone, timeout, cancellationToken );
            }
            finally
            {
                cancelSource.Cancel();
                cancelSource.Dispose();

                taskDone.Close();
            }

            (Byte[] readBytes, SocketException socketException) = taskReadBytes.GetAwaiter().GetResult();
            HandleSocketOperationResult( isTaskEnded, socketException );

            return readBytes;
        }
        
        public async Task DsSendAsync( Byte[] bytesToSend, TimeSpan timeoutToSend, IOBehavior ioBehavior, CancellationToken cancellationToken = default )
        {
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                await DsSendAsync( bytesToSend, timeoutToSend, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                DsSend( bytesToSend, timeoutToSend, cancellationToken );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }
        }

        //TODO: optimize it
        public async Task DsSendAsync( Byte[] bytesToSend, TimeSpan timeout, CancellationToken cancellationToken = default ) =>
            await Task.Run( () => DsSend( bytesToSend, timeout, cancellationToken ) ).ConfigureAwait( continueOnCapturedContext: false );

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

            Log.LogInfo( $"Sent {bytesToSend.Length} bytes" );
        }

        //It throws exceptions, not <see cref="Boolean"/> value, because if socket isn't connected we should immediately end method where VerifyConnected is call
        public void VerifyConnected()
        {
            if ( m_state == SocketState.Closed )
            {
                throw new ObjectDisposedException( nameof( AsyncSocket ) );
            }
            else if ( !Connected || ( ( SocketState.Disconnected <= m_state ) && ( m_state <= SocketState.Closed ) ) )
            {
                throw new SocketException( (Int32)SocketError.NotConnected );
            }
        }

        public async Task DsDisconnectAsync( IOBehavior ioBehavior, Boolean reuseSocket, 
            TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                await DsDisconnectAsync( reuseSocket, timeout, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                DsDisconnect( reuseSocket, timeout, cancellationToken );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} = {default( IOBehavior )}" );
            }
        }

        public void DsDisconnect( Boolean reuseSocket, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            SocketOperation(SocketAsyncOperation.Disconnect, (asyncCallback) => BeginDisconnect(reuseSocket, asyncCallback, state: this), timeout, cancellationToken);
        }

        public async Task DsDisconnectAsync( Boolean reuseSocket, TimeSpan timeout, CancellationToken cancellationToken = default ) =>
            await Task.Run( () => DsDisconnect( reuseSocket, timeout, cancellationToken ) ).ConfigureAwait( continueOnCapturedContext: false );

        public void SocketOperation(SocketAsyncOperation socketOp, Func<AsyncCallback, IAsyncResult> beginOperation, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            switch ( socketOp )
            {
                case SocketAsyncOperation.Connect:
                {
                    VerifyWorkState();
                    State = SocketState.Connecting;
                    Log.LogInfo( $"Start connection" );

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
                    Log.LogInfo( $"Start disconnection" );

                    break;
                }

                default:
                {
                    throw new NotImplementedException( "Doesn't supported" );
                }
            }

            AutoResetEvent operationDone = new AutoResetEvent( initialState: false );

            SocketException socketException = new SocketException((Int32)SocketError.Success);
            ObjectDisposedException disposedException = null;

            AsyncCallback asyncCallback = ( asyncResult ) => SocketOperationCallback( socketOp, asyncResult, operationDone, out disposedException, out socketException );

            try
            {
                beginOperation( asyncCallback );
                WaitResultOfAsyncSocketOperation( operationDone, timeout, ref socketException, ref disposedException, cancellationToken );
            }
            finally
            {
                operationDone.Close();
            }
        }

        public new void Dispose()
        {
            if ( ( SocketState.Created <= m_state ) && ( m_state <= SocketState.Disconnected ) )
            {
                m_state = SocketState.Closing;

                base.Dispose();

                m_state = SocketState.Closed;
            }
            else
            {
                throw new ObjectDisposedException( "Try to dispose already closed socket" );
            }
        }

        protected void VerifyWorkState()
        {
            if ( ( SocketState.Failed <= m_state ) && ( m_state <= SocketState.Closed ) )
            {
                //Wanted to use idle socket
                throw new SocketException( (Int32)SocketError.SocketError );
            }
        }

        /// <param name="socketException">
        /// If it is not ref parameter, it will not be updated by another thread
        /// </param>
        private void WaitResultOfAsyncSocketOperation( EventWaitHandle operationDone, TimeSpan timeout, ref SocketException socketException, ref ObjectDisposedException disposedException, CancellationToken cancellationToken = default)
        {
            Boolean isInTime = cancellationToken != default ? 
                cancellationToken.WaitHandle.WaitOne( timeout ) : 
                operationDone.WaitOne( timeout );

            Exception exception;

            if ( ( socketException.SocketErrorCode == SocketError.Success ) && ( isInTime ) )
            {
                return;
            }
            else if ( socketException.SocketErrorCode != SocketError.Success )
            {
                exception = socketException;
            }
            else if ( !isInTime )
            {
                exception = new TimeoutException();
            }
            else if ( disposedException != null)
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

        //Now it is only for Connect, Send and Disconnect 
        private void SocketOperationCallback(SocketAsyncOperation socketOp, IAsyncResult asyncResult, EventWaitHandle operationDone, out ObjectDisposedException disposedException, out SocketException socketException)
        {
            Socket socket = (Socket)asyncResult.AsyncState;
            socketException = new SocketException((Int32)SocketError.Success);
            disposedException = null;

            try
            {
                switch ( socketOp )
                {
                    case SocketAsyncOperation.Connect:
                    {
                        socket.EndConnect( asyncResult );
                        Log.LogInfo( $"Connection successfully made" );

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
                        Log.LogInfo( $"Disconnection successfully made" );

                        State = SocketState.Disconnected;

                        break;
                    }

                    default:
                    {
                        throw new NotImplementedException( "Doesn't supported" );
                    }
                }

                operationDone.SafeSet( out _ );
            }
            catch (SocketException ex)
            {
                HandleExceptionOfEndSocketOp( ex, operationDone, out socketException );
            }
            catch(ObjectDisposedException ex)
            {
                HandleExceptionOfEndSocketOp( ex, operationDone, out disposedException );
            }
        }

        private void HandleExceptionOfEndSocketOp<T>(Exception inEx, EventWaitHandle operationDone, out T outEx)
            where T: Exception
        {
            //in case if ThreadAbortException is thrown (the whole finally block will be finished first, as it is a critical section). 
            try
            {
                ;//do nothing
            }
            finally
            {
                outEx = inEx as T;
                State = SocketState.Failed;

                operationDone.SafeSet( isSet: out _ );
            }
        }

        /// <summary>
        ///   Reads all available data
        /// </summary>
        private async Task<(Byte[] readBytes, SocketException socketException)> ReadAllAvailableBytesAsync( Socket socketToRead, EventWaitHandle receiveDone, CancellationToken cancellationToken )
        {
            m_state = SocketState.Reading;
            SocketException socketException = new SocketException( (Int32)SocketError.Success );
            Byte[] readBytes = new Byte[ 0 ];
            try
            {
                readBytes = await socketToRead.ReadAllAvailableBytesAsync( receiveDone, Constants.MAX_CHUNK_READ_PER_ONE_TIME, Constants.MAX_AVAILABLE_READ_BYTES, cancellationToken ).
                     ConfigureAwait( continueOnCapturedContext: false );
            }
            catch ( SocketException ex )
            {
                //in case if ThreadAbortException is thrown (the whole finally block will be finished first, as it is a critical section). 
                try
                {
                    ;//do nothing
                }
                finally
                {
                    m_state = SocketState.Failed;
                    socketException = ex;
                    receiveDone.SafeSet( isSet: out _ );
                }

                return (readBytes, socketException);
            }
            finally
            {
                if(readBytes.Length == 0)
                {
                    String logRecord = Display.StringWithAttention( $"Read 0 bytes" );
                    Log.LogError( logRecord );
                }
            }

            m_state = SocketState.AlreadyRead;

            return (readBytes, socketException);
        }

        private void AcceptCallback( IAsyncResult asyncResult )
        {
            //Get the socket that handles the client request
            StateObjectForAccept stateAccept = (StateObjectForAccept)asyncResult.AsyncState;
            stateAccept.AcceptedSocket = stateAccept.Listener.EndAccept( asyncResult );
            m_state = SocketState.Accepted;

            //another thread can receive timeout and it will close stateAccept.AcceptDone
            if ( !stateAccept.AcceptDone.SafeWaitHandle.IsClosed )
            {
                //Signal that the connection has been made
                stateAccept.AcceptDone.Set();
            }
        }

        private Boolean IsInTimeCompleted( EventWaitHandle eventWait, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            Boolean isInTimeCompleted = cancellationToken != default ?
                cancellationToken.WaitHandle.WaitOne( timeout ) :
                eventWait.WaitOne( timeout );
            return isInTimeCompleted;
        }

        private void HandleSocketOperationResult( Boolean isInTime, SocketException socketException )
        {
            if ( socketException.SocketErrorCode != SocketError.Success )
            {
                throw socketException;
            }
            else if ( !isInTime )
            {
                throw new TimeoutException();
            }
        }
    }
}
