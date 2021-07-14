using LUC.DiscoveryService.Kademlia.Protocols.Tcp;
using LUC.DiscoveryService.Messages;
using LUC.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService
{
    class DiscoveryServiceSocket : Socket
    {
        private readonly Object lockerConnect = new Object();
        private AutoResetEvent connectDone;

        private readonly Object lockerReceive = new Object();
        private AutoResetEvent receiveDone;

        private readonly Object lockerSend = new Object();
        private AutoResetEvent sendDone;

        private AutoResetEvent disconnectDone;

        [Import(typeof(ILoggingService))]
        private ILoggingService LoggingService;

        /// <inheritdoc/>
        public DiscoveryServiceSocket(SocketType socketType, ProtocolType protocolType)
            : base(socketType, protocolType)
        {
            ;//do nothing
        }

        /// <inheritdoc/>
        public DiscoveryServiceSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
            : base(addressFamily, socketType, protocolType)
        {
            ;//do nothing
        }

        public DiscoveryServiceSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, BigInteger contactId)
            : base(addressFamily, socketType, protocolType)
        {
            ContactId = contactId;
        }

        public BigInteger ContactId { get; set; }

        public Boolean IsDisposed { get; private set; } = false;

        public async Task<TcpMessageEventArgs> ReceiveAsync(TimeSpan timeoutToRead)
        {
            IPEndPoint ipEndPoint = null;
            Socket clientToReadMessage = null;
            Byte[] readBytes = null;

            try
            {
                clientToReadMessage = await this.AcceptAsync();

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
                //LoggingService.LogFatal(ex.Message);
            }
            catch(SocketException)/* when (ex.SocketErrorCode == SocketError.)*/
            {
                if(IsDisposed)
                {
                    throw new ObjectDisposedException($"Socket {LocalEndPoint} is disposed");
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                clientToReadMessage?.Close();
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

            BeginConnect(remoteEndPoint, new AsyncCallback(ConnectCallback), this);
            isConnected = connectDone.WaitOne(timeout);
        }

        private void ConnectCallback(IAsyncResult asyncResult)
        {
            Socket client = null;
            try
            {
                //Retrieve the socket from the state object
                client = (Socket)asyncResult.AsyncState;

                //Complete the connection
                client.EndConnect(asyncResult);
                //LoggingService.LogInfo($"Socket connected to {client.RemoteEndPoint}");

                //Signal that the connection has been made
                connectDone.Set();
            }
            catch (Exception e)
            {
                //LoggingService.LogInfo($"Failed to connect to {client?.RemoteEndPoint}: {e.Message}");
            }
        }

        public Byte[] ReceiveAsync(TimeSpan timeout, out Boolean isReceived)
        {
            if (receiveDone == null)
            {
                receiveDone = new AutoResetEvent(initialState: false);
            }

            var takReadBytes = ReadBytesAsync(this);

            isReceived = receiveDone.WaitOne(timeout);
            return takReadBytes.Result;
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            StateObjectForReceivingData stateReceiving = (StateObjectForReceivingData)asyncResult.AsyncState;
            var client = stateReceiving.WorkSocket;

            try
            {
                //Read data from the remote device
                var bytesRead = client.EndReceive(asyncResult);

                if (bytesRead > 0)
                {
                    //There might be more data, so store the data received so far
                    stateReceiving.ResultMessage.AddRange(stateReceiving.Buffer);

                    //Get the rest of the data
                    if (client.Available > 0)
                    {
                        stateReceiving.Buffer = new Byte[stateReceiving.BufferSize];
                        List<ArraySegment<Byte>> resultMess = new List<ArraySegment<Byte>>();

                        client.Receive(resultMess, SocketFlags.None);
                        client.BeginReceive(stateReceiving.Buffer, offset: 0, stateReceiving.BufferSize, SocketFlags.None, out var error, new AsyncCallback(ReceiveCallback), stateReceiving);

                        if(error != SocketError.Success)
                        {
                            throw new SocketException((Int32)error);
                        }
                    }
                }
                else
                {
                    //All the data has arrived; put it in response
                    receiveDone.Set();
                    return;
                }
            }
            catch (Exception ex)
            {
                //LoggingService.LogInfo($"Exception occurred during send operation: {ex.Message}");
            }
        }

        public void Send(Byte[] bytesToSend, TimeSpan timeout, out Boolean isSent)
        {
            if (sendDone == null)
            {
                sendDone = new AutoResetEvent(initialState: false);
            }

            //Begin sending the data to the remote device
            BeginSend(bytesToSend, offset: 0, bytesToSend.Length, SocketFlags.None, new AsyncCallback(SendCallback), this);
            isSent = sendDone.WaitOne(timeout);
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            try
            {
                //Retrieve the socket from the state object
                Socket client = (Socket)asyncResult.AsyncState;

                //Complete sending the data to the remote device
                var bytesSent = client.EndSend(asyncResult);
                //LoggingService.LogInfo($"Sent {bytesSent} bytes to {client.RemoteEndPoint}");

                sendDone.Set();
            }
            catch (Exception ex)
            {
                //LoggingService.LogInfo($"Exception occurred during send operation: {ex.Message}");
            }
        }

        public void Disconnect(Boolean reuseSocket, TimeSpan timeout, out Boolean isConnected)
        {
            if (disconnectDone == null)
            {
                disconnectDone = new AutoResetEvent(initialState: false);
            }

            BeginDisconnect(reuseSocket, (asyncResult) =>
            {
                var socket = (Socket)asyncResult.AsyncState;
                socket.EndDisconnect(asyncResult);

                disconnectDone.Set();
            }, this);

            isConnected = disconnectDone.WaitOne(timeout);
        }

        public async Task<Boolean> DisconnectAsync(Boolean reuseSocket, TimeSpan timeout)
        {
            return await Task.Run(() =>
            {
                 Disconnect(reuseSocket, timeout, out var isDisconnected);
                 return isDisconnected;
            });
        }

        public new void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }
    }
}