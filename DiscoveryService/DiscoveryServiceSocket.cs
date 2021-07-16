using LUC.DiscoveryService.Extensions;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.Protocols.Tcp;
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

        private readonly TimeSpan howOftenCheckAcceptedClient = TimeSpan.FromSeconds(value: 0.5);

        private AutoResetEvent acceptDone;
        private AutoResetEvent connectDone;
        private AutoResetEvent receiveDone;
        private AutoResetEvent sendDone;
        private AutoResetEvent disconnectDone;

        [Import(typeof(ILoggingService))]
        protected readonly ILoggingService log;

        public DiscoveryServiceSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, ILoggingService loggingService)
            : base(addressFamily, socketType, protocolType)
        {
            State = SocketState.Created;
            log = loggingService;
        }

        /// <inheritdoc/>
        public DiscoveryServiceSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, BigInteger contactId, ILoggingService loggingService)
            : base(addressFamily, socketType, protocolType)
        {
            ContactId = contactId;
            State = SocketState.Created;
            this.log = loggingService;
        }

        public BigInteger ContactId { get; set; }

        public SocketState State { get; private set; }

        private readonly ConcurrentQueue<Socket> acceptedSockets = new ConcurrentQueue<Socket>();

        private async Task<Socket> AcceptedSocketAsync(Int32 maxAcceptedSockets, TimeSpan howOftenCheckAcceptedClient)
        {
            if (acceptDone == null)
            {
                acceptDone = new AutoResetEvent(initialState: false);
            }

            Task.Run(async () =>
            {
                if(acceptedSockets.Count >= maxAcceptedSockets)
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

        public async Task<TcpMessageEventArgs> ReceiveAsync(TimeSpan timeoutToRead, Int32 maxAcceptedSockets)
        {
            IPEndPoint ipEndPoint;
            Socket clientToReadMessage;
            Byte[] readBytes;

            try
            {
                clientToReadMessage = await AcceptedSocketAsync(maxAcceptedSockets, howOftenCheckAcceptedClient);

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
                receiveResult.RemoteContact = ipEndPoint;
                receiveResult.LocalContactId = ContactId;
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

        public async Task<Boolean> ConnectAsync(EndPoint remoteEndPoint, TimeSpan timeoutToConnect)
        {
            return await Task.Run(() =>
            {
                Connect(remoteEndPoint, timeoutToConnect, out var isConnected);
                return isConnected;
            });
        }

        public void Connect(EndPoint remoteEndPoint, TimeSpan timeout, out Boolean isConnected)
        {
            if(connectDone == null)
            {
                connectDone = new AutoResetEvent(initialState: false);
            }

            VerifyWorkState();

            try
            {
                BeginConnect(remoteEndPoint, new AsyncCallback(ConnectCallback), this);
            }
            catch(Exception ex)
            {
                log.LogError(ex.Message);
            }
            isConnected = connectDone.WaitOne(timeout);

            if(isConnected)
            {
                State = SocketState.Connected;
            }
        }

        private void VerifyWorkState()
        {
            if((State == SocketState.Closing) | (State == SocketState.Failed))
            {
                String messageError = "Wanted to use idle socket";

                //loggingService.LogError(messageError);
                throw new InvalidOperationException(messageError);
            }
        }

        private void ConnectCallback(IAsyncResult asyncResult)
        {
            //Retrieve the socket from the state object
            var client = (Socket)asyncResult.AsyncState;
            if(client != null)
            {
                //Complete the connection
                client.EndConnect(asyncResult);
                log.LogInfo($"Socket connected to {client.RemoteEndPoint}");

                //Signal that the connection has been made
                connectDone.Set();
            }
            else
            {
                throw new InvalidCastException($"Cannot convert from {asyncResult.AsyncState.GetType()} to {client.GetType()}");
            }
        }

        public Byte[] Receive(TimeSpan timeout, out Boolean isReceived)
        {
            if (receiveDone == null)
            {
                receiveDone = new AutoResetEvent(initialState: false);
            }

            var takReadBytes = ReadBytesAsync(this);
            isReceived = receiveDone.WaitOne(timeout);
            if(isReceived)
            {
                State = SocketState.Connected;
            }

            return takReadBytes.Result;
        }

        public void Send(Byte[] bytesToSend, TimeSpan timeout, out Boolean isSent)
        {
            VerifyWorkState();

            if (sendDone == null)
            {
                sendDone = new AutoResetEvent(initialState: false);
            }

            //Begin sending the data to the remote device
            BeginSend(bytesToSend, offset: 0, bytesToSend.Length, SocketFlags.None, new AsyncCallback(SendCallback), this);
            isSent = sendDone.WaitOne(timeout);
            if (isSent)
            {
                State = SocketState.Connected;
            }
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            try
            {
                //Retrieve the socket from the state object
                Socket client = (Socket)asyncResult.AsyncState;

                //Complete sending the data to the remote device
                var bytesSent = client.EndSend(asyncResult);
                log.LogInfo($"Sent {bytesSent} bytes to {client.RemoteEndPoint}");

                sendDone.Set();
            }
            catch (Exception ex)
            {
                log.LogInfo($"Exception occurred during send operation: {ex.Message}");
            }
        }

        public void Disconnect(Boolean reuseSocket, TimeSpan timeout, out Boolean isDisconnected)
        {
            if (disconnectDone == null)
            {
                disconnectDone = new AutoResetEvent(initialState: false);
            }

            VerifyConnected();

            BeginDisconnect(reuseSocket, new AsyncCallback(DisconnectCallback), this);
            isDisconnected = disconnectDone.WaitOne(timeout);
            if(isDisconnected)
            {
                State = SocketState.Disconnected;
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
            return await Task.Run(() =>
            {
                 Disconnect(reuseSocket, timeout, out var isDisconnected);
                 return isDisconnected;
            });
        }

        private void VerifyState(SocketState state)
        {
            if (State != state)
            {
                log.LogError($"Session {RemoteEndPoint} should have SessionStateExpected {state} but was SessionState {State}");
                throw new InvalidOperationException($"Expected state to be {state} but was {State}."/*.FormatInvariant(state, State)*/);
            }
        }

        public void VerifyConnected()
        {
            lock (m_lock)
            {
                if (State == SocketState.Closed)
                {
                    throw new ObjectDisposedException(nameof(DiscoveryServiceSocket));
                }
                else if ((State == SocketState.Disconnected) | (State == SocketState.Failed))
                {
                    throw new InvalidOperationException("ServerSession is not connected.");
                }
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

            base.Dispose();
        }
    }
}