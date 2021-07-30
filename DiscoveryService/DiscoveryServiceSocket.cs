using LUC.DiscoveryService.Extensions;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages;
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

namespace LUC.DiscoveryService
{
    public class DiscoveryServiceSocket : Socket
    {
        private readonly Object m_lock = new Object();
        //private Exception innerException;

        private readonly TimeSpan howOftenCheckAcceptedClient = TimeSpan.FromSeconds(value: 0.5);

        private AutoResetEvent acceptDone;
        private AutoResetEvent connectDone;
        private AutoResetEvent receiveDone;
        private AutoResetEvent sendDone;
        private AutoResetEvent disconnectDone;

        [Import(typeof(ILoggingService))]
        internal static ILoggingService Log { get; private set; }

        public DiscoveryServiceSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, ILoggingService loggingService)
            : base(addressFamily, socketType, protocolType)
        {
            State = SocketState.Created;
            Log = loggingService;
        }

        /// <inheritdoc/>
        public DiscoveryServiceSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, BigInteger contactId, ILoggingService loggingService)
            : base(addressFamily, socketType, protocolType)
        {
            ContactId = contactId;
            State = SocketState.Created;
            Log = loggingService;
        }

        public BigInteger ContactId { get; set; }

        public SocketState State { get; private set; }

        private readonly ConcurrentQueue<Socket> acceptedSockets = new ConcurrentQueue<Socket>();

        private async Task<Socket> AcceptedSocketAsync(Int32 lengthStorageOfAcceptedSockets, TimeSpan howOftenCheckAcceptedClient)
        {
            if (acceptDone == null)
            {
                acceptDone = new AutoResetEvent(initialState: false);
            }

            Task.Run(async () =>
            {
                if(acceptedSockets.Count >= lengthStorageOfAcceptedSockets)
                {
                    acceptedSockets.TryDequeue(out _);
                }

                acceptedSockets.Enqueue(await this.AcceptAsync());
                if(acceptedSockets.Count == 1)
                {
                    acceptDone.Set();
                }
            }).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter();

            Socket acceptedSocket = null;
            await Task.Run(async () =>
            {
                if (acceptedSockets.Count == 0)
                {
                    acceptDone.WaitOne();
                }

                while (acceptedSocket == null)
                {
                    foreach (var socket in acceptedSockets)
                    {
                        if (socket.Available > 0)
                        {
                            acceptedSocket = socket;
                            break;
                        }
                    }

                    if(acceptedSocket == null)
                    {
                        await Task.Delay(howOftenCheckAcceptedClient);
                    }
                }
            }).ConfigureAwait(false);
            
            return acceptedSocket;
        }

        public async Task<TcpMessageEventArgs> ReceiveAsync(TimeSpan timeoutToRead, Int32 lengthStorageOfAcceptedSockets)
        {
            IPEndPoint ipEndPoint;
            Socket clientToReadMessage;
            Byte[] readBytes;

            try
            {
                clientToReadMessage = await AcceptedSocketAsync(lengthStorageOfAcceptedSockets, howOftenCheckAcceptedClient);
                if (receiveDone == null)
                {
                    receiveDone = new AutoResetEvent(initialState: false);
                }
                var taskReadBytes = ReadBytesAsync(clientToReadMessage);

                var isReceivedInTime = receiveDone.WaitOne(timeoutToRead);
                if(isReceivedInTime)
                {
                    readBytes = taskReadBytes.Result;
                    ipEndPoint = clientToReadMessage.RemoteEndPoint as IPEndPoint;
                }
                else
                {
                    throw new TimeoutException($"Timeout to read data from {clientToReadMessage.RemoteEndPoint}");
                }
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            catch(SocketException)
            {
                if(State == SocketState.Closed)
                {
                    throw new ObjectDisposedException($"Socket {LocalEndPoint} is disposed");
                }
                else
                {
                    throw;
                }
            }

            TcpMessageEventArgs receiveResult = new TcpMessageEventArgs();
            if (ipEndPoint != null)
            {
                receiveResult.Buffer = readBytes;
                receiveResult.SendingEndPoint = ipEndPoint;
                receiveResult.AcceptedSocket = clientToReadMessage;
                receiveResult.LocalContactId = ContactId;
                receiveResult.LocalEndPoint = LocalEndPoint;
            }
            else
            {
                throw new InvalidOperationException("Cannot convert remote end point to IPEndPoint");
            }

            return receiveResult;
        }

        /// <summary>
        ///   Reads all available data
        /// </summary>
        private async Task<Byte[]> ReadBytesAsync(Socket socketToRead)
        {
            List<Byte> allMessage = new List<Byte>();
            var availableDataToRead = socketToRead.Available;
            for (Int32 countReadBytes = 1; countReadBytes > 0 && availableDataToRead > 0; )
            {
                var buffer = new ArraySegment<Byte>(new Byte[availableDataToRead]);
                countReadBytes = await socketToRead.ReceiveAsync(buffer, SocketFlags.None);
                allMessage.AddRange(buffer);

                availableDataToRead = socketToRead.Available;
            }
            
            receiveDone?.Set();

            return allMessage.ToArray();
        }

        public async Task ConnectAsync(EndPoint remoteEndPoint, TimeSpan timeoutToConnect)
        {
            var taskConnection = Task.Run(() =>
            {
                try
                {
                    Connect(remoteEndPoint, timeoutToConnect);
                }
                catch (SocketException ex)
                {
                    Log.LogError(ex.ToString());
                }
                catch (TimeoutException ex)
                {
                    Log.LogError(ex.ToString());
                }
            });
            await taskConnection.ConfigureAwait(continueOnCapturedContext: false);

            if (taskConnection.Exception != null)
            {
                throw taskConnection.Exception;
            }
        }

        public void Connect(EndPoint remoteEndPoint, TimeSpan timeout)
        {
            if(connectDone == null)
            {
                connectDone = new AutoResetEvent(initialState: false);
            }

            VerifyWorkState();

            BeginConnect(remoteEndPoint, new AsyncCallback(ConnectCallback), state: this);
            var isConnected = connectDone.WaitOne(timeout);
            
            if (isConnected)
            {
                State = SocketState.Connected;
            }
            else
            {
                throw new TimeoutException();
            }
        }

        private void VerifyWorkState()
        {
            if((SocketState.Closing <= State) && (State <= SocketState.Failed))
            {
                String messageError = "Wanted to use idle socket";

                //loggingService.LogError(messageError);
                throw new InvalidOperationException(messageError);
            }
        }

        private void ConnectCallback(IAsyncResult asyncResult)
        {
            //Retrieve the socket from the state object
            try
            {
                var client = (Socket)asyncResult.AsyncState;

                //Complete the connection
                client.EndConnect(asyncResult);
                Log.LogInfo($"Socket connected to {client.RemoteEndPoint}");

                //Signal that the connection has been made
                connectDone.Set();
            }
            catch(SocketException ex)
            {
                Log.LogError(ex.ToString());
            }
            catch(InvalidCastException ex)
            {
                Log.LogError(ex.ToString());
            }
        }

        public Byte[] Receive(TimeSpan timeout)
        {
            if (receiveDone == null)
            {
                receiveDone = new AutoResetEvent(initialState: false);
            }

            Boolean isReceived;
            Task<Byte[]> taskReadBytes;
            try
            {
                taskReadBytes = ReadBytesAsync(this);
                isReceived = receiveDone.WaitOne(timeout);
            }
            catch (SocketException)
            {
                State = SocketState.Disconnected;
                throw;
            }

            if(isReceived)
            {
                State = SocketState.Connected;

                return taskReadBytes.Result;
            }
            else
            {
                throw new TimeoutException();
            }
        }

        //Maybe we should use lock to avoid sending several packages
        public void Send(Byte[] bytesToSend, TimeSpan timeout)
        {
            //lock(sendDone)
            //{
                VerifyConnected();

                if (sendDone == null)
                {
                    sendDone = new AutoResetEvent(initialState: false);
                }

                //Begin sending the data to the remote device
                BeginSend(bytesToSend, offset: 0, bytesToSend.Length, SocketFlags.None, new AsyncCallback(SendCallback), this);
                var isSent = sendDone.WaitOne(timeout);

                if (isSent)
                {
                    State = SocketState.Connected;
                }
                else
                {
                    throw new TimeoutException();
                }
            //}
        }

        public void VerifyConnected()
        {
            lock (m_lock)
            {
                if (State == SocketState.Closed)
                {
                    throw new ObjectDisposedException(nameof(DiscoveryServiceSocket));
                }
                else if (!Connected || ((SocketState.Disconnected <= State) && (State <= SocketState.Failed)))
                {
                    throw new InvalidOperationException("ServerSession is not connected.");
                }
            }
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
                //Retrieve the socket from the state object
                Socket client = (Socket)asyncResult.AsyncState;

                //Complete sending the data to the remote device
                var bytesSent = client.EndSend(asyncResult);
                Log.LogInfo($"Sent {bytesSent} bytes to {client.RemoteEndPoint}");

                sendDone.Set();
        }

        public void Disconnect(Boolean reuseSocket, TimeSpan timeout)
        {
            if (disconnectDone == null)
            {
                disconnectDone = new AutoResetEvent(initialState: false);
            }

            VerifyConnected();

            BeginDisconnect(reuseSocket, new AsyncCallback(DisconnectCallback), this);
            var isDisconnected = disconnectDone.WaitOne(timeout);
            if(isDisconnected)
            {
                State = SocketState.Disconnected;
            }
            else
            {
                throw new TimeoutException();
            }
        }

        private void DisconnectCallback(IAsyncResult asyncResult)
        {
            var socket = (Socket)asyncResult.AsyncState;
            socket.EndDisconnect(asyncResult);

            disconnectDone.Set();
        }

        public async Task<Boolean> DisconnectAsync(Boolean reuseSocket, TimeSpan timeout)
        {
            var disconnectTask = Task.Run(() =>
            {
                 Disconnect(reuseSocket, timeout);
            });
            await disconnectTask;

            Boolean isDisconnected;
            if(disconnectTask.Exception == null)
            {
                isDisconnected = true;
            }
            else
            {
                isDisconnected = false;
            }

            return isDisconnected;
        }

        private void VerifyState(SocketState state)
        {
            if (State != state)
            {
                Log.LogError($"Session {RemoteEndPoint} should have SessionStateExpected {state} but was SessionState {State}");
                throw new InvalidOperationException($"Expected state to be {state} but was {State}."/*.FormatInvariant(state, State)*/);
            }
        }

        public new void Dispose()
        {
            State = SocketState.Closed;

            foreach (var acceptedSocket in acceptedSockets)
            {
                try
                {
                    acceptedSocket.Dispose();
                }
                catch
                {
                    //eat it
                }
            }
            DisposeAllResetEvent();

            base.Dispose();
        }

        private void DisposeAllResetEvent()
        {
            acceptDone?.Dispose();
            connectDone?.Dispose();
            sendDone?.Dispose();
            receiveDone?.Dispose();
            disconnectDone?.Dispose();
        }
    }
}